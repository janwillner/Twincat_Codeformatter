using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;
using TCatSysManagerLib;

namespace TcFormatter.Vsix.TwinCat
{
    internal sealed class NamingIssue
    {
        public NamingIssue(string treePath, string part, NamingDiagnostic diagnostic)
        {
            TreePath = treePath;
            Part = part;
            Diagnostic = diagnostic;
        }

        public string TreePath { get; }
        public string Part { get; }
        public NamingDiagnostic Diagnostic { get; }
    }

    internal sealed class ProcessSummary
    {
        public int Objects;
        public int Changed;
        public List<string> Errors { get; } = new List<string>();
        public List<NamingIssue> Naming { get; } = new List<NamingIssue>();
        public List<string> ChangedObjects { get; } = new List<string>();
    }

    /// <summary>Formats and/or checks one PLC object through ITcPlcDeclaration / ITcPlcImplementation.</summary>
    internal static class PlcCodeProcessor
    {
        public static void Process(ITcSmTreeItem item, TcFormatConfig config, bool format, bool lint, ProcessSummary summary)
        {
            string path;
            string name;
            int type;
            try
            {
                path = item.PathName;
                name = item.Name;
                type = item.ItemType;
            }
            catch (COMException ex)
            {
                summary.Errors.Add("Cannot access tree item: " + ex.Message);
                return;
            }

            summary.Objects++;
            bool changed = false;

            if (item is ITcPlcDeclaration declaration)
            {
                string text = Read(() => declaration.DeclarationText, path, "declaration", summary);
                if (text != null)
                {
                    if (format)
                        text = FormatPart(text, CodePart.Declaration, config, path, summary, t => declaration.DeclarationText = t, ref changed);
                    if (lint && text != null)
                    {
                        foreach (var d in NamingChecker.Check(text, config.Naming, name, PlcTree.KindForNaming(type)))
                            summary.Naming.Add(new NamingIssue(path, "Declaration", d));
                    }
                }
            }

            if (item is ITcPlcImplementation implementation && PlcTree.IsStructuredText(implementation))
            {
                string text = Read(() => implementation.ImplementationText, path, "implementation", summary);
                if (text != null && format)
                    FormatPart(text, CodePart.Implementation, config, path, summary, t => implementation.ImplementationText = t, ref changed);
            }

            if (lint && type == PlcTree.Action)
            {
                foreach (var d in NamingChecker.CheckObjectName(name, "ACTION", config.Naming))
                    summary.Naming.Add(new NamingIssue(path, "Name", d));
            }

            if (changed)
            {
                summary.Changed++;
                summary.ChangedObjects.Add(path);
            }
        }

        private static string Read(Func<string> read, string path, string what, ProcessSummary summary)
        {
            try
            {
                return read() ?? string.Empty;
            }
            catch (COMException ex)
            {
                summary.Errors.Add($"{path}: cannot read {what}: {ex.Message}");
                return null;
            }
        }

        private static string FormatPart(string text, CodePart part, TcFormatConfig config, string path, ProcessSummary summary,
                                         Action<string> write, ref bool changed)
        {
            var result = StFormatter.Format(text, part, config.Format);
            if (result.Error != null)
            {
                summary.Errors.Add($"{path} ({part}): {result.Error}");
                return text;
            }
            if (!result.Changed)
                return text;
            try
            {
                write(result.Text);
                changed = true;
                return result.Text;
            }
            catch (COMException ex)
            {
                summary.Errors.Add($"{path} ({part}): cannot write (read-only?): {ex.Message}");
                return text;
            }
        }
    }
}
