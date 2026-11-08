using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace Ac0PtBrInstaller
{
    public sealed class InstallerForm : Form
    {
        private readonly TextBox folderText;
        private readonly Button folderButton;
        private readonly Button installButton;
        private readonly Button closeButton;
        private readonly Label statusText;
        private readonly BackgroundWorker worker;
        private readonly ToolTip toolTip;
        private readonly Image folderImage;
        private readonly Image bannerImage;
        private readonly Image titleImage;
        private readonly TableLayoutPanel mainLayout;
        private Bitmap backgroundCache;
        private bool installing;

        public InstallerForm()
        {
            Text = "Ace Combat Zero — Tradução PT-BR";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(820, 600);
            MinimumSize = new Size(760, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 20, 22);
            DoubleBuffered = true;
            bannerImage = LoadArtwork("AC0_PTBR.banner.jpg");
            titleImage = LoadArtwork("AC0_PTBR.titulo.png");
            MaximizeBox = false;

            TableLayoutPanel layout = new BufferedTableLayoutPanel();
            mainLayout = layout;
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.Padding = new Padding(24);
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            Controls.Add(layout);

            PictureBox title = new PictureBox();
            title.Image = titleImage;
            title.SizeMode = PictureBoxSizeMode.Zoom;
            title.Dock = DockStyle.Left;
            title.Width = 500;
            title.BackColor = Color.Transparent;
            title.AccessibleName = "Ace Combat Zero — The Belkan War";
            title.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(title, 0, 0);

            Label folderLabel = MakeLabel("Selecione a pasta ACZero no diretório de instalação do jogo\r\ne clique em Instalar tradução.", Color.White);
            folderLabel.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold, GraphicsUnit.Point);
            folderLabel.Margin = new Padding(0, 18, 0, 6);
            layout.Controls.Add(folderLabel, 0, 1);

            TableLayoutPanel folderRow = new BufferedTableLayoutPanel();
            folderRow.Dock = DockStyle.Fill;
            folderRow.BackColor = Color.Transparent;
            folderRow.Margin = Padding.Empty;
            folderRow.ColumnCount = 2;
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44f));
            folderRow.RowCount = 1;
            folderRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(folderRow, 0, 2);

            folderText = new DirectoryTextBox();
            folderText.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            folderText.Margin = new Padding(0, 0, 8, 0);
            folderText.AccessibleName = "Pasta do jogo";
            folderText.TabIndex = 0;
            folderRow.Controls.Add(folderText, 0, 0);

            folderImage = CreateFolderImage();
            folderButton = new Button();
            folderButton.Dock = DockStyle.Fill;
            folderButton.Margin = new Padding(0, 0, 0, 0);
            folderButton.Image = folderImage;
            folderButton.AccessibleName = "Escolher pasta do jogo";
            folderButton.TabIndex = 1;
            folderButton.UseVisualStyleBackColor = true;
            folderButton.Click += BrowseClicked;
            folderRow.Controls.Add(folderButton, 1, 0);

            toolTip = new ToolTip();
            toolTip.SetToolTip(folderButton, "Abrir o Explorador para escolher a pasta do jogo");

            folderText.TextChanged += delegate {
                statusText.Text = String.Empty;
            };

            statusText = new BufferedLabel();
            statusText.AutoSize = true;
            statusText.Dock = DockStyle.Top;
            statusText.Margin = new Padding(0, 12, 0, 12);
            statusText.BackColor = Color.Transparent;
            statusText.ForeColor = Color.FromArgb(242, 229, 220);
            statusText.TabStop = false;
            statusText.AccessibleName = "Status da instalação";
            statusText.Text = String.Empty;
            layout.Controls.Add(statusText, 0, 3);

            TableLayoutPanel buttons = new BufferedTableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.BackColor = Color.Transparent;
            buttons.Margin = Padding.Empty;
            buttons.ColumnCount = 3;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98f));
            buttons.RowCount = 1;
            buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(buttons, 0, 4);

            Label credits = MakeLabel("Tradução por Carreta", Color.White);
            credits.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point);
            credits.Dock = DockStyle.None;
            credits.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            credits.Margin = new Padding(4, 0, 8, 10);
            buttons.Controls.Add(credits, 0, 0);

            installButton = new Button();
            installButton.Dock = DockStyle.Fill;
            installButton.Text = "Instalar tradução";
            installButton.Margin = new Padding(0, 0, 10, 0);
            installButton.Font = new Font(Font, FontStyle.Bold);
            installButton.TabIndex = 2;
            installButton.UseVisualStyleBackColor = false;
            installButton.BackColor = Color.FromArgb(193, 52, 30);
            installButton.ForeColor = Color.White;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderSize = 0;
            installButton.Click += InstallClicked;
            buttons.Controls.Add(installButton, 1, 0);

            closeButton = new Button();
            closeButton.Dock = DockStyle.Fill;
            closeButton.Text = "Fechar";
            closeButton.Margin = Padding.Empty;
            closeButton.TabIndex = 3;
            closeButton.UseVisualStyleBackColor = false;
            closeButton.BackColor = Color.FromArgb(46, 39, 40);
            closeButton.ForeColor = Color.White;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(120, 101, 96);
            closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(closeButton, 2, 0);
            AcceptButton = installButton;
            CancelButton = closeButton;

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += InstallInBackground;
            worker.ProgressChanged += InstallProgressChanged;
            worker.RunWorkerCompleted += InstallCompleted;
        }

        private static Label MakeLabel(string text, Color color)
        {
            Label label = new BufferedLabel();
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            label.Dock = DockStyle.Fill;
            label.Text = text;
            label.ForeColor = color;
            label.Margin = Padding.Empty;
            return label;
        }

        internal void SetPreviewDirectory(string directory)
        {
            folderText.Text = directory;
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            try
            {
                string selected = FolderPicker.PickFolder(this, folderText.Text.Trim().Trim('"'));
                if (!String.IsNullOrWhiteSpace(selected))
                {
                    folderText.Text = selected;
                    string validated;
                    if (!TryResolveDirectory(out validated)) return;
                    folderText.Text = validated;
                    folderText.Focus();
                    folderText.SelectionStart = folderText.Text.Length;
                }
            }
            catch (Exception error)
            {
                ShowError("Não foi possível abrir o seletor de pasta. " + error.Message +
                    "\r\nVocê também pode digitar ou colar o caminho no campo.");
            }
        }

        private void InstallClicked(object sender, EventArgs e)
        {
            if (installing) return;
            string directory;
            if (!TryResolveDirectory(out directory)) return;
            try
            {
                folderText.Text = directory;
                installing = true;
                SetBusy(true);
                statusText.ForeColor = Color.FromArgb(242, 229, 220);
                statusText.Text = "Preparando a instalação...";
                worker.RunWorkerAsync(directory);
            }
            catch (Exception error)
            {
                installing = false;
                SetBusy(false);
                ShowError(error.Message);
            }
        }

        private void InstallInBackground(object sender, DoWorkEventArgs e)
        {
            string lastMessage = null;
            e.Result = InstallerEngine.Install((string)e.Argument, delegate(InstallProgress progress)
            {
                // The progress bar was removed: repeated percentages need no UI work.
                if (progress != null && !String.IsNullOrWhiteSpace(progress.Message) &&
                    !String.Equals(lastMessage, progress.Message, StringComparison.Ordinal))
                {
                    lastMessage = progress.Message;
                    worker.ReportProgress(Math.Max(0, Math.Min(100, progress.Percent)), progress.Message);
                }
            });
        }

        private void InstallProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string message = e.UserState as string;
            if (!String.IsNullOrWhiteSpace(message) && statusText.Text != message) statusText.Text = message;
        }

        private void InstallCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            installing = false;
            SetBusy(false);
            if (e.Error != null)
            {
                ShowError(e.Error.Message);
                return;
            }

            ShowInstallationSuccess();
        }

        internal void ShowInstallationSuccess()
        {
            statusText.ForeColor = Color.FromArgb(139, 225, 159);
            statusText.Text = "A tradução foi instalada na pasta do jogo" +
                "\r\n\r\nNo jogo, selecione o idioma Inglês (US) para usar a tradução.";
        }

        private bool TryResolveDirectory(out string directory)
        {
            directory = null;
            try
            {
                directory = InstallerEngine.ResolveGameDirectory(folderText.Text.Trim().Trim('"'));
                return true;
            }
            catch (Exception)
            {
                statusText.ForeColor = Color.FromArgb(255, 155, 137);
                statusText.Text = "Diretório inválido. Selecione a pasta ACZero dentro do diretório do jogo.";
                using (InvalidDirectoryDialog dialog = new InvalidDirectoryDialog())
                    dialog.ShowDialog(this);
                return false;
            }
        }

        private void SetBusy(bool busy)
        {
            SuspendLayout();
            mainLayout.SuspendLayout();
            try
            {
                folderText.Enabled = !busy;
                folderButton.Enabled = !busy;
                installButton.Enabled = !busy;
                closeButton.Enabled = !busy;
                installButton.Text = busy ? "Instalando..." : "Instalar tradução";
                UseWaitCursor = busy;
            }
            finally
            {
                mainLayout.ResumeLayout(false);
                ResumeLayout(false);
            }
        }

        private void ShowError(string message)
        {
            statusText.ForeColor = Color.FromArgb(255, 155, 137);
            statusText.Text = message;
            MessageBox.Show(this, message, "Não foi possível instalar", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (installing)
            {
                e.Cancel = true;
                statusText.Text = "A instalação está em andamento. Aguarde a conclusão para fechar.";
                SystemSounds.Beep.Play();
                return;
            }
            base.OnFormClosing(e);
        }

        private static Image LoadArtwork(string name)
        {
            using (System.IO.Stream stream = typeof(InstallerForm).Assembly.GetManifestResourceStream(name))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (bannerImage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                base.OnPaintBackground(e);
                return;
            }
            if (backgroundCache == null || backgroundCache.Size != ClientSize)
            {
                Bitmap next = new Bitmap(ClientSize.Width, ClientSize.Height);
                using (Graphics graphics = Graphics.FromImage(next))
                {
                    float scale = Math.Max((float)ClientSize.Width / bannerImage.Width,
                        (float)ClientSize.Height / bannerImage.Height);
                    float width = bannerImage.Width * scale;
                    float height = bannerImage.Height * scale;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(bannerImage, (ClientSize.Width - width) / 2f,
                        (ClientSize.Height - height) / 2f, width, height);
                    using (LinearGradientBrush shade = new LinearGradientBrush(ClientRectangle,
                        Color.FromArgb(125, 17, 12, 14), Color.FromArgb(235, 17, 12, 14), 90f))
                        graphics.FillRectangle(shade, ClientRectangle);
                }
                if (backgroundCache != null) backgroundCache.Dispose();
                backgroundCache = next;
            }
            e.Graphics.DrawImageUnscaled(backgroundCache, Point.Empty);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // Composite native input/buttons together with the transparent panels.
                parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return parameters;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (worker != null) worker.Dispose();
                if (toolTip != null) toolTip.Dispose();
                if (folderImage != null) folderImage.Dispose();
                if (bannerImage != null) bannerImage.Dispose();
                if (titleImage != null) titleImage.Dispose();
                if (backgroundCache != null) backgroundCache.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Image CreateFolderImage()
        {
            Bitmap image = new Bitmap(24, 24);
            using (Graphics graphics = Graphics.FromImage(image))
            using (SolidBrush back = new SolidBrush(Color.FromArgb(229, 168, 48)))
            using (SolidBrush front = new SolidBrush(Color.FromArgb(255, 205, 81)))
            using (Pen outline = new Pen(Color.FromArgb(160, 113, 31), 1f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] backPoints = new PointF[] {
                    new PointF(2, 6), new PointF(9, 6), new PointF(11, 8),
                    new PointF(22, 8), new PointF(22, 20), new PointF(2, 20)
                };
                graphics.FillPolygon(back, backPoints);
                graphics.DrawPolygon(outline, backPoints);
                PointF[] frontPoints = new PointF[] {
                    new PointF(2, 11), new PointF(22, 11),
                    new PointF(20, 20), new PointF(4, 20)
                };
                graphics.FillPolygon(front, frontPoints);
                graphics.DrawPolygon(outline, frontPoints);
            }
            return image;
        }
    }
}
