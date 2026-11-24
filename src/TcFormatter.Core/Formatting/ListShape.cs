using System.Collections.Generic;
using TcFormatter.Lexing;
using TcFormatter.Syntax;

namespace TcFormatter.Formatting
{
    internal enum ListKind { Call, Initializer, Enum }

    /// <summary>
    /// A list in parentheses or brackets inside one logical line that may be wrapped one element per line:
    /// the arguments of a call statement, a structure / array / function block initializer of a declaration,
    /// or the values of an enum. The list comes from the parse tree (<see cref="TokenRoles"/>); this class only maps it
    /// to the positions in the line. Lines with comments or pragmas before the final ';' keep their layout.
    /// </summary>
    internal sealed class ListShape
    {
        public ListKind Kind { get; private set; }
        public int Open { get; private set; }
        public int Close { get; private set; }

        /// <summary>Token ranges [start, end) of the elements.</summary>
        public List<(int Start, int End)> Items { get; } = new List<(int, int)>();

        /// <summary><c>[target :=] callee( arg, arg, ... );</c></summary>
        public static ListShape Call(IList<Token> tokens, TokenRoles roles) =>
            From(ListKind.Call, roles.Calls, tokens, tokens.Count > 0 ? tokens[0] : null);

        /// <summary><c>name : TYPE := ( ... );</c>, <c>name : TYPE := [ ... ];</c>, <c>fb : FB_X( ... );</c> or <c>e : (A, B);</c></summary>
        public static ListShape Initializer(IList<Token> tokens, TokenRoles roles) =>
            From(ListKind.Initializer, roles.Initializers, tokens, tokens.Count > 0 ? tokens[0] : null);

        /// <summary><c>TYPE name : ( A, B := 2 ) [BASETYPE] ;</c> or, after a line break behind TYPE, <c>name : ( ... );</c></summary>
        public static ListShape Enum(IList<Token> tokens, TokenRoles roles)
        {
            if (tokens.Count == 0)
                return null;
            var name = tokens[0].IsKeyword("TYPE") && tokens.Count > 1 ? tokens[1] : tokens[0];
            return From(ListKind.Enum, roles.Enums, tokens, name);
        }

        private static ListShape From(ListKind kind, Dictionary<Token, ListSpan> spans, IList<Token> tokens, Token key)
        {
            if (key == null || !spans.TryGetValue(key, out var span))
                return null;
            int end = tokens.IndexOf(span.End);
            if (end < 0)
                return null;
            for (int k = 0; k < tokens.Count; k++)
            {
                var t = tokens[k];
                if (k < end ? t.IsComment || t.Kind == TokenKind.Pragma : k > end && !t.IsComment)
                    return null;
            }

            var shape = new ListShape { Kind = kind, Open = tokens.IndexOf(span.Open), Close = tokens.IndexOf(span.Close) };
            if (shape.Open < 0 || shape.Close < 0)
                return null;
            foreach (var (first, last) in span.Items)
            {
                int start = tokens.IndexOf(first), stop = tokens.IndexOf(last);
                if (start < 0 || stop < start)
                    return null;
                shape.Items.Add((start, stop + 1));
            }
            return shape;
        }
    }
}
