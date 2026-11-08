using System.Collections.Generic;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    internal enum FrameKind { If, Case, For, While, Repeat, Var, Struct, Union, Region, Try, Type }

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

        /// <summary>CASE: the ELSE branch was reached.</summary>
        public bool InElse { get; set; }

        /// <summary>CASE: a branch (label or ELSE) has started; <see cref="BranchLines"/> counts its lines.</summary>
        public bool BranchStarted { get; set; }

        public int BranchLines { get; set; }
    }

    /// <summary>One logical output line (may contain a multi-line block comment).</summary>
    internal sealed class Line
    {
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>Blank lines before this line in the source.</summary>
        public int BlankBefore { get; set; }

        /// <summary>Line lies in a "tcfmt: off" / "STweep.Disable" region and is emitted exactly as in the source.</summary>
        public bool Verbatim { get; set; }

        /// <summary>Original text of the physical line (without line break).</summary>
        public string OriginalText { get; set; } = string.Empty;

        public int Indent { get; set; }

        public bool IsContinuation { get; set; }

        /// <summary>For continuation lines: the first line of the statement.</summary>
        public Line StatementHead { get; set; }

        /// <summary>For continuation lines: open parentheses / brackets at the start of the line.</summary>
        public int ParenDepthAtStart { get; set; }

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

        /// <summary>Line starts with TYPE (implicit enum candidate).</summary>
        public bool IsTypeHeader { get; set; }

        /// <summary>The line was joined from several source lines (a list that was already wrapped).</summary>
        public bool WasMultiline { get; set; }

        /// <summary>Do not split this line (simple IF kept on one line).</summary>
        public bool NoSplit { get; set; }

        /// <summary>Line starts a CASE branch (label or ELSE).</summary>
        public bool IsCaseBranchStart { get; set; }

        /// <summary>For branch starts: number of lines of the previous branch (-1 = first branch).</summary>
        public int PreviousCaseBranchLines { get; set; } = -1;
    }
}
