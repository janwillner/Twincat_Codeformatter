using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;
using TcFormatter.Objects;

namespace TcFormatter.TwinCatFiles
{
    public sealed class ObjectDiagnostic
    {
        public ObjectDiagnostic(string objectPath, string part, NamingDiagnostic diagnostic, int fileLine = 0, int fileColumn = 0)
        {
            ObjectPath = objectPath;
            Part = part;
            Diagnostic = diagnostic;
            FileLine = fileLine;
            FileColumn = fileColumn;
        }

        /// <summary>1-based line in the XML file (0 when unknown).</summary>
        public int FileLine { get; }

        /// <summary>1-based column in the XML file (0 when unknown).</summary>
        public int FileColumn { get; }

        /// <summary>Dotted object path inside the file, e.g. "FB_Motor.M_Start".</summary>
        public string ObjectPath { get; }

        /// <summary>"Declaration", "Implementation" or "Name" (the object name itself, e.g. of an action).</summary>
        public string Part { get; }

        public NamingDiagnostic Diagnostic { get; }
    }

    public sealed class TcFileResult
    {
        public string Text { get; internal set; }
        public bool Changed { get; internal set; }
        public int SectionsFormatted { get; internal set; }
        public List<string> Errors { get; } = new List<string>();
        public List<ObjectDiagnostic> Naming { get; } = new List<ObjectDiagnostic>();
    }

    /// <summary>
    /// Formats / lints the Structured Text inside TwinCAT XML files (.TcPOU, .TcDUT, .TcGVL, .TcIO).
    /// Only the contents of the CDATA sections of &lt;Declaration&gt; and &lt;ST&gt; elements are replaced;
    /// every other byte of the file (GUIDs, attributes, LineIds, whitespace) stays as it is.
    /// </summary>
    public static class TcFileProcessor
    {
        public static readonly string[] Extensions = { ".TcPOU", ".TcDUT", ".TcGVL", ".TcIO" };

        private static readonly Regex Tag = new Regex(@"<(/?)([A-Za-z_][\w.\-]*)([^>]*?)(/?)>", RegexOptions.Compiled);
        private static readonly Regex NameAttribute = new Regex("\\bName\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex CdataRun = new Regex(@"\G\s*<!\[CDATA\[(.*?)\]\]>", RegexOptions.Compiled | RegexOptions.Singleline);

        private sealed class Element
        {
            public string Tag;
            public string Name;
        }

        private sealed class Section
        {
            public int Start;     // index of first character of the content (first "<![CDATA[")
            public int End;       // index after the last "]]>"
            public string Code;
            public CodePart Part;
            public Element Owner;
            public string ObjectPath;
        }

        public static TcFileResult Process(string xml, TcFormatConfig config, bool format, bool lint)
        {
            xml = xml ?? string.Empty;
            var result = new TcFileResult { Text = xml };
            var objects = FindSections(xml)
                .GroupBy(s => s.ObjectPath)
                .Select(g => new XmlObject(g.ToList()))
                .ToList();

            var findings = new List<(XmlObject Object, ObjectFinding Finding)>();
            foreach (var obj in objects)
            {
                var processed = ObjectProcessor.Process(obj, config, format, lint);
                result.Errors.AddRange(processed.Errors);
                findings.AddRange(processed.Naming.Select(f => (obj, f)));
            }

            var replacements = objects.SelectMany(o => o.Replacements).ToList();
            if (replacements.Count > 0)
            {
                var sb = new StringBuilder(xml);
                foreach (var (section, text) in replacements.OrderByDescending(r => r.Section.Start))
                {
                    sb.Remove(section.Start, section.End - section.Start);
                    sb.Insert(section.Start, Cdata(text));
                }
                result.Text = sb.ToString();
                result.Changed = !string.Equals(result.Text, xml, StringComparison.Ordinal);
                result.SectionsFormatted = replacements.Count;
            }

            if (findings.Count > 0)
            {
                // positions refer to the file as it is (or would be) written
                var written = FindSections(result.Text);
                foreach (var (obj, finding) in findings)
                {
                    var section = written.FirstOrDefault(s => s.ObjectPath == obj.Path && (finding.Part == null || s.Part == finding.Part));
                    var (line, column) = FilePosition(result.Text, section, finding.Diagnostic);
                    result.Naming.Add(new ObjectDiagnostic(obj.Path, finding.PartLabel, finding.Diagnostic, line, column));
                }
            }
            return result;
        }

        /// <summary>Line and column of a diagnostic in the file: the code starts after "&lt;![CDATA[".</summary>
        private static (int Line, int Column) FilePosition(string xml, Section section, NamingDiagnostic d)
        {
            if (section == null)
                return (0, 0);
            int codeStart = section.Start + 9;
            int baseLine = 1, lineStart = 0;
            for (int i = 0; i < codeStart && i < xml.Length; i++)
            {
                if (xml[i] == '\n')
                {
                    baseLine++;
                    lineStart = i + 1;
                }
            }
            int baseColumn = codeStart - lineStart + 1;
            return (baseLine + d.Line - 1, d.Line == 1 ? baseColumn + d.Column - 1 : d.Column);
        }

        /// <summary>Adapter: the sections of one object inside the XML file. Writes are collected and applied by <see cref="Process"/>.</summary>
        private sealed class XmlObject : IPlcObject
        {
            private readonly List<Section> _sections;
            private readonly Dictionary<CodePart, string> _texts = new Dictionary<CodePart, string>();

            public XmlObject(List<Section> sections)
            {
                _sections = sections;
                var owner = sections[0].Owner;
                Path = sections[0].ObjectPath;
                Name = owner?.Name;
                Kind = owner?.Tag == "GVL" ? PlcObjectKind.Gvl : owner?.Tag == "Action" ? PlcObjectKind.Action : PlcObjectKind.Other;
            }

            public string Path { get; }
            public string Name { get; }
            public PlcObjectKind Kind { get; }
            public List<(Section Section, string Text)> Replacements { get; } = new List<(Section, string)>();

            private Section SectionOf(CodePart part) => _sections.FirstOrDefault(s => s.Part == part);

            public bool Has(CodePart part) => SectionOf(part) != null;

            public string Read(CodePart part) => _texts.TryGetValue(part, out var text) ? text : SectionOf(part).Code;

            public void Write(CodePart part, string text)
            {
                _texts[part] = text;
                Replacements.Add((SectionOf(part), text));
            }
        }

        /// <summary>The code parts of a TwinCAT file: object path, declaration or implementation, code.</summary>
        internal static IEnumerable<(string ObjectPath, CodePart Part, string Code)> CodeParts(string xml) =>
            FindSections(xml ?? string.Empty).Select(s => (s.ObjectPath, s.Part, s.Code));

        private static string Cdata(string text) => "<![CDATA[" + text.Replace("]]>", "]]]]><![CDATA[>") + "]]>";

        private static List<Section> FindSections(string xml)
        {
            var sections = new List<Section>();
            var stack = new List<Element>();
            int position = 0;

            while (position < xml.Length)
            {
                int lt = xml.IndexOf('<', position);
                if (lt < 0)
                    break;

                if (string.CompareOrdinal(xml, lt, "<![CDATA[", 0, 9) == 0)
                {
                    int end = xml.IndexOf("]]>", lt + 9, StringComparison.Ordinal);
                    position = end < 0 ? xml.Length : end + 3;
                    continue;
                }
                if (string.CompareOrdinal(xml, lt, "<!--", 0, 4) == 0)
                {
                    int end = xml.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                    position = end < 0 ? xml.Length : end + 3;
                    continue;
                }
                if (string.CompareOrdinal(xml, lt, "<?", 0, 2) == 0 || string.CompareOrdinal(xml, lt, "<!", 0, 2) == 0)
                {
                    int end = xml.IndexOf('>', lt);
                    position = end < 0 ? xml.Length : end + 1;
                    continue;
                }

                var match = Tag.Match(xml, lt);
                if (!match.Success || match.Index != lt)
                {
                    position = lt + 1;
                    continue;
                }
                position = match.Index + match.Length;

                bool closing = match.Groups[1].Value == "/";
                bool selfClosing = match.Groups[4].Value == "/";
                string tag = match.Groups[2].Value;

                if (closing)
                {
                    int index = stack.FindLastIndex(e => e.Tag == tag);
                    if (index >= 0)
                        stack.RemoveRange(index, stack.Count - index);
                    continue;
                }
                if (selfClosing)
                    continue;

                if (tag == "Declaration" || tag == "ST")
                {
                    var section = ReadCdata(xml, position, tag);
                    if (section != null)
                    {
                        section.Part = tag == "Declaration" ? CodePart.Declaration : CodePart.Implementation;
                        // owner: element enclosing <Declaration>, or enclosing <Implementation> for <ST>
                        var named = stack.Where(e => e.Name != null).ToList();
                        section.Owner = named.LastOrDefault();
                        section.ObjectPath = string.Join(".", named.Select(e => e.Name));
                        sections.Add(section);
                        position = section.End;
                    }
                }

                var nameMatch = NameAttribute.Match(match.Groups[3].Value);
                stack.Add(new Element { Tag = tag, Name = nameMatch.Success ? nameMatch.Groups[1].Value : null });
            }
            return sections;
        }

        /// <summary>Reads the CDATA run directly after an opening tag up to its closing tag.</summary>
        private static Section ReadCdata(string xml, int start, string tag)
        {
            var sb = new StringBuilder();
            int position = start;
            int contentStart = -1;
            int contentEnd = -1;
            while (true)
            {
                var match = CdataRun.Match(xml, position);
                if (!match.Success)
                    break;
                if (contentStart < 0)
                    contentStart = match.Index + (match.Value.Length - match.Value.TrimStart().Length);
                sb.Append(match.Groups[1].Value);
                position = match.Index + match.Length;
                contentEnd = position;
            }
            if (contentStart < 0)
                return null;

            string closing = "</" + tag + ">";
            if (string.CompareOrdinal(xml, contentEnd, closing, 0, closing.Length) != 0)
                return null; // mixed content: leave alone

            return new Section { Start = contentStart, End = contentEnd, Code = sb.ToString() };
        }
    }
}
