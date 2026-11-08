using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;
using TcFormatter.Naming;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Tools > Options > TcFormatter > Naming. Edits the same file as the Formatting page
    /// (see <see cref="SettingsTarget"/>).
    /// </summary>
    [ComVisible(true)]
    [Guid("6a7b8c9d-0e1f-4a2b-9c3d-4e5f6a7b8c9d")]
    public sealed class NamingOptionsPage : DialogPage
    {
        private NamingRulesView _view = new NamingRulesView(new NamingRules());
        private OptionPreviewControl _control;
        private readonly LoadedSettings _loaded = new LoadedSettings();

        public override object AutomationObject => _view;

        protected override System.Windows.Forms.IWin32Window Window
        {
            get
            {
                if (_control == null)
                {
                    _control = new OptionPreviewControl(option => NamingOptionExamples.Preview(option, _view.Rules));
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
            _view = new NamingRulesView(_loaded.Load(config => config.Naming) ?? new NamingRules());
            Show();
        }

        public override void SaveSettingsToStorage() => _loaded.Save(config => config.Naming = _view.Rules);

        public override void ResetSettings()
        {
            _view = new NamingRulesView(new NamingRules());
            Show();
        }

        private void Show()
        {
            _control?.SetSource(_loaded.Caption, _loaded.Error != null);
            _control?.Bind(_view);
        }
    }

    /// <summary>
    /// Property grid view of <see cref="NamingRules"/>. Tables and lists are edited as multi-line text,
    /// one "KEY = value" entry per line.
    /// </summary>
    public sealed class NamingRulesView
    {
        public NamingRulesView(NamingRules rules) => Rules = rules;

        [Browsable(false)]
        public NamingRules Rules { get; }

        [Category("1 General"), DisplayName("Enabled"), Description("Enable the naming check.")]
        public bool Enabled { get => Rules.Enabled; set => Rules.Enabled = value; }

        [Category("1 General"), DisplayName("Default severity"), Description("Severity of rules without an entry in 'Severities'.")]
        public NamingSeverity DefaultSeverity { get => Rules.DefaultSeverity; set => Rules.DefaultSeverity = value; }

        [Category("1 General"), DisplayName("Severities")]
        [Description("One 'RULE = Severity' per line. TCN001 object prefix, TCN002 variable prefix, TCN003 '_' in method/property/action names, TCN004 enum value, TCN005 invalid identifier. Severity: Off, Info, Warning, Error.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string Severities
        {
            get => string.Join(Environment.NewLine, Rules.Severities.Select(p => p.Key + " = " + p.Value));
            set
            {
                var map = new Dictionary<string, NamingSeverity>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, val) in ParseTable(value))
                {
                    if (Enum.TryParse(val, true, out NamingSeverity severity))
                        map[key] = severity;
                }
                Rules.Severities = map;
            }
        }

        [Category("1 General"), DisplayName("Ignored names"), Description("Regular expressions of names that are never reported, one per line.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string IgnoreNames
        {
            get => string.Join(Environment.NewLine, Rules.IgnoreNames);
            set => Rules.IgnoreNames = Lines(value).ToList();
        }

        [Category("1 General"), DisplayName("Checked sections"), Description("Variable sections that are checked, one per line (VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_GLOBAL, VAR_TEMP, VAR_STAT, VAR_INST, STRUCT).")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string CheckedSections
        {
            get => string.Join(Environment.NewLine, Rules.CheckedSections);
            set => Rules.CheckedSections = Lines(value).ToList();
        }

        [Category("2 Variables"), DisplayName("Type prefixes")]
        [Description("One 'TYPE = prefix' per line. '*' is a wildcard (FB_* = fb), alternatives are separated by ',' (INT = n,i). Unknown types and empty prefixes are not checked.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string TypePrefixes
        {
            get => Table(Rules.TypePrefixes);
            set => Rules.TypePrefixes = ToDictionary(value);
        }

        [Category("2 Variables"), DisplayName("Scope prefixes")]
        [Description("One 'SECTION = prefix' per line, required before the type prefix, e.g. 'VAR_GLOBAL = g' gives gbStop.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string ScopePrefixes
        {
            get => Table(Rules.ScopePrefixes);
            set => Rules.ScopePrefixes = ToDictionary(value);
        }

        [Category("2 Variables"), DisplayName("Array prefix")]
        public string ArrayPrefix { get => Rules.ArrayPrefix; set => Rules.ArrayPrefix = value ?? string.Empty; }

        [Category("2 Variables"), DisplayName("Array prefix includes element prefix"), Description("ARRAY OF BOOL -> ab")]
        public bool ArrayIncludesElementPrefix { get => Rules.ArrayIncludesElementPrefix; set => Rules.ArrayIncludesElementPrefix = value; }

        [Category("2 Variables"), DisplayName("Pointer prefix")]
        public string PointerPrefix { get => Rules.PointerPrefix; set => Rules.PointerPrefix = value ?? string.Empty; }

        [Category("2 Variables"), DisplayName("Pointer prefix includes target prefix"), Description("POINTER TO INT -> pn")]
        public bool PointerIncludesTargetPrefix { get => Rules.PointerIncludesTargetPrefix; set => Rules.PointerIncludesTargetPrefix = value; }

        [Category("2 Variables"), DisplayName("Reference prefix")]
        public string ReferencePrefix { get => Rules.ReferencePrefix; set => Rules.ReferencePrefix = value ?? string.Empty; }

        [Category("2 Variables"), DisplayName("Reference prefix includes target prefix")]
        public bool ReferenceIncludesTargetPrefix { get => Rules.ReferenceIncludesTargetPrefix; set => Rules.ReferenceIncludesTargetPrefix = value; }

        [Category("2 Variables"), DisplayName("Constant prefix"), Description("Additional accepted prefix for constants (VAR CONSTANT). Empty = none.")]
        public string ConstantPrefix { get => Rules.ConstantPrefix; set => Rules.ConstantPrefix = value ?? string.Empty; }

        [Category("3 Objects"), DisplayName("Object prefixes")]
        [Description("One 'KIND = prefix' per line. Kinds: FUNCTION_BLOCK, FUNCTION, PROGRAM, INTERFACE, STRUCT, ENUM, UNION, ALIAS, GVL, METHOD, PROPERTY, ACTION. Empty prefix = not checked.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor)), TypeConverter(typeof(MultilineListConverter))]
        public string ObjectPrefixes
        {
            get => Table(Rules.ObjectPrefixes);
            set => Rules.ObjectPrefixes = ToDictionary(value);
        }

        [Category("3 Objects"), DisplayName("PascalCase after prefix"), Description("The character after a prefix must be an upper case letter or digit.")]
        public bool RequirePascalCaseAfterPrefix { get => Rules.RequirePascalCaseAfterPrefix; set => Rules.RequirePascalCaseAfterPrefix = value; }

        [Category("3 Objects"), DisplayName("Allow '_' in method/property/action names")]
        public bool AllowUnderscoreInMemberNames { get => Rules.AllowUnderscoreInMemberNames; set => Rules.AllowUnderscoreInMemberNames = value; }

        [Category("3 Objects"), DisplayName("Enum value casing"), Description("Any, PascalCase or UpperCase (INIT, OPEN_GEAR).")]
        public NameCase EnumValueCase { get => Rules.EnumValueCase; set => Rules.EnumValueCase = value; }

        [Category("3 Objects"), DisplayName("Allow leading '_'"), Description("Allow names starting with '_' (e.g. object prefixes _FB_ / _FC_).")]
        public bool AllowLeadingUnderscore { get => Rules.AllowLeadingUnderscore; set => Rules.AllowLeadingUnderscore = value; }

        [Category("2 Variables"), DisplayName("Constant name casing"), Description("Any, PascalCase or UpperCase after the prefix (dn_MAX_LIGHT_CYCLES).")]
        public NameCase ConstantNameCase { get => Rules.ConstantNameCase; set => Rules.ConstantNameCase = value; }

        [Category("2 Variables"), DisplayName("Method local marker"), Description("Text required after the type prefix of method locals, e.g. 'Temp' -> lr_TempAngle. Empty = none.")]
        public string MethodLocalMarker { get => Rules.MethodLocalMarker; set => Rules.MethodLocalMarker = value ?? string.Empty; }

        private static string Table(Dictionary<string, string> map) =>
            string.Join(Environment.NewLine, map.Select(p => p.Key + " = " + p.Value));

        private static Dictionary<string, string> ToDictionary(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, val) in ParseTable(text))
                map[key] = val;
            return map;
        }

        private static IEnumerable<(string Key, string Value)> ParseTable(string text)
        {
            foreach (var line in Lines(text))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                yield return (line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
        }

        private static IEnumerable<string> Lines(string text) =>
            (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0);
    }
}

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// Shows a multi-line table in a single grid cell as "A = 1; B = 2" and accepts that form when the cell is
    /// edited directly (the "..." button opens the multi-line editor).
    /// </summary>
    public sealed class MultilineListConverter : StringConverter
    {
        private const string Separator = "; ";

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is string text)
                return string.Join(Separator, text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string text && !text.Contains("\n"))
                return string.Join(Environment.NewLine, text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            return base.ConvertFrom(context, culture, value);
        }
    }
}
