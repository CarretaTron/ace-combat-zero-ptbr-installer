using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Ac0PtBrInstaller
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 2 && args[0] == "--verificar-pacote")
                {
                    InstallerEngine.WriteJson(Path.GetFullPath(args[1]), InstallerEngine.VerifyPackage(null));
                    return 0;
                }
                if (args.Length == 2 && (args[0] == "--previa-janela" || args[0] == "--previa-sucesso" ||
                    args[0] == "--previa-diretorio-invalido" || args[0] == "--previa-pasta"))
                {
                    const string example = @"C:\SteamLibrary\steamapps\common\ACE COMBAT ZERO THE BELKAN WAR (Steam Version)\ACZero";
                    Form preview;
                    if (args[0] == "--previa-diretorio-invalido")
                        preview = new InvalidDirectoryDialog();
                    else
                    {
                        InstallerForm main = new InstallerForm();
                        if (args[0] == "--previa-pasta" || args[0] == "--previa-sucesso")
                            main.SetPreviewDirectory(example);
                        if (args[0] == "--previa-sucesso") main.ShowInstallationSuccess();
                        preview = main;
                    }
                    using (Form form = preview)
                    {
                        // Render only our own controls, with no visible desktop window.
                        form.Opacity = 0;
                        form.ShowInTaskbar = false;
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-32000, -32000);
                        form.Show();
                        form.PerformLayout();
                        Application.DoEvents();
                        using (Bitmap image = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height));
                            image.Save(Path.GetFullPath(args[1]), ImageFormat.Png);
                        }
                        form.Hide();
                    }
                    return 0;
                }
                if (args.Length != 0) throw new InstallerException("Opção de execução inválida. Abra o instalador com dois cliques.");
                Application.Run(new InstallerForm());
                return 0;
            }
            catch (Exception error)
            {
                if (args.Length != 0)
                {
                    try { InstallerEngine.WriteJson(Path.GetFullPath(args[1]), new { ok = false, error = error.Message }); }
                    catch { }
                }
                else MessageBox.Show(error.Message, "Tradução PT-BR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
