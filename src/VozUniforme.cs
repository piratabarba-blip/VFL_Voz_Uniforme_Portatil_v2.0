using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("VFL Voz Uniforme")]
[assembly: AssemblyDescription("Separacao por IA, limpeza e nivelamento de audio para videos")]
[assembly: AssemblyCompany("VFL")]
[assembly: AssemblyProduct("VFL Voz Uniforme")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace VozUniformeApp
{
    internal static class Program
    {
        private const string AppUserModelId = "VFL.VozUniforme";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [STAThread]
        private static void Main()
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color colorWindow = Color.FromArgb(14, 17, 23);
        private readonly Color colorHeader = Color.FromArgb(18, 22, 30);
        private readonly Color colorCard = Color.FromArgb(25, 31, 43);
        private readonly Color colorSurface = Color.FromArgb(20, 25, 35);
        private readonly Color colorInput = Color.FromArgb(35, 43, 59);
        private readonly Color colorSecondary = Color.FromArgb(40, 49, 67);
        private readonly Color colorBorder = Color.FromArgb(48, 59, 78);
        private readonly Color colorText = Color.FromArgb(248, 250, 252);
        private readonly Color colorMuted = Color.FromArgb(148, 163, 184);
        private readonly Color colorAccent = Color.FromArgb(16, 185, 129);
        private readonly Color colorCyan = Color.FromArgb(6, 182, 212);
        private readonly Color colorGreen = Color.FromArgb(16, 185, 129);
        private readonly Color colorDanger = Color.FromArgb(239, 68, 68);
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
            Text = "VFL Voz Uniforme — Áudio com IA acelerada";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(1180, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = colorWindow;
            ForeColor = colorText;
            Font = new Font("Segoe UI", 10f);

            Panel header = new Panel { Location = new Point(0, 0), Size = new Size(1180, 78), BackColor = colorHeader };
            Controls.Add(header);
            string logoPath = Path.Combine(projectDir, "assets", "vfl-suite-logo.png");
            if (File.Exists(logoPath))
                header.Controls.Add(new PictureBox { Location = new Point(22, 13), Size = new Size(48, 48), Image = Image.FromFile(logoPath), SizeMode = PictureBoxSizeMode.Zoom });
            else
            {
                Panel logo = new Panel { Location = new Point(22, 13), Size = new Size(48, 48), BackColor = colorAccent };
                logo.Controls.Add(new Label { Text = "V", Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
                RoundControl(logo, 8);
                header.Controls.Add(logo);
            }
            header.Controls.Add(MakeLabel("VFL Voz Uniforme", new Point(84, 10), new Size(430, 30), colorText, 14f, FontStyle.Bold));
            header.Controls.Add(MakeLabel("Separe voz e música com IA, limpe as falas e preserve o vídeo.", new Point(84, 40), new Size(650, 22), colorMuted, 8.5f, FontStyle.Regular));
            Label version = MakeLabel("ÁUDIO COM IA  •  GPU AUTO  |  v2.1", new Point(850, 25), new Size(290, 24), colorCyan, 7.5f, FontStyle.Bold);
            version.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(version);
            header.Controls.Add(new Panel { Location = new Point(0, 76), Size = new Size(1180, 2), BackColor = colorAccent });

            Panel left = MakeCard(new Rectangle(18, 94, 326, 548));
            Controls.Add(left);
            left.Controls.Add(MakeLabel("◆  ENTRADA E DESTINO", new Point(22, 18), new Size(280, 24), colorText, 9f, FontStyle.Bold));
            left.Controls.Add(MakeLabel("Escolha o vídeo original e onde salvar o resultado.", new Point(22, 44), new Size(280, 34), colorMuted, 8f, FontStyle.Regular));
            left.Controls.Add(MakeLabel("VÍDEO DE ENTRADA", new Point(22, 94), new Size(280, 20), colorMuted, 7.5f, FontStyle.Bold));
            inputBox = new TextBox { Location = new Point(22, 117), Size = new Size(204, 29), BackColor = colorInput, ForeColor = colorText, BorderStyle = BorderStyle.FixedSingle };
            RoundControl(inputBox, 6);
            inputButton = MakeButton("PROCURAR", new Point(234, 114), 70, colorSecondary); inputButton.Height = 35; inputButton.Font = new Font("Segoe UI Semibold", 7.5f); left.Controls.Add(inputBox); left.Controls.Add(inputButton);
            left.Controls.Add(MakeLabel("SALVAR RESULTADO EM", new Point(22, 164), new Size(280, 20), colorMuted, 7.5f, FontStyle.Bold));
            outputBox = new TextBox { Location = new Point(22, 187), Size = new Size(204, 29), BackColor = colorInput, ForeColor = colorText, BorderStyle = BorderStyle.FixedSingle };
            RoundControl(outputBox, 6);
            outputButton = MakeButton("PROCURAR", new Point(234, 184), 70, colorSecondary); outputButton.Height = 35; outputButton.Font = new Font("Segoe UI Semibold", 7.5f); left.Controls.Add(outputBox); left.Controls.Add(outputButton);
            left.Controls.Add(new Panel { Location = new Point(22, 242), Size = new Size(282, 1), BackColor = colorBorder });
            left.Controls.Add(MakeLabel("AJUSTES DE ÁUDIO", new Point(22, 262), new Size(280, 20), colorText, 8f, FontStyle.Bold));
            left.Controls.Add(MakeLabel("INTENSIDADE DA LIMPEZA", new Point(22, 300), new Size(280, 20), colorMuted, 7.5f, FontStyle.Bold));
            profileBox = MakeCombo(new Point(22, 323), new Size(282, 31), new[] { "Leve", "Normal", "Forte" }, 1); left.Controls.Add(profileBox);
            left.Controls.Add(MakeLabel("VOLUME FINAL", new Point(22, 376), new Size(280, 20), colorMuted, 7.5f, FontStyle.Bold));
            lufsBox = MakeCombo(new Point(22, 399), new Size(282, 31), new[] { "-14 LUFS - YouTube", "-16 LUFS - Voz/Podcast", "-18 LUFS - Suave" }, 1); left.Controls.Add(lufsBox);
            Panel safeNote = new Panel { Location = new Point(22, 462), Size = new Size(282, 62), BackColor = colorSurface };
            RoundControl(safeNote, 10);
            safeNote.Controls.Add(MakeLabel("✓  ORIGINAL PRESERVADO", new Point(12, 9), new Size(255, 18), colorGreen, 7.5f, FontStyle.Bold));
            safeNote.Controls.Add(MakeLabel("As escolhas são aplicadas somente ao novo arquivo.", new Point(12, 30), new Size(255, 24), colorMuted, 7.5f, FontStyle.Regular));
            left.Controls.Add(safeNote);

            Panel center = MakeCard(new Rectangle(356, 94, 492, 548));
            Controls.Add(center);
            center.Controls.Add(MakeLabel("PROCESSAMENTO DE ÁUDIO", new Point(24, 18), new Size(300, 24), colorText, 9f, FontStyle.Bold));
            center.Controls.Add(MakeLabel("Acompanhe a limpeza, a IA e a remontagem em tempo real.", new Point(24, 44), new Size(440, 24), colorMuted, 8f, FontStyle.Regular));
            Panel stage = new Panel { Location = new Point(24, 88), Size = new Size(444, 310), BackColor = colorSurface };
            RoundControl(stage, 14);
            stage.Controls.Add(MakeLabel("IA", new Point(189, 34), new Size(66, 28), colorAccent, 16f, FontStyle.Bold));
            Label centerMessage = MakeLabel("PROCESSAMENTO LOCAL", new Point(40, 82), new Size(364, 36), colorCyan, 14f, FontStyle.Bold); centerMessage.TextAlign = ContentAlignment.MiddleCenter; stage.Controls.Add(centerMessage);
            Label centerHelp = MakeLabel("Separação, limpeza, nivelamento e remontagem acontecem inteiramente neste computador.", new Point(54, 126), new Size(336, 62), colorMuted, 9f, FontStyle.Regular); centerHelp.TextAlign = ContentAlignment.MiddleCenter; stage.Controls.Add(centerHelp);
            stage.Controls.Add(MakeLabel("PROGRESSO", new Point(24, 218), new Size(130, 20), colorMuted, 7.5f, FontStyle.Bold));
            timeLabel = MakeLabel("Tempo  00:00:00", new Point(210, 214), new Size(210, 24), colorMuted, 8.5f, FontStyle.Bold); timeLabel.TextAlign = ContentAlignment.MiddleRight; stage.Controls.Add(timeLabel);
            progressTrack = new Panel { Location = new Point(24, 252), Size = new Size(396, 10), BackColor = colorInput };
            progressFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 10), BackColor = colorGreen }; progressTrack.Controls.Add(progressFill); center.Controls.Add(progressTrack);
            RoundControl(progressTrack, 5);
            stage.Controls.Add(progressTrack);
            center.Controls.Add(stage);
            Label privacy = MakeLabel("🔒  Nenhum arquivo é enviado para a internet.", new Point(24, 430), new Size(444, 30), colorGreen, 8.5f, FontStyle.Bold); privacy.TextAlign = ContentAlignment.MiddleCenter; center.Controls.Add(privacy);
            Label ready = MakeLabel("Selecione um vídeo para iniciar o tratamento.", new Point(24, 468), new Size(444, 36), colorMuted, 8.5f, FontStyle.Regular); ready.TextAlign = ContentAlignment.MiddleCenter; center.Controls.Add(ready);

            Panel right = MakeCard(new Rectangle(860, 94, 302, 548));
            Controls.Add(right);
            right.Controls.Add(MakeLabel("↗  TRATAMENTO FINAL", new Point(20, 18), new Size(262, 24), colorText, 9f, FontStyle.Bold));
            right.Controls.Add(MakeLabel("Defina como a voz e a música serão entregues.", new Point(20, 44), new Size(262, 36), colorMuted, 8f, FontStyle.Regular));
            right.Controls.Add(MakeLabel("MÚSICA DE FUNDO", new Point(20, 98), new Size(262, 20), colorMuted, 7.5f, FontStyle.Bold));
            musicModeBox = MakeCombo(new Point(20, 121), new Size(262, 31), new[]
            {
                "Vídeo somente com voz",
                "Manter música (suave)",
                "Deixar música baixa — IA",
                "Remover música — IA"
            }, 0);
            musicModeBox.SelectedIndexChanged += delegate
            {
                if (process != null) return;
                bool aiMode = musicModeBox.SelectedIndex >= 2;
                if (aiMode) uniformBox.Checked = true;
                SetStatus(aiMode ? "Modo IA: a voz e a musica serao separadas." : "Modo de audio selecionado.", "Ready");
            };
            right.Controls.Add(musicModeBox);

            uniformBox = new CheckBox
            {
                Text = "Uniformizar todas as vozes",
                Location = new Point(20, 178),
                Size = new Size(262, 28),
                Checked = true,
                BackColor = colorCard,
                ForeColor = colorText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            uniformBox.FlatAppearance.CheckedBackColor = colorAccent;
            right.Controls.Add(uniformBox);
            Panel aiNote = new Panel { Location = new Point(20, 226), Size = new Size(262, 112), BackColor = colorSurface };
            RoundControl(aiNote, 10);
            aiNote.Controls.Add(MakeLabel("ACELERAÇÃO INTELIGENTE", new Point(14, 12), new Size(234, 20), colorCyan, 7.5f, FontStyle.Bold));
            aiNote.Controls.Add(MakeLabel("A GPU NVIDIA é usada automaticamente. Sem GPU compatível, o processamento continua pela CPU.", new Point(14, 38), new Size(234, 62), colorMuted, 8f, FontStyle.Regular));
            right.Controls.Add(aiNote);
            startButton = MakeButton("MELHORAR ÁUDIO", new Point(20, 438), 262, colorGreen); startButton.Height = 44; right.Controls.Add(startButton);
            clearButton = MakeButton("LIMPAR / PRÓXIMO", new Point(20, 494), 262, colorSecondary); clearButton.Height = 36; right.Controls.Add(clearButton);

            Panel footer = new Panel { Location = new Point(18, 654), Size = new Size(1144, 48), BackColor = colorHeader }; RoundControl(footer, 10); Controls.Add(footer);
            statusDot = new Panel { Location = new Point(18, 20), Size = new Size(9, 9), BackColor = colorMuted }; RoundControl(statusDot, 5); footer.Controls.Add(statusDot);
            statusLabel = MakeLabel("Selecione um vídeo para começar.", new Point(38, 11), new Size(620, 27), colorMuted, 8.5f, FontStyle.Regular); footer.Controls.Add(statusLabel);
            statusBadge = MakeLabel("AGUARDANDO", new Point(650, 11), new Size(120, 27), colorMuted, 7.5f, FontStyle.Bold); statusBadge.TextAlign = ContentAlignment.MiddleRight; footer.Controls.Add(statusBadge);
            cancelButton = MakeButton("CANCELAR", new Point(790, 7), 112, colorSecondary); cancelButton.Height = 34; cancelButton.Enabled = false; footer.Controls.Add(cancelButton);
            Label original = MakeLabel("ORIGINAL PRESERVADO", new Point(920, 11), new Size(205, 27), colorMuted, 7.5f, FontStyle.Bold); original.TextAlign = ContentAlignment.MiddleRight; footer.Controls.Add(original);

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

        private Panel MakeCard(Rectangle bounds)
        {
            Panel card = new Panel { Bounds = bounds, BackColor = colorCard };
            RoundControl(card, 12);
            return card;
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
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            RoundControl(button, 10);
            return button;
        }

        private static void RoundControl(Control control, int radius)
        {
            Action update = delegate
            {
                if (control.Width < 2 || control.Height < 2) return;
                int diameter = radius * 2;
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddArc(0, 0, diameter, diameter, 180, 90);
                    path.AddArc(control.Width - diameter - 1, 0, diameter, diameter, 270, 90);
                    path.AddArc(control.Width - diameter - 1, control.Height - diameter - 1, diameter, diameter, 0, 90);
                    path.AddArc(0, control.Height - diameter - 1, diameter, diameter, 90, 90);
                    path.CloseFigure();
                    Region previous = control.Region;
                    control.Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            };
            update();
            control.SizeChanged += delegate { update(); };
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
            RoundControl(combo, 6);
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
            RoundControl(box, 6);
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
                HandleProcessLine(errorEvent.Data);
            };
            if (captureOutput)
            {
                process.OutputDataReceived += delegate(object outputSender, DataReceivedEventArgs outputEvent)
                {
                    if (outputEvent.Data == null) return;
                    HandleProcessLine(outputEvent.Data);
                };
            }
            try
            {
                process.Start();
                if (String.Equals(fileName, aiPythonPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                }
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

        private void HandleProcessLine(string line)
        {
            lock (processErrors) processErrors.AppendLine(line);
            if (processingStage != "Separate" || IsDisposed) return;

            string statusText = null;
            int parsedProgress = -1;
            if (line.StartsWith("VFL_DEVICE=", StringComparison.Ordinal))
            {
                statusText = "Etapa 2/3: " + line.Substring("VFL_DEVICE=".Length);
            }
            else
            {
                Match chunk = Regex.Match(line, @"Processing chunk\s+(\d+)/(\d+)", RegexOptions.IgnoreCase);
                int current;
                int total;
                if (chunk.Success && Int32.TryParse(chunk.Groups[1].Value, out current) &&
                    Int32.TryParse(chunk.Groups[2].Value, out total) && total > 0)
                {
                    parsedProgress = 15 + (int)Math.Round(((current - 1.0) / total) * 60.0);
                    statusText = "Etapa 2/3: separando com IA - bloco " + current + "/" + total;
                }
            }

            if (statusText == null) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (parsedProgress >= 0)
                    {
                        progressPercent = Math.Max(progressPercent, parsedProgress);
                        SetProgress(progressPercent);
                    }
                    SetStatus(statusText, "Processing");
                }));
            }
            catch { }
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
            SetStatus("Selecione um vídeo para começar.", "Idle");
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
            else if (state == "Error") SetStatusColors(colorDanger, "ERRO");
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
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = String.Join(" ", arguments.Select(QuoteArgument).ToArray()),
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectError
            };
            if (String.Equals(fileName, aiPythonPath, StringComparison.OrdinalIgnoreCase))
            {
                int balancedThreads = Math.Max(2, (int)Math.Floor(Environment.ProcessorCount * 0.80));
                info.EnvironmentVariables["OMP_NUM_THREADS"] = balancedThreads.ToString(CultureInfo.InvariantCulture);
                info.EnvironmentVariables["MKL_NUM_THREADS"] = balancedThreads.ToString(CultureInfo.InvariantCulture);
                info.EnvironmentVariables["NUMEXPR_MAX_THREADS"] = balancedThreads.ToString(CultureInfo.InvariantCulture);
                info.EnvironmentVariables["VFL_RESOURCE_PROFILE"] = "balanced";
            }
            return info;
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
