import argparse
import json
import os
import sys


def main():
    parser = argparse.ArgumentParser(description="VFL Voz Uniforme - separador de voz e musica")
    parser.add_argument("input")
    parser.add_argument("output_dir")
    parser.add_argument("model_dir")
    args = parser.parse_args()

    base_dir = os.path.dirname(os.path.abspath(__file__))
    packages_dir = os.path.join(base_dir, "packages")
    if packages_dir not in sys.path:
        sys.path.insert(0, packages_dir)

    ffmpeg_dir = os.path.abspath(os.path.join(base_dir, "..", "ffmpeg", "bin"))
    os.environ["PATH"] = ffmpeg_dir + os.pathsep + os.environ.get("PATH", "")
    os.makedirs(args.output_dir, exist_ok=True)
    os.makedirs(args.model_dir, exist_ok=True)

    print("VFL_STAGE=Carregando modelo de IA", flush=True)
    from audio_separator.separator import Separator

    separator = Separator(
        log_level=20,
        model_file_dir=args.model_dir,
        output_dir=args.output_dir,
        output_format="WAV",
        use_soundfile=True,
        mdx_params={
            "hop_length": 1024,
            "segment_size": 256,
            "overlap": 0.25,
            "batch_size": 1,
            "enable_denoise": True,
        },
    )
    separator.load_model(model_filename="UVR-MDX-NET-Inst_HQ_3.onnx")
    print("VFL_STAGE=Separando voz e musica", flush=True)
    files = separator.separate(
        args.input,
        {"Vocals": "voz_vfl", "Instrumental": "musica_vfl"},
    )
    result = {
        "files": [
            path if os.path.isabs(path) else os.path.abspath(os.path.join(args.output_dir, path))
            for path in files
        ]
    }
    print("VFL_RESULT=" + json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
