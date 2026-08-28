using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("VFL Voz Uniforme")]
[assembly: AssemblyDescription("Separacao por IA, limpeza e nivelamento de audio para videos")]
[assembly: AssemblyCompany("VFL")]
[assembly: AssemblyProduct("VFL Voz Uniforme")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace VozUniformeApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color colorWindow = Color.FromArgb(12, 14, 19);
        private readonly Color colorHeader = Color.FromArgb(17, 20, 27);
        private readonly Color colorCard = Color.FromArgb(24, 27, 35);
        private readonly Color colorInput = Color.FromArgb(34, 38, 49);
        private readonly Color colorSecondary = Color.FromArgb(43, 48, 61);
        private readonly Color colorBorder = Color.FromArgb(55, 61, 76);
        private readonly Color colorText = Color.FromArgb(244, 246, 250);
        private readonly Color colorMuted = Color.FromArgb(166, 173, 190);
        private readonly Color colorAccent = Color.FromArgb(226, 50, 68);
        private readonly Color colorGreen = Color.FromArgb(50, 205, 125);
        private readonly Color colorOrange = Color.FromArgb(245, 166, 35);

        private readonly string projectDir;
        private readonly string ffmpegPath;
        private readonly string ffprobePath;
        private readonly string aiPythonPath;
        private readonly string aiScriptPath;
        private readonly string aiModelsPath;

        private TextBox inputBox;
        private TextBox outputBox;
        private Button inputButton;
        private Button outputButton;
        private Button startButton;
        private Button clearButton;
        private Button cancelButton;
        private ComboBox profileBox;
        private ComboBox lufsBox;
        private ComboBox musicModeBox;
        private CheckBox uniformBox;
        private Panel progressTrack;
        private Panel progressFill;
        private Panel statusDot;
        private Label statusLabel;
        private Label statusBadge;
        private Label timeLabel;
        private Timer timer;

        private Process process;
        private readonly StringBuilder processErrors = new StringBuilder();
        private Stopwatch stopwatch;
        private string progressFile;
        private double duration;
        private int progressPercent;
        private bool cancelRequested;
        private string processingStage;
        private string tempWorkDir;
        private string extractedAudioPath;
        private string vocalsPath;
        private string instrumentalPath;
        private int selectedTarget;
        private string selectedProfile;
        private bool selectedUniform;
        private int selectedMusicMode;

        public MainForm()
        {
            projectDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            ffmpegPath = Path.Combine(projectDir, "third_party", "ffmpeg", "bin", "ffmpeg.exe");
            ffprobePath = Path.Combine(projectDir, "third_party", "ffmpeg", "bin", "ffprobe.exe");
            aiPythonPath = Path.Combine(projectDir, "third_party", "ai", "runtime", "python.exe");
            aiScriptPath = Path.Combine(projectDir, "third_party", "ai", "separate.py");
            aiModelsPath = Path.Combine(projectDir, "third_party", "ai", "models");
            BuildInterface();
        }

        private void BuildInterface()
        {
            Text = "VFL Voz Uniforme 2.0 - Audio com IA";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(900, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = colorWindow;
            ForeColor = colorText;
            Font = new Font("Segoe UI", 10f);

            Panel header = new Panel { Location = new Point(0, 0), Size = new Size(900, 100), BackColor = colorHeader };
            Controls.Add(header);
            header.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(6, 100), BackColor = colorAccent });

            Panel logo = new Panel { Location = new Point(27, 22), Size = new Size(56, 56), BackColor = colorAccent };
            logo.Controls.Add(new Label
            {
                Text = "VFL",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            });
            header.Controls.Add(logo);
            header.Controls.Add(MakeLabel("VFL Voz Uniforme", new Point(101, 18), new Size(420, 42), colorText, 22f, FontStyle.Bold));
            header.Controls.Add(MakeLabel("Separe voz e musica com IA, limpe as falas e preserve o video.", new Point(104, 59), new Size(560, 25), colorMuted, 10f, FontStyle.Regular));
            Label version = MakeLabel("IA LOCAL  |  v2.0", new Point(710, 35), new Size(160, 25), colorMuted, 9f, FontStyle.Regular);
            version.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(version);

            Panel card = new Panel { Location = new Point(26, 120), Size = new Size(848, 365), BackColor = colorCard };
            Controls.Add(card);
            card.Controls.Add(MakeLabel("NOVO PROCESSAMENTO", new Point(24, 17), new Size(250, 22), colorAccent, 9f, FontStyle.Bold));

            CreateFileRow(card, "Video de entrada", 48, out inputBox, out inputButton);
            CreateFileRow(card, "Salvar resultado em", 116, out outputBox, out outputButton);
            card.Controls.Add(new Panel { Location = new Point(26, 193), Size = new Size(796, 1), BackColor = colorBorder });

            card.Controls.Add(MakeLabel("Intensidade da limpeza", new Point(24, 210), new Size(240, 22), colorMuted, 9.5f, FontStyle.Regular));
            profileBox = MakeCombo(new Point(26, 235), new Size(230, 30), new[] { "Leve", "Normal", "Forte" }, 1);
            card.Controls.Add(profileBox);

            card.Controls.Add(MakeLabel("Volume final", new Point(284, 210), new Size(180, 22), colorMuted, 9.5f, FontStyle.Regular));
            lufsBox = MakeCombo(new Point(286, 235), new Size(250, 30), new[] { "-14 LUFS - YouTube", "-16 LUFS - Voz/Podcast", "-18 LUFS - Suave" }, 1);
            card.Controls.Add(lufsBox);

            card.Controls.Add(MakeLabel("Musica de fundo", new Point(563, 210), new Size(250, 22), colorMuted, 9.5f, FontStyle.Regular));
            musicModeBox = MakeCombo(new Point(565, 235), new Size(255, 30), new[]
            {
                "Video somente com voz",
                "Manter musica (suave)",
                "Deixar musica baixa - IA",
                "Remover musica - IA"
            }, 0);
            musicModeBox.SelectedIndexChanged += delegate
            {
                if (process != null) return;
                bool aiMode = musicModeBox.SelectedIndex >= 2;
                if (aiMode) uniformBox.Checked = true;
                SetStatus(aiMode ? "Modo IA: a voz e a musica serao separadas." : "Modo de audio selecionado.", "Ready");
            };
            card.Controls.Add(musicModeBox);

            uniformBox = new CheckBox
            {
                Text = "Uniformizar todas as vozes",
                Location = new Point(26, 269),
                Size = new Size(300, 24),
                Checked = true,
                BackColor = colorCard,
                ForeColor = colorText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            uniformBox.FlatAppearance.CheckedBackColor = colorAccent;
            card.Controls.Add(uniformBox);

            timeLabel = MakeLabel("Tempo  00:00:00", new Point(510, 267), new Size(312, 24), colorMuted, 9f, FontStyle.Bold);
            timeLabel.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(timeLabel);

            progressTrack = new Panel { Location = new Point(26, 295), Size = new Size(796, 8), BackColor = colorInput };
            progressFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 8), BackColor = colorAccent };
            progressTrack.Controls.Add(progressFill);
            card.Controls.Add(progressTrack);

            statusDot = new Panel { Location = new Point(27, 326), Size = new Size(10, 10), BackColor = colorMuted };
            card.Controls.Add(statusDot);
            statusLabel = MakeLabel("Selecione um video para comecar.", new Point(45, 319), new Size(650, 25), colorMuted, 10f, FontStyle.Regular);
            card.Controls.Add(statusLabel);
            statusBadge = MakeLabel("AGUARDANDO", new Point(695, 317), new Size(127, 25), colorMuted, 8f, FontStyle.Bold);
            statusBadge.TextAlign = ContentAlignment.MiddleRight;
            card.Controls.Add(statusBadge);

            startButton = MakeButton("Melhorar audio", new Point(26, 508), 180, colorAccent);
            clearButton = MakeButton("Limpar / proximo video", new Point(218, 508), 200, colorSecondary);
            cancelButton = MakeButton("Cancelar", new Point(430, 508), 115, colorSecondary);
            cancelButton.Enabled = false;
            Controls.Add(startButton);
            Controls.Add(clearButton);
            Controls.Add(cancelButton);

            Label footer = MakeLabel("O video original nunca e alterado.", new Point(620, 516), new Size(254, 25), colorMuted, 9f, FontStyle.Regular);
            footer.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(footer);

            inputButton.Click += SelectInput;
            outputButton.Click += SelectOutput;
            startButton.Click += StartProcessing;
            clearButton.Click += delegate { if (process == null) ResetApp(); };
            cancelButton.Click += CancelProcessing;
            FormClosing += OnFormClosing;

            timer = new Timer { Interval = 400 };
            timer.Tick += TimerTick;
        }

        private Label MakeLabel(string text, Point location, Size size, Color color, float fontSize, FontStyle style)
        {
            return new Label { Text = text, Location = location, Size = size, ForeColor = color, Font = new Font("Segoe UI", fontSize, style) };
        }

        private Button MakeButton(string text, Point location, int width, Color backColor)
        {
            Button button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 42),
                BackColor = backColor,
                ForeColor = colorText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private ComboBox MakeCombo(Point location, Size size, string[] items, int selectedIndex)
        {
            ComboBox combo = new ComboBox
            {
                Location = location,
                Size = size,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = colorInput,
                ForeColor = colorText
            };
            combo.Items.AddRange(items);
            combo.SelectedIndex = selectedIndex;
            return combo;
        }

        private void CreateFileRow(Panel card, string label, int y, out TextBox box, out Button button)
        {
            card.Controls.Add(MakeLabel(label, new Point(24, y), new Size(300, 22), colorMuted, 9.5f, FontStyle.Regular));
            box = new TextBox
            {
                Location = new Point(26, y + 24),
                Size = new Size(658, 30),
                BackColor = colorInput,
                ForeColor = colorText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f)
            };
            card.Controls.Add(box);
            button = MakeButton("Procurar", new Point(698, y + 21), 124, colorSecondary);
            button.Height = 34;
            card.Controls.Add(button);
        }

        private void SelectInput(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Videos|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v|Todos os arquivos|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                inputBox.Text = dialog.FileName;
                string directory = Path.GetDirectoryName(dialog.FileName);
                string name = Path.GetFileNameWithoutExtension(dialog.FileName);
                outputBox.Text = Path.Combine(directory, name + "_audio_melhorado.mp4");
                SetProgress(0);
                SetStatus("Video selecionado: " + Path.GetFileName(dialog.FileName), "Ready");
            }
        }

        private void SelectOutput(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Video MP4|*.mp4";
                dialog.DefaultExt = "mp4";
                if (dialog.ShowDialog(this) == DialogResult.OK) outputBox.Text = dialog.FileName;
            }
        }

        private void StartProcessing(object sender, EventArgs e)
        {
            if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath))
            {
                MessageBox.Show(this, "FFmpeg nao encontrado ao lado do programa.", "Dependencia ausente", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!File.Exists(inputBox.Text))
            {
                MessageBox.Show(this, "Selecione um video valido.", "Video ausente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (String.IsNullOrWhiteSpace(outputBox.Text) || String.Equals(inputBox.Text, outputBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Escolha um arquivo de saida diferente do original.", "Saida invalida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (File.Exists(outputBox.Text) && MessageBox.Show(this, "O resultado ja existe. Deseja substitui-lo?", "Substituir arquivo?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string probeError;
            if (!TryReadDuration(inputBox.Text, out duration, out probeError))
            {
                MessageBox.Show(this, String.IsNullOrWhiteSpace(probeError) ? "Nao foi possivel ler a duracao do video." : probeError, "Arquivo invalido", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            selectedTarget = new[] { -14, -16, -18 }[lufsBox.SelectedIndex];
            selectedProfile = profileBox.SelectedItem.ToString();
            selectedUniform = uniformBox.Checked;
            selectedMusicMode = musicModeBox.SelectedIndex;
            if (selectedMusicMode >= 2 && (!File.Exists(aiPythonPath) || !File.Exists(aiScriptPath)))
            {
                MessageBox.Show(this, "A engine de IA nao foi encontrada na pasta do programa.", "IA ausente", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            cancelRequested = false;
            progressPercent = 0;
            stopwatch = Stopwatch.StartNew();
            timeLabel.Text = "Tempo  00:00:00";
            SetProgress(0);
            SetBusy(true);
            timer.Start();

            if (selectedMusicMode >= 2) StartAiExtraction();
            else StartDirectProcessing();
        }

        private void StartDirectProcessing()
        {
            processingStage = "Direct";
            string filter = GetAudioFilter(selectedProfile, selectedTarget, selectedUniform, selectedMusicMode == 1);
            string[] arguments =
            {
                "-hide_banner", "-loglevel", "error", "-y", "-i", inputBox.Text,
                "-map", "0:v:0?", "-map", "0:a:0", "-map_metadata", "0", "-c:v", "copy",
                "-af", filter, "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
                "-movflags", "+faststart", "-progress", NewProgressFile(), "-nostats", outputBox.Text
            };
            SetStatus("Processando audio... 0%", "Processing");
            StartChildProcess(ffmpegPath, arguments, false);
        }

        private void StartAiExtraction()
        {
            processingStage = "Extract";
            tempWorkDir = Path.Combine(Path.GetTempPath(), "vfl_voz_uniforme_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempWorkDir);
            string stemsDir = Path.Combine(tempWorkDir, "stems");
            Directory.CreateDirectory(stemsDir);
            extractedAudioPath = Path.Combine(tempWorkDir, "audio_original.wav");
            vocalsPath = Path.Combine(stemsDir, "voz_vfl.wav");
            instrumentalPath = Path.Combine(stemsDir, "musica_vfl.wav");
            string[] arguments =
            {
                "-hide_banner", "-loglevel", "error", "-y", "-i", inputBox.Text,
                "-vn", "-map", "0:a:0", "-ac", "2", "-ar", "44100", "-c:a", "pcm_s16le",
                "-progress", NewProgressFile(), "-nostats", extractedAudioPath
            };
            SetStatus("Etapa 1/3: preparando audio para a IA...", "Processing");
            StartChildProcess(ffmpegPath, arguments, false);
        }

        private void StartAiSeparation()
        {
            processingStage = "Separate";
            CleanupProgressFile();
            string[] arguments =
            {
                aiScriptPath, extractedAudioPath, Path.GetDirectoryName(vocalsPath), aiModelsPath
            };
            SetProgress(15);
            progressPercent = 15;
            SetStatus("Etapa 2/3: separando voz e musica com IA...", "Processing");
            StartChildProcess(aiPythonPath, arguments, true);
        }

        private void StartAiMix()
        {
            processingStage = "Mix";
            string voiceFilter = GetAudioFilter(selectedProfile, selectedTarget, selectedUniform, false);
            List<string> arguments = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", inputBox.Text, "-i", vocalsPath };
            string audioMap;
            if (selectedMusicMode == 2)
            {
                arguments.AddRange(new[] { "-i", instrumentalPath });
                string mixFilter = "[1:a]" + voiceFilter + "[voz];[2:a]volume=0.15[musica];[voz][musica]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.95:attack=5:release=100[final]";
                arguments.AddRange(new[] { "-filter_complex", mixFilter });
                audioMap = "[final]";
            }
            else
            {
                arguments.AddRange(new[] { "-filter_complex", "[1:a]" + voiceFilter + "[final]" });
                audioMap = "[final]";
            }
            arguments.AddRange(new[]
            {
                "-map", "0:v:0?", "-map", audioMap, "-map_metadata", "0", "-c:v", "copy",
                "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-movflags", "+faststart",
                "-progress", NewProgressFile(), "-nostats", outputBox.Text
            });
            SetProgress(80);
            progressPercent = 80;
            SetStatus("Etapa 3/3: finalizando o video...", "Processing");
            StartChildProcess(ffmpegPath, arguments.ToArray(), false);
        }

        private string NewProgressFile()
        {
            CleanupProgressFile();
            progressFile = Path.Combine(Path.GetTempPath(), "vfl_progresso_" + Guid.NewGuid().ToString("N") + ".txt");
            return progressFile;
        }

        private void StartChildProcess(string fileName, IEnumerable<string> arguments, bool captureOutput)
        {
            processErrors.Clear();
            ProcessStartInfo info = CreateProcessInfo(fileName, arguments, captureOutput, true);
            process = new Process { StartInfo = info };
            process.ErrorDataReceived += delegate(object errorSender, DataReceivedEventArgs errorEvent)
            {
                if (errorEvent.Data == null) return;
                lock (processErrors) processErrors.AppendLine(errorEvent.Data);
            };
            if (captureOutput)
            {
                process.OutputDataReceived += delegate(object outputSender, DataReceivedEventArgs outputEvent)
                {
                    if (outputEvent.Data == null) return;
                    lock (processErrors) processErrors.AppendLine(outputEvent.Data);
                };
            }
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                if (captureOutput) process.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                if (process != null) process.Dispose();
                process = null;
                FinishWithError(ex.Message);
            }
        }

        private bool TryReadDuration(string input, out double parsedDuration, out string error)
        {
            parsedDuration = 0;
            error = "";
            ProcessStartInfo info = CreateProcessInfo(ffprobePath, new[]
            {
                "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", input
            }, true, true);
            try
            {
                using (Process probe = Process.Start(info))
                {
                    string output = probe.StandardOutput.ReadToEnd().Trim();
                    error = probe.StandardError.ReadToEnd().Trim();
                    probe.WaitForExit();
                    return probe.ExitCode == 0 && Double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDuration);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (process != null && !process.HasExited)
            {
                if (processingStage == "Separate")
                {
                    int estimated = 15 + (int)Math.Min(60, stopwatch.Elapsed.TotalSeconds / Math.Max(30, duration * 1.5) * 60);
                    progressPercent = Math.Max(progressPercent, estimated);
                    SetProgress(progressPercent);
                    timeLabel.Text = "Tempo " + FormatTime(stopwatch.Elapsed) + "  |  IA trabalhando...";
                }
                else
                {
                    UpdateTimeDisplay();
                    int latest = ReadProgressPercent();
                    if (latest >= 0)
                    {
                        if (processingStage == "Extract") progressPercent = (int)Math.Round(latest * 0.15);
                        else if (processingStage == "Mix") progressPercent = 80 + (int)Math.Round(latest * 0.20);
                        else progressPercent = latest;
                        SetProgress(progressPercent);
                        if (processingStage == "Direct") SetStatus("Processando audio... " + latest + "%", "Processing");
                    }
                }
                return;
            }
            if (process == null) return;

            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Dispose();
            process = null;
            CleanupProgressFile();

            string errorText;
            lock (processErrors) errorText = processErrors.ToString();
            if (cancelRequested)
            {
                timer.Stop();
                if (stopwatch != null) stopwatch.Stop();
                SetBusy(false);
                CleanupTempWorkDir();
                SetStatus("Processamento cancelado.", "Cancel");
                return;
            }

            if (exitCode != 0)
            {
                string[] lines = errorText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string tail = String.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 12)).ToArray());
                FinishWithError(String.IsNullOrWhiteSpace(tail) ? "Nao foi possivel processar o video." : tail);
                return;
            }

            if (processingStage == "Extract")
            {
                StartAiSeparation();
                return;
            }
            if (processingStage == "Separate")
            {
                if (!File.Exists(vocalsPath) || !File.Exists(instrumentalPath))
                {
                    FinishWithError("A IA terminou, mas nao gerou as duas faixas de audio esperadas.");
                    return;
                }
                StartAiMix();
                return;
            }

            FinishSuccess();
        }

        private void FinishSuccess()
        {
            timer.Stop();
            if (stopwatch != null)
            {
                stopwatch.Stop();
                timeLabel.Text = "Tempo total  " + FormatTime(stopwatch.Elapsed);
            }
            SetBusy(false);
            SetProgress(100);
            CleanupTempWorkDir();
            SetStatus("Concluido: " + Path.GetFileName(outputBox.Text), "Success");
            MessageBox.Show(this, "Video salvo em:\n" + outputBox.Text, "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void FinishWithError(string message)
        {
            timer.Stop();
            if (stopwatch != null) stopwatch.Stop();
            CleanupProgressFile();
            CleanupTempWorkDir();
            SetBusy(false);
            MessageBox.Show(this, message, "Erro no processamento", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Nao foi possivel processar o video.", "Error");
        }

        private int ReadProgressPercent()
        {
            if (String.IsNullOrWhiteSpace(progressFile) || !File.Exists(progressFile) || duration <= 0) return -1;
            try
            {
                string lastValue = null;
                using (FileStream stream = new FileStream(progressFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        if (line.StartsWith("out_time_ms=", StringComparison.Ordinal)) lastValue = line.Substring(12);
                }
                long microseconds;
                if (!Int64.TryParse(lastValue, out microseconds)) return -1;
                return Math.Min(99, Math.Max(0, (int)Math.Round((microseconds / 1000000.0) / duration * 100.0)));
            }
            catch (IOException) { return -1; }
        }

        private void UpdateTimeDisplay()
        {
            if (stopwatch == null) return;
            string elapsed = FormatTime(stopwatch.Elapsed);
            if (progressPercent > 0)
            {
                double remainingSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds * ((100.0 / progressPercent) - 1.0));
                timeLabel.Text = "Tempo " + elapsed + "  |  Restante ~" + FormatTime(TimeSpan.FromSeconds(remainingSeconds));
            }
            else timeLabel.Text = "Tempo " + elapsed + "  |  Calculando...";
        }

        private void CancelProcessing(object sender, EventArgs e)
        {
            if (process == null || process.HasExited) return;
            cancelRequested = true;
            SetStatus("Cancelando...", "Processing");
            try { process.Kill(); } catch { }
        }

        private void ResetApp()
        {
            inputBox.Clear();
            outputBox.Clear();
            profileBox.SelectedIndex = 1;
            lufsBox.SelectedIndex = 1;
            musicModeBox.SelectedIndex = 0;
            uniformBox.Checked = true;
            duration = 0;
            progressPercent = 0;
            stopwatch = null;
            timeLabel.Text = "Tempo  00:00:00";
            SetProgress(0);
            SetStatus("Selecione um video para comecar.", "Idle");
            inputBox.Focus();
        }

        private void SetBusy(bool busy)
        {
            startButton.Enabled = !busy;
            clearButton.Enabled = !busy;
            inputButton.Enabled = !busy;
            outputButton.Enabled = !busy;
            inputBox.ReadOnly = busy;
            outputBox.ReadOnly = busy;
            profileBox.Enabled = !busy;
            lufsBox.Enabled = !busy;
            musicModeBox.Enabled = !busy;
            uniformBox.Enabled = !busy;
            cancelButton.Enabled = busy;
        }

        private void SetProgress(int value)
        {
            int safe = Math.Max(0, Math.Min(100, value));
            progressFill.Width = (int)Math.Round(progressTrack.ClientSize.Width * (safe / 100.0));
        }

        private void SetStatus(string text, string state)
        {
            statusLabel.Text = text;
            if (state == "Ready") SetStatusColors(colorAccent, "PRONTO");
            else if (state == "Processing") SetStatusColors(colorOrange, "PROCESSANDO");
            else if (state == "Success") SetStatusColors(colorGreen, "CONCLUIDO");
            else if (state == "Error") SetStatusColors(colorAccent, "ERRO");
            else if (state == "Cancel") SetStatusColors(colorOrange, "CANCELADO");
            else SetStatusColors(colorMuted, "AGUARDANDO");
        }

        private void SetStatusColors(Color color, string badge)
        {
            statusDot.BackColor = color;
            statusBadge.ForeColor = color;
            statusBadge.Text = badge;
        }

        private string GetAudioFilter(string profile, int target, bool uniform, bool preserveMusic)
        {
            int nr = 12, nf = -35, gain = 12, threshold = -20;
            string ratio = "3.0";
            if (profile == "Leve") { nr = 8; nf = -38; gain = 8; threshold = -18; ratio = "2.2"; }
            else if (profile == "Forte") { nr = 18; nf = -32; gain = 16; threshold = -23; ratio = "4.0"; }
            if (preserveMusic)
            {
                int musicNr = profile == "Leve" ? 3 : (profile == "Forte" ? 8 : 5);
                int musicNf = profile == "Leve" ? -48 : (profile == "Forte" ? -38 : -43);
                return String.Join(",", new[]
                {
                    "highpass=f=45",
                    "lowpass=f=18000",
                    "afftdn=nr=" + musicNr + ":nf=" + musicNf + ":tn=1",
                    "acompressor=threshold=-14dB:ratio=1.6:attack=35:release=550:makeup=1dB:knee=6:detection=rms",
                    "loudnorm=I=" + target + ":TP=-1.5:LRA=11",
                    "alimiter=limit=0.95:attack=5:release=100"
                });
            }
            string leveler = uniform
                ? "dynaudnorm=f=100:g=3:p=0.80:m=20:r=0.10:s=4:t=0.01:o=0.5"
                : "dynaudnorm=f=250:g=15:p=0.90:m=" + gain;
            string compressor = uniform
                ? "acompressor=threshold=-22dB:ratio=5:attack=10:release=250:makeup=2.5dB:knee=4:detection=rms"
                : "acompressor=threshold=" + threshold + "dB:ratio=" + ratio + ":attack=15:release=220:makeup=2dB";
            string loudnessRange = uniform ? "5" : "7";
            return String.Join(",", new[]
            {
                "highpass=f=75",
                "lowpass=f=14000",
                "afftdn=nr=" + nr + ":nf=" + nf + ":tn=1",
                leveler,
                compressor,
                "loudnorm=I=" + target + ":TP=-1.5:LRA=" + loudnessRange,
                "alimiter=limit=0.95:attack=5:release=50"
            });
        }

        private ProcessStartInfo CreateProcessInfo(string fileName, IEnumerable<string> arguments, bool redirectOutput, bool redirectError)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = String.Join(" ", arguments.Select(QuoteArgument).ToArray()),
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectError
            };
        }

        private static string QuoteArgument(string value)
        {
            if (value == null || value.Length == 0) return "\"\"";
            if (!value.Any(c => Char.IsWhiteSpace(c) || c == '"')) return value;
            StringBuilder result = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }
                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(c);
            }
            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static string FormatTime(TimeSpan time)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }

        private void CleanupProgressFile()
        {
            if (!String.IsNullOrWhiteSpace(progressFile))
            {
                try { if (File.Exists(progressFile)) File.Delete(progressFile); } catch { }
            }
            progressFile = null;
        }

        private void CleanupTempWorkDir()
        {
            if (!String.IsNullOrWhiteSpace(tempWorkDir))
            {
                try
                {
                    string resolved = Path.GetFullPath(tempWorkDir);
                    string tempRoot = Path.GetFullPath(Path.GetTempPath());
                    if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(resolved).StartsWith("vfl_voz_uniforme_", StringComparison.OrdinalIgnoreCase) &&
                        Directory.Exists(resolved))
                        Directory.Delete(resolved, true);
                }
                catch { }
            }
            tempWorkDir = null;
            extractedAudioPath = null;
            vocalsPath = null;
            instrumentalPath = null;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Stop();
            if (process != null && !process.HasExited)
            {
                try { process.Kill(); } catch { }
            }
            CleanupProgressFile();
            CleanupTempWorkDir();
        }
    }
}
