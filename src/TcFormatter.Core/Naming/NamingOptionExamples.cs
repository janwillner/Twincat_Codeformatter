using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TcFormatter.Formatting;

namespace TcFormatter.Naming
{
    /// <summary>
    /// A small declaration example for every <see cref="NamingRules"/> property. "After" is the example with the
    /// names the real naming check suggests, followed by its findings, so the preview is correct for every value.
    /// </summary>
    public static class NamingOptionExamples
    {
        private sealed class Example
        {
            public string Code;
            public (string Label, Action<NamingRules> Change)[] Alternatives;
        }

        private static readonly Dictionary<string, Example> Examples = new Dictionary<string, Example>(StringComparer.OrdinalIgnoreCase);

        static NamingOptionExamples()
        {
            const string speed = "VAR\nSpeed : LREAL;\nbStart : BOOL;\nEND_VAR";

            // ---------------------------------------------------------------- general
            Add(nameof(NamingRules.Enabled), speed);
            Add(nameof(NamingRules.Severities), speed,
                Alt("severities: { \"TCN002\": \"Error\" }", r => r.Severities[NamingRuleIds.VariablePrefix] = NamingSeverity.Error),
                Alt("severities: { \"TCN002\": \"Off\" }", r => r.Severities[NamingRuleIds.VariablePrefix] = NamingSeverity.Off));
            Add(nameof(NamingRules.DefaultSeverity), speed);
            Add(nameof(NamingRules.IgnoreNames), speed, Alt("ignoreNames: [ \"^MAIN$\", \"^Speed$\" ]", r => r.IgnoreNames.Add("^Speed$")));
            Add(nameof(NamingRules.CheckedSections), "VAR_TEMP\nSpeed : LREAL;\nEND_VAR",
                Alt("checkedSections without VAR_TEMP", r => r.CheckedSections.RemoveAll(s => s.Equals("VAR_TEMP", StringComparison.OrdinalIgnoreCase))));

            // ---------------------------------------------------------------- variables
            Add(nameof(NamingRules.TypePrefixes), "VAR\nSpeed : LREAL;\nCounter : INT;\nfbTimer : TON;\nEND_VAR",
                Alt("typePrefixes: { \"LREAL\": \"lr\" }", r => r.TypePrefixes["LREAL"] = "lr"));
            Add(nameof(NamingRules.ArrayPrefix), "VAR\nValues : ARRAY[1..3] OF INT;\nEND_VAR", Alt("arrayPrefix: \"arr\"", r => r.ArrayPrefix = "arr"));
            Add(nameof(NamingRules.ArrayIncludesElementPrefix), "VAR\naValues : ARRAY[1..3] OF INT;\nEND_VAR");
            Add(nameof(NamingRules.PointerPrefix), "VAR\nData : POINTER TO INT;\nEND_VAR", Alt("pointerPrefix: \"ptr\"", r => r.PointerPrefix = "ptr"));
            Add(nameof(NamingRules.PointerIncludesTargetPrefix), "VAR\npData : POINTER TO INT;\nEND_VAR");
            Add(nameof(NamingRules.ReferencePrefix), "VAR\nAxis : REFERENCE TO ST_Axis;\nEND_VAR", Alt("referencePrefix: \"r\"", r => r.ReferencePrefix = "r"));
            Add(nameof(NamingRules.ReferenceIncludesTargetPrefix), "VAR\nrefAxis : REFERENCE TO ST_Axis;\nEND_VAR");
            Add(nameof(NamingRules.ConstantPrefix), "VAR CONSTANT\ncMaxCount : INT := 10;\nEND_VAR", Alt("constantPrefix: \"\"", r => r.ConstantPrefix = ""));
            Add(nameof(NamingRules.ConstantNameCase), "VAR CONSTANT\nnMaxCount : INT := 10;\nEND_VAR");
            Add(nameof(NamingRules.MethodLocalMarker), "METHOD Calculate : BOOL\nVAR\nfAngle : LREAL;\nEND_VAR",
                Alt("methodLocalMarker: \"Temp\"", r => r.MethodLocalMarker = "Temp"));
            Add(nameof(NamingRules.ScopePrefixes), "VAR_GLOBAL\nbStop : BOOL;\nEND_VAR", Alt("scopePrefixes: { \"VAR_GLOBAL\": \"g\" }", r => r.ScopePrefixes["VAR_GLOBAL"] = "g"));

            // ---------------------------------------------------------------- objects
            Add(nameof(NamingRules.ObjectPrefixes), "FUNCTION_BLOCK Motor\nVAR\nbOn : BOOL;\nEND_VAR",
                Alt("objectPrefixes: { \"FUNCTION_BLOCK\": \"FB\" }", r => r.ObjectPrefixes["FUNCTION_BLOCK"] = "FB"));
            Add(nameof(NamingRules.RequirePascalCaseAfterPrefix), "FUNCTION_BLOCK FB_motor\nVAR\nbOn : BOOL;\nEND_VAR");
            Add(nameof(NamingRules.AllowUnderscoreInMemberNames), "METHOD Start_Axis : BOOL\nVAR_INPUT\nbExecute : BOOL;\nEND_VAR");
            Add(nameof(NamingRules.EnumValueCase), "TYPE E_Mode :\n(\nidle,\nRUNNING,\nAuto\n);\nEND_TYPE");
            Add(nameof(NamingRules.AllowLeadingUnderscore), "VAR\n_bBusy : BOOL;\nEND_VAR");
        }

        private static (string, Action<NamingRules>) Alt(string label, Action<NamingRules> change) => (label, change);

        private static void Add(string option, string code, params (string Label, Action<NamingRules> Change)[] alternatives) =>
            Examples[option] = new Example { Code = code, Alternatives = alternatives };

        public static bool Has(string option) => option != null && Examples.ContainsKey(option);

        /// <summary>Checks the example of <paramref name="option"/> with <paramref name="current"/>.</summary>
        public static OptionPreview Preview(string option, NamingRules current)
        {
            if (!Has(option))
                return null;
            var example = Examples[option];
            var rules = (current ?? new NamingRules()).Clone();
            var diagnostics = NamingChecker.Check(example.Code, rules)
                .OrderBy(d => d.Line).ThenBy(d => d.Column).ToList();

            var after = new StringBuilder(Rename(example.Code, diagnostics));
            after.Append('\n').Append('\n');
            if (diagnostics.Count == 0)
                after.Append("// no findings");
            foreach (var d in diagnostics)
                after.Append($"// {d.RuleId} {d.Severity}: {d.Message}").Append('\n');

            return new OptionPreview(
                FormatterOptionExamples.Visualize(example.Code, 4, false),
                FormatterOptionExamples.Visualize(after.ToString().TrimEnd('\n'), 4, false));
        }

        /// <summary>Copies of <paramref name="current"/> with other values of <paramref name="option"/>.</summary>
        public static IEnumerable<NamingRules> Alternatives(string option, NamingRules current) =>
            DescribeAlternatives(option, current).Select(a => a.Rules);

        /// <summary>Like <see cref="Alternatives"/>, with a short text of the changed setting.</summary>
        public static IEnumerable<(string Label, NamingRules Rules)> DescribeAlternatives(string option, NamingRules current)
        {
            var property = typeof(NamingRules).GetProperty(option, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
                yield break;

            if (Examples.TryGetValue(option, out var example) && example.Alternatives.Length > 0)
            {
                foreach (var (label, change) in example.Alternatives)
                {
                    var copy = current.Clone();
                    change(copy);
                    yield return (label, copy);
                }
                yield break;
            }

            object value = property.GetValue(current);
            IEnumerable<object> others;
            if (property.PropertyType == typeof(bool))
                others = new object[] { !(bool)value };
            else if (property.PropertyType.IsEnum)
                others = Enum.GetValues(property.PropertyType).Cast<object>().Where(v => !v.Equals(value));
            else
                yield break;
            foreach (var other in others)
            {
                var copy = current.Clone();
                property.SetValue(copy, other);
                yield return ($"{option} = {other}", copy);
            }
        }

        /// <summary>The description shown in the settings (from the [Description] attribute).</summary>
        public static string Description(string option) =>
            typeof(NamingRules).GetProperty(option, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?
                .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

        /// <summary>All options that have an example.</summary>
        public static IEnumerable<string> Options =>
            typeof(NamingRules).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Where(Has);

        /// <summary>Replaces every reported name that has a suggestion with the suggested name.</summary>
        private static string Rename(string code, IReadOnlyList<NamingDiagnostic> diagnostics)
        {
            var lines = code.Split('\n');
            foreach (var d in diagnostics.Where(d => !string.IsNullOrEmpty(d.Suggestion))
                         .OrderByDescending(d => d.Line).ThenByDescending(d => d.Column))
            {
                if (d.Line < 1 || d.Line > lines.Length)
                    continue;
                string line = lines[d.Line - 1];
                int start = d.Column - 1;
                if (start >= 0 && start + d.Name.Length <= line.Length && string.Compare(line, start, d.Name, 0, d.Name.Length, StringComparison.Ordinal) == 0)
                    lines[d.Line - 1] = line.Substring(0, start) + d.Suggestion + line.Substring(start + d.Name.Length);
            }
            return string.Join("\n", lines);
        }
    }
}
