using System.Collections.Generic;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;
using TcFormatter.Objects;
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

    /// <summary>Formats and / or checks one PLC object of the project tree with <see cref="ObjectProcessor"/>.</summary>
    internal static class PlcCodeProcessor
    {
        public static void Process(ITcSmTreeItem item, TcFormatConfig config, bool format, bool lint, ProcessSummary summary)
        {
            summary.Objects++;
            var obj = new TreeItemObject(item);
            var result = ObjectProcessor.Process(obj, config, format, lint);
            summary.Errors.AddRange(result.Errors);
            foreach (var finding in result.Naming)
                summary.Naming.Add(new NamingIssue(obj.Path, finding.PartLabel, finding.Diagnostic));
            if (result.Changed)
            {
                summary.Changed++;
                summary.ChangedObjects.Add(obj.Path);
            }
        }

        /// <summary>Adapter: an item of the TwinCAT project tree, read and written through the Automation Interface.</summary>
        private sealed class TreeItemObject : IPlcObject
        {
            private readonly ITcSmTreeItem _item;
            private int? _type;

            public TreeItemObject(ITcSmTreeItem item)
            {
                _item = item;
            }

            // COM access can throw on broken objects; ObjectProcessor reports that, so nothing is read in the constructor
            private int Type => _type ?? (_type = _item.ItemType).Value;

            public string Path => _item.PathName;

            public string Name => _item.Name;

            public PlcObjectKind Kind => PlcTree.KindOf(Type);

            public bool Has(CodePart part) =>
                part == CodePart.Declaration
                    ? PlcTree.HasDeclarationPart(Type)
                    : PlcTree.HasImplementationPart(Type) && PlcText.IsStructuredText(_item);

            public string Read(CodePart part) => PlcText.Read(_item, Property(part));

            public void Write(CodePart part, string text) => PlcText.Write(_item, Property(part), text);

            private static string Property(CodePart part) =>
                part == CodePart.Declaration ? PlcText.Declaration : PlcText.Implementation;
        }
    }
}
