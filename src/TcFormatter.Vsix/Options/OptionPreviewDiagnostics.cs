using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Vsix.Options
{
    /// <summary>Checks the options pages' example preview without opening the modal Tools > Options dialog.</summary>
    internal static class OptionPreviewDiagnostics
    {
        public static string Run(string directory, string formatOption, string namingOption)
        {
            var report = new StringBuilder();
            var config = TcFormatConfig.LoadUser(out _);

            var format = config.Format;
            Check(report, directory, "format", new OptionPreviewControl(o => FormatterOptionExamples.Preview(o, format)), format, formatOption);

            var naming = new NamingRulesView(config.Naming);
            Check(report, directory, "naming", new OptionPreviewControl(o => NamingOptionExamples.Preview(o, naming.Rules)), naming, namingOption);

            string file = Path.Combine(directory, "TcFormatter-options.txt");
            File.WriteAllText(file, report.ToString());
            return report.ToString();
        }

        private static void Check(StringBuilder report, string directory, string name, OptionPreviewControl control, object settings, string snapshotOption)
        {
            using (var form = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-3000, -3000),
                Size = new Size(900, 700),
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
            })
            {
                control.Dock = DockStyle.Fill;
                form.Controls.Add(control);
                form.Show();
                try
                {
                    control.Bind(settings);
                    Application.DoEvents();
                    var problems = control.SelfTest(snapshotOption);
                    Application.DoEvents();
                    report.AppendLine($"[{name}] {problems[0]}, {problems.Count - 1} problem(s)");
                    for (int i = 1; i < problems.Count; i++)
                        report.AppendLine("  " + problems[i]);

                    string png = Path.Combine(directory, $"TcFormatter-options-{name}.png");
                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                        bitmap.Save(png, ImageFormat.Png);
                    }
                    report.AppendLine("  screenshot: " + png);
                }
                catch (Exception ex)
                {
                    report.AppendLine($"[{name}] failed: {ex}");
                }
                finally
                {
                    form.Close();
                }
            }
        }
    }
}
