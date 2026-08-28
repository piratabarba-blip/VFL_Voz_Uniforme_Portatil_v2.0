import argparse
import json
import os
import subprocess
import sys


RESOURCE_RESERVE = 0.20
CHUNK_DURATION_SECONDS = 600


def configure_resources(packages_dir):
    """Detecta CUDA e preserva recursos para o Windows continuar responsivo."""
    logical_cores = os.cpu_count() or 4
    cpu_threads = max(2, int(logical_cores * (1.0 - RESOURCE_RESERVE)))
    os.environ["OMP_NUM_THREADS"] = str(cpu_threads)
    os.environ["MKL_NUM_THREADS"] = str(cpu_threads)
    os.environ["NUMEXPR_MAX_THREADS"] = str(cpu_threads)

    if packages_dir not in sys.path:
        sys.path.insert(0, packages_dir)

    cuda_runtime_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cuda_runtime")
    if os.path.isdir(cuda_runtime_dir):
        os.environ["PATH"] = cuda_runtime_dir + os.pathsep + os.environ.get("PATH", "")

    import torch
    import onnxruntime as ort

    torch.set_num_threads(cpu_threads)
    device = {
        "cuda": False,
        "cpu_threads": cpu_threads,
        "torch": torch,
        "ort": ort,
        "batch_size": 1,
        "ort_gpu_limit": None,
    }

    if "CUDAExecutionProvider" in ort.get_available_providers() and os.path.isdir(cuda_runtime_dir):
        try:
            query = subprocess.check_output(
                [
                    "nvidia-smi",
                    "--query-gpu=name,memory.total,memory.free",
                    "--format=csv,noheader,nounits",
                ],
                text=True,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            ).splitlines()[0]
            name, total_mb, free_mb = [part.strip() for part in query.split(",")]
            total_bytes = int(float(total_mb) * 1024 * 1024)
            free_bytes = int(float(free_mb) * 1024 * 1024)
        except Exception:
            name = "NVIDIA CUDA"
            total_bytes = 6 * 1024 ** 3
            free_bytes = total_bytes

        # O ONNX executa o modelo na RTX, enquanto o STFT leve permanece na CPU.
        # O limite abaixo preserva pelo menos 20% da VRAM para o Windows e outros apps.
        ort_limit = int(min(total_bytes * 0.72, free_bytes * 0.78))
        total_gb = total_bytes / (1024 ** 3)
        device.update({
            "cuda": True,
            "name": name,
            "total_bytes": total_bytes,
            "ort_gpu_limit": ort_limit,
            "batch_size": 2 if total_gb >= 10 else 1,
        })
        print(
            "VFL_DEVICE=GPU: " + name +
            " | CUDA/ONNX | perfil equilibrado",
            flush=True,
        )
    else:
        print(
            "VFL_DEVICE=CPU: " + str(cpu_threads) +
            " threads | perfil equilibrado",
            flush=True,
        )

    return device


def create_separator(device, model_dir, output_dir, force_cpu=False):
    from audio_separator.separator import Separator

    separator = Separator(
        log_level=20,
        model_file_dir=model_dir,
        output_dir=output_dir,
        output_format="WAV",
        use_soundfile=True,
        use_autocast=False,
        chunk_duration=CHUNK_DURATION_SECONDS,
        mdx_params={
            "hop_length": 1024,
            "segment_size": 256,
            "overlap": 0.25,
            "batch_size": device["batch_size"] if not force_cpu else 1,
            "enable_denoise": True,
        },
    )

    if force_cpu:
        separator.torch_device = device["torch"].device("cpu")
        separator.onnx_execution_provider = ["CPUExecutionProvider"]
    elif device["cuda"]:
        separator.onnx_execution_provider = [
            (
                "CUDAExecutionProvider",
                {
                    "device_id": 0,
                    "gpu_mem_limit": device["ort_gpu_limit"],
                    "arena_extend_strategy": "kSameAsRequested",
                    "cudnn_conv_algo_search": "HEURISTIC",
                },
            ),
            "CPUExecutionProvider",
        ]

    return separator


def active_onnx_providers(separator):
    """Recupera os providers da sessão escondida dentro da função de inferência."""
    try:
        model_run = separator.model_instance.model_run
        for cell in model_run.__closure__ or ():
            value = cell.cell_contents
            if hasattr(value, "get_providers"):
                return value.get_providers()
    except Exception:
        pass
    return []


def main():
    parser = argparse.ArgumentParser(description="VFL Voz Uniforme - separador de voz e musica")
    parser.add_argument("input")
    parser.add_argument("output_dir")
    parser.add_argument("model_dir")
    args = parser.parse_args()

    base_dir = os.path.dirname(os.path.abspath(__file__))
    packages_dir = os.path.join(base_dir, "packages")
    ffmpeg_dir = os.path.abspath(os.path.join(base_dir, "..", "ffmpeg", "bin"))
    os.environ["PATH"] = ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")
    os.makedirs(args.output_dir, exist_ok=True)
    os.makedirs(args.model_dir, exist_ok=True)

    device = configure_resources(packages_dir)

    def run_separation(force_cpu=False):
        print("VFL_STAGE=Carregando modelo de IA", flush=True)
        current = create_separator(device, args.model_dir, args.output_dir, force_cpu)
        current.load_model(model_filename="UVR-MDX-NET-Inst_HQ_3.onnx")
        providers = active_onnx_providers(current)
        print("VFL_PROVIDERS=" + ",".join(providers), flush=True)
        if device["cuda"] and not force_cpu and "CUDAExecutionProvider" not in providers:
            raise RuntimeError("A sessao ONNX nao conseguiu ativar CUDAExecutionProvider")
        print("VFL_STAGE=Separando voz e musica", flush=True)
        return current.separate(
            args.input,
            {"Vocals": "voz_vfl", "Instrumental": "musica_vfl"},
        )

    try:
        files = run_separation(False)
    except Exception as gpu_error:
        if not device["cuda"]:
            raise
        print("VFL_STAGE=GPU indisponivel durante o trabalho; continuando pela CPU", flush=True)
        print("VFL_GPU_FALLBACK=" + str(gpu_error), flush=True)
        try:
            device["torch"].cuda.empty_cache()
        except Exception:
            pass
        files = run_separation(True)
    result = {
        "files": [
            path if os.path.isabs(path) else os.path.abspath(os.path.join(args.output_dir, path))
            for path in files
        ]
    }
    print("VFL_RESULT=" + json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
