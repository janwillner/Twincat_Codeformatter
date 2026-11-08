using System;
using System.Collections.Generic;
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

    /// <summary>Formats and/or checks one PLC object (declaration and Structured Text implementation).</summary>
    internal static class PlcCodeProcessor
    {
        public static void Process(ITcSmTreeItem item, TcFormatConfig config, bool format, bool lint, ProcessSummary summary)
        {
            string path = "?";
            int type = -1;
            try
            {
                path = item.PathName;
                type = item.ItemType;
                ProcessCore(item, path, type, config, format, lint, summary);
            }
            catch (Exception ex)
            {
                // one broken object must never stop the whole run
                summary.Errors.Add($"{path} (item type {type}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ProcessCore(ITcSmTreeItem item, string path, int type, TcFormatConfig config, bool format, bool lint, ProcessSummary summary)
        {
            string name = item.Name;
            summary.Objects++;
            bool changed = false;

            if (PlcTree.HasDeclarationPart(type))
            {
                string text = PlcText.Read(item, PlcText.Declaration);
                if (format)
                    text = FormatPart(item, text, CodePart.Declaration, PlcText.Declaration, config, path, summary, ref changed);
                if (lint)
                {
                    foreach (var d in NamingChecker.Check(text, config.Naming, name, PlcTree.KindForNaming(type)))
                        summary.Naming.Add(new NamingIssue(path, "Declaration", d));
                }
            }

            if (format && PlcTree.HasImplementationPart(type) && PlcText.IsStructuredText(item))
            {
                string text = PlcText.Read(item, PlcText.Implementation);
                FormatPart(item, text, CodePart.Implementation, PlcText.Implementation, config, path, summary, ref changed);
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

        private static string FormatPart(ITcSmTreeItem item, string text, CodePart part, string property, TcFormatConfig config,
                                         string path, ProcessSummary summary, ref bool changed)
        {
            var result = StFormatter.Format(text, part, config.Format);
            if (result.Error != null)
            {
                summary.Errors.Add($"{path} ({part}): {result.Error}");
                return text;
            }
            if (!result.Changed)
                return text;
            PlcText.Write(item, property, result.Text);
            changed = true;
            return result.Text;
        }
    }
}
