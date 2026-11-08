using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Tools > Options > TcFormatter > Formatting. Shows every <see cref="FormatterOptions"/> property with
    /// its category and description and a before / after example of the selected option. Stored in
    /// %APPDATA%\TcFormatter\.tcformat.json, which is used whenever no project .tcformat.json applies
    /// (and is read by the tcfmt CLI as well).
    /// </summary>
    [ComVisible(true)]
    [Guid("2f6d8a1b-3c4e-4f50-8a9b-0c1d2e3f4a5b")]
    public sealed class FormatOptionsPage : DialogPage
    {
        private FormatterOptions _options = new FormatterOptions();
        private OptionPreviewControl _control;

        public override object AutomationObject => _options;

        protected override IWin32Window Window
        {
            get
            {
                if (_control == null)
                {
                    _control = new OptionPreviewControl(option => FormatterOptionExamples.Preview(option, _options));
                    _control.Bind(_options);
                }
                return _control;
            }
        }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            _control?.Bind(_options);
        }

        public override void LoadSettingsFromStorage()
        {
            _options = TcFormatConfig.LoadUser(out _).Format;
            _control?.Bind(_options);
        }

        public override void SaveSettingsToStorage()
        {
            var config = TcFormatConfig.LoadUser(out _);
            config.Format = _options;
            config.SaveTo(TcFormatConfig.UserConfigFile);
        }

        public override void ResetSettings()
        {
            _options = new FormatterOptions();
            _control?.Bind(_options);
        }
    }
}
