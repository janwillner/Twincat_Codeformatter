using System.Collections.Generic;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    internal enum FrameKind { If, Case, For, While, Repeat, Var, Struct, Union, Region, Try }

    internal sealed class Frame
    {
        public Frame(FrameKind kind, int level, int id)
        {
            Kind = kind;
            Level = level;
            Id = id;
        }

        public FrameKind Kind { get; }

        /// <summary>Indentation level of the line that opened the frame.</summary>
        public int Level { get; }

        public int Id { get; }

        /// <summary>CASE: the OF keyword was seen. FOR/WHILE: the DO keyword was seen.</summary>
        public bool HeaderDone { get; set; }

        /// <summary>CASE: at least one label (or ELSE) was seen, so statements belong to a label body.</summary>
        public bool CaseInBody { get; set; }
    }

    /// <summary>One logical output line (may contain a multi-line block comment).</summary>
    internal sealed class Line
    {
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>Blank lines before this line in the source.</summary>
        public int BlankBefore { get; set; }

        /// <summary>Line lies in a "tcfmt: off" region and is emitted exactly as in the source.</summary>
        public bool Verbatim { get; set; }

        /// <summary>Original text of the physical line (without line break).</summary>
        public string OriginalText { get; set; } = string.Empty;

        public int Indent { get; set; }

        public bool IsContinuation { get; set; }

        /// <summary>Last significant token opens a block (THEN, DO, VAR, case label, ...).</summary>
        public bool OpensBlock { get; set; }

        /// <summary>First token closes a block (END_xx, ELSE, ELSIF, UNTIL, ...).</summary>
        public bool StartsWithCloser { get; set; }

        /// <summary>Id of the innermost frame; lines with different ids never share an alignment block.</summary>
        public int FrameId { get; set; }

        /// <summary>Line is a member declaration inside VAR / STRUCT / UNION.</summary>
        public bool InDeclarationSection { get; set; }

        /// <summary>Line is an enum member inside TYPE ... ( ... ).</summary>
        public bool IsEnumMember { get; set; }

        /// <summary>Line is a statement of the implementation (not a continuation).</summary>
        public bool IsStatement { get; set; }
    }
}
