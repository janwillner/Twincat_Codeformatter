using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Configuration
{
    /// <summary>
    /// Contents of a <c>.tcformat.json</c> file: <c>{ "format": { ... }, "naming": { ... } }</c>.
    /// The file is looked up from the formatted object's directory upwards (like Black's pyproject.toml).
    /// </summary>
    public sealed class TcFormatConfig
    {
        public const string FileName = ".tcformat.json";

        public FormatterOptions Format { get; set; } = new FormatterOptions();

        public NamingRules Naming { get; set; } = new NamingRules();

        // ------------------------------------------------------------------ discovery

        /// <summary>Returns the nearest .tcformat.json in <paramref name="startDirectory"/> or one of its parents, or null.</summary>
        public static string FindFile(string startDirectory, string stopAt = null)
        {
            if (string.IsNullOrEmpty(startDirectory))
                return null;
            var dir = new DirectoryInfo(startDirectory);
            if (!dir.Exists && File.Exists(startDirectory))
                dir = new FileInfo(startDirectory).Directory;
            string stop = stopAt == null ? null : Path.GetFullPath(stopAt).TrimEnd('\\', '/');
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate))
                    return candidate;
                if (stop != null && string.Equals(dir.FullName.TrimEnd('\\', '/'), stop, StringComparison.OrdinalIgnoreCase))
                    return null;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// Per-user configuration (written by the IDE options pages). Used when no project file applies.
        /// </summary>
        public static string UserConfigFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TcFormatter", FileName);

        /// <summary>
        /// Loads the configuration that applies to <paramref name="startDirectory"/>: the nearest project
        /// .tcformat.json, otherwise the per-user file, otherwise the defaults.
        /// </summary>
        public static TcFormatConfig LoadFor(string startDirectory, out string sourceFile, out IReadOnlyList<string> warnings,
                                             string stopAt = null, bool includeUserConfig = true)
        {
            sourceFile = FindFile(startDirectory, stopAt);
            if (sourceFile == null && includeUserConfig && File.Exists(UserConfigFile))
                sourceFile = UserConfigFile;
            if (sourceFile == null)
            {
                warnings = Array.Empty<string>();
                return new TcFormatConfig();
            }
            return Load(sourceFile, out warnings);
        }

        /// <summary>Loads the per-user configuration or the defaults.</summary>
        public static TcFormatConfig LoadUser(out IReadOnlyList<string> warnings)
        {
            if (!File.Exists(UserConfigFile))
            {
                warnings = Array.Empty<string>();
                return new TcFormatConfig();
            }
            return Load(UserConfigFile, out warnings);
        }

        public void SaveTo(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, ToJson(includeDescriptions: true), new UTF8Encoding(false));
        }

        public static TcFormatConfig Load(string file, out IReadOnlyList<string> warnings)
        {
            var config = Parse(File.ReadAllText(file), out var list);
            warnings = list;
            return config;
        }

        // ------------------------------------------------------------------ parsing

        public static TcFormatConfig Parse(string json, out List<string> warnings)
        {
            warnings = new List<string>();
            var config = new TcFormatConfig();
            if (string.IsNullOrWhiteSpace(json))
                return config;

            if (!(Json.Parse(json) is List<KeyValuePair<string, object>> root))
                throw new ConfigException("Invalid .tcformat.json: the document must be an object.");

            foreach (var pair in root)
            {
                if (pair.Key.StartsWith("$", StringComparison.Ordinal))
                    continue; // $schema, $comment
                if (Is(pair.Key, "format"))
                    Apply(config.Format, pair.Value, "format", warnings);
                else if (Is(pair.Key, "naming"))
                    Apply(config.Naming, pair.Value, "naming", warnings);
                else
                    warnings.Add($"Unknown key '{pair.Key}' (expected 'format' or 'naming').");
            }
            return config;
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<PropertyInfo> Settings(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite);

        private static void Apply(object target, object value, string path, List<string> warnings)
        {
            if (!(value is List<KeyValuePair<string, object>> members))
            {
                warnings.Add($"'{path}' must be an object.");
                return;
            }
            var properties = Settings(target.GetType()).ToList();
            foreach (var member in members)
            {
                var property = properties.FirstOrDefault(p => Is(p.Name, member.Key));
                string key = path + "." + member.Key;
                if (property == null)
                {
                    warnings.Add($"Unknown setting '{key}'.");
                    continue;
                }
                try
                {
                    SetValue(target, property, member.Value, key, warnings);
                }
                catch (FormatException ex)
                {
                    warnings.Add($"Invalid value for '{key}': {ex.Message}");
                }
            }
        }

        private static void SetValue(object target, PropertyInfo property, object value, string key, List<string> warnings)
        {
            var type = property.PropertyType;
            if (type == typeof(bool))
            {
                property.SetValue(target, value is bool b ? b : throw new FormatException("expected true or false."));
            }
            else if (type == typeof(int))
            {
                property.SetValue(target, value is double d && d == Math.Floor(d) ? (int)d : throw new FormatException("expected an integer."));
            }
            else if (type == typeof(string))
            {
                property.SetValue(target, value == null ? null : value as string ?? throw new FormatException("expected a string."));
            }
            else if (type.IsEnum)
            {
                property.SetValue(target, ParseEnum(type, value));
            }
            else if (type == typeof(List<string>))
            {
                if (!(value is List<object> items))
                    throw new FormatException("expected an array of strings.");
                property.SetValue(target, items.Select(i => i as string ?? throw new FormatException("expected an array of strings.")).ToList());
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (!(value is List<KeyValuePair<string, object>> entries))
                    throw new FormatException("expected an object.");
                var dictionary = (IDictionary)property.GetValue(target);
                var valueType = type.GetGenericArguments()[1];
                foreach (var entry in entries)
                {
                    try
                    {
                        object converted = valueType.IsEnum ? ParseEnum(valueType, entry.Value)
                            : entry.Value as string ?? throw new FormatException("expected a string.");
                        dictionary[entry.Key] = converted;
                    }
                    catch (FormatException ex)
                    {
                        warnings.Add($"Invalid value for '{key}.{entry.Key}': {ex.Message}");
                    }
                }
            }
            else
            {
                throw new FormatException("unsupported setting type.");
            }
        }

        private static object ParseEnum(Type type, object value)
        {
            if (value is string s)
            {
                foreach (var name in Enum.GetNames(type))
                {
                    if (Is(name, s))
                        return Enum.Parse(type, name);
                }
            }
            throw new FormatException($"'{value}' is not one of {string.Join(", ", Enum.GetNames(type))}.");
        }

        // ------------------------------------------------------------------ writing

        public string ToJson(bool includeDescriptions = true)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            if (includeDescriptions)
                sb.Append("  // TcFormatter configuration. Applies to all TwinCAT objects in this folder and below.\n");
            WriteSection(sb, "format", Format, includeDescriptions, last: false);
            WriteSection(sb, "naming", Naming, includeDescriptions, last: true);
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void WriteSection(StringBuilder sb, string name, object section, bool descriptions, bool last)
        {
            sb.Append("  ").Append(Json.Quote(name)).Append(": {\n");
            var properties = Settings(section.GetType()).ToList();
            for (int i = 0; i < properties.Count; i++)
            {
                var p = properties[i];
                if (descriptions)
                {
                    string description = p.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    if (p.PropertyType.IsEnum)
                        description = (description == null ? "" : description + " ") + "Values: " + string.Join(" | ", Enum.GetNames(p.PropertyType)) + ".";
                    if (!string.IsNullOrEmpty(description))
                        sb.Append("    // ").Append(description).Append('\n');
                }
                sb.Append("    ").Append(Json.Quote(CamelCase(p.Name))).Append(": ");
                WriteValue(sb, p.GetValue(section), "    ");
                sb.Append(i == properties.Count - 1 ? "\n" : ",\n");
            }
            sb.Append("  }").Append(last ? "\n" : ",\n");
        }

        private static void WriteValue(StringBuilder sb, object value, string indent)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int n:
                    sb.Append(n.ToString(CultureInfo.InvariantCulture));
                    break;
                case string s:
                    sb.Append(Json.Quote(s));
                    break;
                case Enum e:
                    sb.Append(Json.Quote(e.ToString()));
                    break;
                case IDictionary dictionary:
                    if (dictionary.Count == 0)
                    {
                        sb.Append("{}");
                        break;
                    }
                    sb.Append("{\n");
                    int i = 0;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        sb.Append(indent).Append("  ").Append(Json.Quote((string)entry.Key)).Append(": ");
                        WriteValue(sb, entry.Value, indent + "  ");
                        sb.Append(++i == dictionary.Count ? "\n" : ",\n");
                    }
                    sb.Append(indent).Append('}');
                    break;
                case IList list:
                    sb.Append('[');
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (j > 0)
                            sb.Append(", ");
                        WriteValue(sb, list[j], indent);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append(Json.Quote(Convert.ToString(value, CultureInfo.InvariantCulture)));
                    break;
            }
        }

        private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
