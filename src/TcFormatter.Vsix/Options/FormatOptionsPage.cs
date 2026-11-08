using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Tools > Options > TcFormatter > Formatting. Shows every <see cref="FormatterOptions"/> property with
    /// its category and description. Stored in %APPDATA%\TcFormatter\.tcformat.json, which is used whenever
    /// no project .tcformat.json applies (and is read by the tcfmt CLI as well).
    /// </summary>
    [ComVisible(true)]
    [Guid("2f6d8a1b-3c4e-4f50-8a9b-0c1d2e3f4a5b")]
    public sealed class FormatOptionsPage : DialogPage
    {
        private FormatterOptions _options = new FormatterOptions();

        public override object AutomationObject => _options;

        public override void LoadSettingsFromStorage() => _options = TcFormatConfig.LoadUser(out _).Format;

        public override void SaveSettingsToStorage()
        {
            var config = TcFormatConfig.LoadUser(out _);
            config.Format = _options;
            config.SaveTo(TcFormatConfig.UserConfigFile);
        }

        public override void ResetSettings() => _options = new FormatterOptions();
    }
}
