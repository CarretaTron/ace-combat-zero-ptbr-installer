using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class RedrawSmoke
{
    private static Bitmap Capture(Form form)
    {
        Application.DoEvents();
        Bitmap bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        return bitmap;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        string reportPath = Path.Combine(output, "resultado_redesenho.json");
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type type = assembly.GetType("Ac0PtBrInstaller.InstallerForm", true);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo cacheField = type.GetField("backgroundCache", flags);
            FieldInfo statusField = type.GetField("statusText", flags);
            MethodInfo progress = type.GetMethod("InstallProgressChanged", flags);
            MethodInfo busy = type.GetMethod("SetBusy", flags);
            using (Form form = (Form)Activator.CreateInstance(type))
            {
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                Label status = (Label)statusField.GetValue(form);
                int textChanges = 0;
                status.TextChanged += delegate { textChanges++; };
                Color[] background = new Color[4];
                Point[] points = { new Point(45, 420), new Point(150, 450),
                                   new Point(300, 480), new Point(400, 410) };
                using (Bitmap first = Capture(form))
                {
                    first.Save(Path.Combine(output, "inicio.png"), ImageFormat.Png);
                    for (int p = 0; p < points.Length; p++) background[p] = first.GetPixel(points[p].X, points[p].Y);
                }
                object cache = cacheField.GetValue(form);
                Check(cache != null, "O fundo nao foi preparado.");
                busy.Invoke(form, new object[] { true });
                int frames = 0;
                for (int phase = 0; phase < 5; phase++)
                {
                    for (int repeat = 0; repeat < 20; repeat++)
                    {
                        progress.Invoke(form, new object[] { null,
                            new ProgressChangedEventArgs(phase * 20 + repeat, "Conferindo etapa " + phase + "...") });
                        Application.DoEvents();
                    }
                    using (Bitmap frame = Capture(form))
                    {
                        for (int p = 0; p < points.Length; p++)
                            Check(frame.GetPixel(points[p].X, points[p].Y) == background[p], "O fundo mudou durante o progresso.");
                        Check(Object.ReferenceEquals(cache, cacheField.GetValue(form)), "O fundo foi recalculado sem mudar o tamanho.");
                        frames++;
                    }
                }
                Check(textChanges == 5, "Mensagens repetidas atualizaram o status.");
                busy.Invoke(form, new object[] { false });
                type.GetMethod("ShowInstallationSuccess", flags).Invoke(form, null);
                Check(status.Text.StartsWith("A tradução foi instalada na pasta do jogo"), "Mensagem de sucesso alterada.");
                Check(status.Text.Contains("Inglês (US)"), "Orientacao de idioma ausente.");
                Check(Application.OpenForms.Count == 1, "Foi aberta uma janela extra.");
                using (Bitmap success = Capture(form))
                    success.Save(Path.Combine(output, "sucesso.png"), ImageFormat.Png);
                Check(Object.ReferenceEquals(cache, cacheField.GetValue(form)), "O sucesso recriou o fundo.");
                form.ClientSize = new Size(900, 660);
                using (Bitmap resized = Capture(form))
                    resized.Save(Path.Combine(output, "redimensionado.png"), ImageFormat.Png);
                Bitmap newCache = (Bitmap)cacheField.GetValue(form);
                Check(!Object.ReferenceEquals(cache, newCache) && newCache.Size == form.ClientSize,
                    "O cache nao acompanhou o redimensionamento.");
                InstallerReport(reportPath, new { status = "passed", progress_events = 100,
                    distinct_status_updates = 5, unchanged_background_frames = frames,
                    background_reused_between_updates = true, cache_resized_correctly = true,
                    success_and_language_preserved = true, extra_success_dialogs = 0,
                    actual_game_installation = false });
                form.Hide();
            }
            return 0;
        }
        catch (Exception error)
        {
            InstallerReport(reportPath, new { status = "failed", error = error.ToString() });
            return 1;
        }
    }

    private static void InstallerReport(string path, object value)
    {
        File.WriteAllText(path, new JavaScriptSerializer().Serialize(value));
    }
}
