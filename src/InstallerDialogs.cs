using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ac0PtBrInstaller
{
    internal sealed class DirectoryTextBox : TextBox
    {
        public DirectoryTextBox()
        {
            ForeColor = Color.FromArgb(160, 160, 160);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            ForeColor = TextLength == 0 ? Color.FromArgb(160, 160, 160) : SystemColors.WindowText;
            base.OnTextChanged(e);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string text);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // A native cue banner never becomes the field's actual value.
            SendMessage(Handle, 0x1501, new IntPtr(1),
                @"Ex.: C:\SteamLibrary\steamapps\common\ACE COMBAT ZERO THE BELKAN WAR (Steam Version)\ACZero");
        }
    }

    internal class InstallerDialog : Form
    {
        protected readonly TableLayoutPanel ContentLayout;

        protected InstallerDialog(string title, int height)
        {
            Text = title;
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 10f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(700, height);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(30, 26, 28);
            ForeColor = Color.FromArgb(242, 229, 220);
            ContentLayout = new TableLayoutPanel();
            ContentLayout.Dock = DockStyle.Fill;
            ContentLayout.Padding = new Padding(24);
            ContentLayout.ColumnCount = 1;
            ContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(ContentLayout);
        }

        protected void AddText(string text, float size, bool bold, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = color;
            label.Margin = new Padding(0, 0, 0, 14);
            AddRow(label, SizeType.AutoSize, 0);
        }

        protected void AddPath(string title, string path)
        {
            AddText(title, 10f, true, ForeColor);
            TextBox field = new TextBox();
            field.Text = path;
            field.ReadOnly = true;
            field.Multiline = false;
            field.WordWrap = false;
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(0, 0, 0, 18);
            field.AccessibleName = title;
            field.BackColor = Color.FromArgb(46, 39, 40);
            field.ForeColor = ForeColor;
            field.BorderStyle = BorderStyle.FixedSingle;
            field.SelectionStart = 0;
            AddRow(field, SizeType.AutoSize, 0);
        }

        protected void AddCloseButton(string caption)
        {
            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Fill;
            AddRow(spacer, SizeType.Percent, 100);
            Button button = new Button();
            button.Text = caption;
            button.Anchor = AnchorStyles.Right;
            button.Size = new Size(126, 40);
            button.Margin = Padding.Empty;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(193, 52, 30);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
            button.DialogResult = DialogResult.OK;
            AddRow(button, SizeType.Absolute, 40);
            AcceptButton = button;
            CancelButton = button;
        }

        private void AddRow(Control control, SizeType type, float height)
        {
            int row = ContentLayout.RowCount++;
            ContentLayout.RowStyles.Add(new RowStyle(type, height));
            ContentLayout.Controls.Add(control, 0, row);
        }
    }

    internal sealed class InvalidDirectoryDialog : InstallerDialog
    {
        public InvalidDirectoryDialog() : base("Diretório inválido", 260)
        {
            AddText("Diretório inválido", 16f, true, Color.FromArgb(255, 155, 137));
            AddText("A pasta selecionada não parece ser uma instalação válida de Ace Combat Zero: The Belkan War.\r\n\r\nSelecione a pasta ACZero dentro do diretório do jogo.", 10.5f, false, ForeColor);
            AddCloseButton("Entendi");
        }
    }

}
