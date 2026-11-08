using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Tools > Options > TcFormatter > Formatting. Shows every <see cref="FormatterOptions"/> property with
    /// its category and description and a before / after example of the selected option. Edits the
    /// .tcformat.json of the open solution, or the IDE settings (%APPDATA%\TcFormatter\.tcformat.json)
    /// when there is none (see <see cref="SettingsTarget"/>).
    /// </summary>
    [ComVisible(true)]
    [Guid("2f6d8a1b-3c4e-4f50-8a9b-0c1d2e3f4a5b")]
    public sealed class FormatOptionsPage : DialogPage
    {
        private FormatterOptions _options = new FormatterOptions();
        private OptionPreviewControl _control;
        private readonly LoadedSettings _loaded = new LoadedSettings();

        public override object AutomationObject => _options;

        protected override IWin32Window Window
        {
            get
            {
                if (_control == null)
                {
                    _control = new OptionPreviewControl(option => FormatterOptionExamples.Preview(option, _options));
                    Show();
                }
                return _control;
            }
        }

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            if (_loaded.IsOutdated())
                LoadSettingsFromStorage();
            else
                Show();
        }

        public override void LoadSettingsFromStorage()
        {
            _options = _loaded.Load(config => config.Format) ?? new FormatterOptions();
            Show();
        }

        public override void SaveSettingsToStorage() => _loaded.Save(config => config.Format = _options);

        public override void ResetSettings()
        {
            _options = new FormatterOptions();
            Show();
        }

        private void Show()
        {
            _control?.SetSource(_loaded.Caption, _loaded.Error != null);
            _control?.Bind(_options);
        }
    }

    /// <summary>Which settings file an options page shows, and whether it changed since (shared logic of both pages).</summary>
    internal sealed class LoadedSettings
    {
        private SettingsTarget _target;
        private DateTime _stamp;

        /// <summary>Why the file could not be read; the page then does not save (it would overwrite the file).</summary>
        public string Error { get; private set; }

        public string Caption => _target == null
            ? string.Empty
            : Error == null ? _target.Caption : $"Cannot read {_target.File}: {Error} Changes are not saved.";

        /// <summary>Another solution / file is in effect, or the file was changed outside of the options page.</summary>
        public bool IsOutdated()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var now = SettingsTarget.Resolve();
            return _target == null || !string.Equals(now.File, _target.File, StringComparison.OrdinalIgnoreCase) || now.Stamp != _stamp;
        }

        public T Load<T>(Func<TcFormatConfig, T> part) where T : class
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _target = SettingsTarget.Resolve();
            _stamp = _target.Stamp;
            try
            {
                Error = null;
                return part(_target.Load());
            }
            catch (Exception ex) when (ex is ConfigException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Error = ex.Message;
                return null;
            }
        }

        public void Save(Action<TcFormatConfig> apply)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_target == null || Error != null)
                return;
            // read again: the other page may have saved its part of the same file in the meantime
            var config = _target.Load();
            apply(config);
            config.SaveTo(_target.File);
            _stamp = _target.Stamp;
        }
    }
}
