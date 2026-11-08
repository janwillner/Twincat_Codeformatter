using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TcFormatter.Configuration
{
    /// <summary>Import and export of complete settings (IDE settings, project .tcformat.json, STweep exports).</summary>
    public static class SettingsTransfer
    {
        /// <summary>File dialog filter for <see cref="Import"/>.</summary>
        public const string ImportFilter =
            "All supported settings (*.json;*.stweep)|*.json;*.stweep|TcFormatter settings (*.tcformat.json;*.json)|*.json|STweep settings (*.stweep)|*.stweep|All files (*.*)|*.*";

        /// <summary>File dialog filter for <see cref="Export"/>.</summary>
        public const string ExportFilter = "TcFormatter settings (*.tcformat.json)|*.tcformat.json|JSON (*.json)|*.json";

        public static bool IsStweepFile(string file) =>
            string.Equals(Path.GetExtension(file), ".stweep", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads <paramref name="file"/>. A TcFormatter file (.tcformat.json / .json) replaces the format and naming
        /// settings; a STweep export (.stweep) only provides format settings, the naming rules of
        /// <paramref name="current"/> are kept. <paramref name="notes"/> lists warnings and settings that could
        /// not be taken over exactly.
        /// </summary>
        /// <exception cref="ConfigException">The file is no valid settings file.</exception>
        /// <exception cref="IOException">The file cannot be read.</exception>
        public static TcFormatConfig Import(string file, TcFormatConfig current, out List<string> notes)
        {
            string text = File.ReadAllText(file);
            if (IsStweepFile(file))
            {
                var stweep = StweepImport.Convert(text, out notes);
                var merged = TcFormatConfig.Parse(current.ToJson(includeDescriptions: false), out _);
                merged.Format = stweep.Format;
                return merged;
            }

            var config = TcFormatConfig.Parse(text, out var warnings);
            notes = warnings.ToList();
            return config;
        }

        /// <summary>Writes <paramref name="config"/> as a complete, commented .tcformat.json.</summary>
        public static void Export(TcFormatConfig config, string file) => config.SaveTo(file);
    }
}
