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
    /// <summary>Tools > Options > TcFormatter > Naming.</summary>
    [ComVisible(true)]
    [Guid("6a7b8c9d-0e1f-4a2b-9c3d-4e5f6a7b8c9d")]
    public sealed class NamingOptionsPage : DialogPage
    {
        private NamingRulesView _view = new NamingRulesView(new NamingRules());

        public override object AutomationObject => _view;

        public override void LoadSettingsFromStorage() => _view = new NamingRulesView(TcFormatConfig.LoadUser(out _).Naming);

        public override void SaveSettingsToStorage()
        {
            var config = TcFormatConfig.LoadUser(out _);
            config.Naming = _view.Rules;
            config.SaveTo(TcFormatConfig.UserConfigFile);
        }

        public override void ResetSettings() => _view = new NamingRulesView(new NamingRules());
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
        [Description("One 'RULE = Severity' per line. TCN001 object prefix, TCN002 variable prefix, TCN003 method/property name, TCN004 enum value, TCN005 invalid identifier. Severity: Off, Info, Warning, Error.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
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
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string IgnoreNames
        {
            get => string.Join(Environment.NewLine, Rules.IgnoreNames);
            set => Rules.IgnoreNames = Lines(value).ToList();
        }

        [Category("1 General"), DisplayName("Checked sections"), Description("Variable sections that are checked, one per line (VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_GLOBAL, VAR_TEMP, VAR_STAT, VAR_INST, STRUCT).")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string CheckedSections
        {
            get => string.Join(Environment.NewLine, Rules.CheckedSections);
            set => Rules.CheckedSections = Lines(value).ToList();
        }

        [Category("2 Variables"), DisplayName("Type prefixes")]
        [Description("One 'TYPE = prefix' per line. '*' is a wildcard (FB_* = fb), alternatives are separated by ',' (INT = n,i). Unknown types are not checked.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string TypePrefixes
        {
            get => Table(Rules.TypePrefixes);
            set => Rules.TypePrefixes = ToDictionary(value);
        }

        [Category("2 Variables"), DisplayName("Scope prefixes")]
        [Description("One 'SECTION = prefix' per line, required before the type prefix, e.g. 'VAR_GLOBAL = g' gives gbStop.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
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
        [Description("One 'KIND = prefix' per line. Kinds: FUNCTION_BLOCK, FUNCTION, PROGRAM, INTERFACE, STRUCT, ENUM, UNION, ALIAS, GVL, METHOD, PROPERTY, ACTION. Empty prefix = PascalCase without prefix.")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
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
