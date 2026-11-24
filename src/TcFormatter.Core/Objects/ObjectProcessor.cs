using System;
using System.Collections.Generic;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Objects
{
    /// <summary>What the naming rules need to know about the kind of an Objekt; everything else is read from its text.</summary>
    public enum PlcObjectKind
    {
        /// <summary>POU, method, property, DUT, interface, ...: kind and name are part of the declaration text.</summary>
        Other,

        /// <summary>Global variable list: its name is not part of the text.</summary>
        Gvl,

        /// <summary>Action: it has no declaration, only a name.</summary>
        Action,
    }

    /// <summary>
    /// One Objekt of a PLC project as the <see cref="ObjectProcessor"/> sees it. Adapters: an object inside a TwinCAT XML
    /// file (<see cref="TwinCatFiles.TcFileProcessor"/>) and an item of the TwinCAT project tree (VSIX).
    /// </summary>
    public interface IPlcObject
    {
        /// <summary>Path of the object used in messages (file object path or project tree path).</summary>
        string Path { get; }

        /// <summary>Name of the object in the project.</summary>
        string Name { get; }

        PlcObjectKind Kind { get; }

        /// <summary>The object has this Code-Teil as Structured Text.</summary>
        bool Has(CodePart part);

        string Read(CodePart part);

        void Write(CodePart part, string text);
    }

    /// <summary>A naming finding of an object. <see cref="Part"/> is null when it is about the object name itself (actions).</summary>
    public sealed class ObjectFinding
    {
        public ObjectFinding(CodePart? part, NamingDiagnostic diagnostic)
        {
            Part = part;
            Diagnostic = diagnostic;
        }

        public CodePart? Part { get; }

        /// <summary>Line and column are relative to the Code-Teil (or 1, 1 for the object name).</summary>
        public NamingDiagnostic Diagnostic { get; }

        /// <summary>"Declaration", "Implementation" or "Name".</summary>
        public string PartLabel => Part?.ToString() ?? "Name";
    }

    public sealed class ObjectResult
    {
        /// <summary>At least one Code-Teil was rewritten.</summary>
        public bool Changed { get; internal set; }

        /// <summary>Ablehnungen and failures, as "path (part): reason".</summary>
        public List<string> Errors { get; } = new List<string>();

        public List<ObjectFinding> Naming { get; } = new List<ObjectFinding>();
    }

    /// <summary>
    /// Formats and / or checks the names of one Objekt. The order is fixed: each Code-Teil is formatted first and the
    /// naming rules check the text as it is written, so that positions refer to the formatted code. One failing
    /// object never stops a run: exceptions become an error of that object.
    /// </summary>
    public static class ObjectProcessor
    {
        public static ObjectResult Process(IPlcObject obj, TcFormatConfig config, bool format, bool lint)
        {
            config = config ?? new TcFormatConfig();
            var result = new ObjectResult();
            string path = "?";
            try
            {
                path = obj.Path;
                foreach (var part in new[] { CodePart.Declaration, CodePart.Implementation })
                {
                    bool needed = format || (lint && part == CodePart.Declaration);
                    if (!needed || !obj.Has(part))
                        continue;
                    string text = obj.Read(part);
                    bool syntaxError = false;
                    if (format)
                        text = Format(obj, part, text, config, path, result, out syntaxError);
                    // a syntax error is already reported as Ablehnung of the formatter; the naming check would repeat it
                    if (lint && part == CodePart.Declaration && !syntaxError)
                    {
                        string name = obj.Kind == PlcObjectKind.Gvl ? obj.Name : null;
                        string kind = obj.Kind == PlcObjectKind.Gvl ? "GVL" : null;
                        foreach (var d in NamingChecker.Check(text, config.Naming, name, kind))
                            result.Naming.Add(new ObjectFinding(part, d));
                    }
                }

                if (lint && obj.Kind == PlcObjectKind.Action)
                {
                    foreach (var d in NamingChecker.CheckObjectName(obj.Name, "ACTION", config.Naming))
                        result.Naming.Add(new ObjectFinding(null, d));
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{path}: {ex.GetType().Name}: {ex.Message}");
            }
            return result;
        }

        private static string Format(IPlcObject obj, CodePart part, string text, TcFormatConfig config, string path, ObjectResult result,
                                     out bool syntaxError)
        {
            var formatted = StFormatter.Format(text, part, config.Format);
            syntaxError = formatted.SyntaxError != null;
            if (formatted.Error != null)
            {
                result.Errors.Add($"{path} ({part}): {formatted.Error}");
                return text;
            }
            if (!formatted.Changed)
                return text;
            obj.Write(part, formatted.Text);
            result.Changed = true;
            return formatted.Text;
        }
    }
}
