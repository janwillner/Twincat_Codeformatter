using System.Collections.Generic;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    internal enum ListKind { Call, Initializer, Enum }

    /// <summary>
    /// A list in parentheses or brackets inside one logical line that may be wrapped one element per line:
    /// the arguments of a call statement, a structure / array / function block initializer of a declaration,
    /// or the values of an implicit enum.
    /// </summary>
    internal sealed class ListShape
    {
        public ListKind Kind { get; private set; }
        public int Open { get; private set; }
        public int Close { get; private set; }

        /// <summary>Token ranges [start, end) of the elements.</summary>
        public List<(int Start, int End)> Items { get; } = new List<(int, int)>();

        public static ListShape Call(IList<Token> tokens)
        {
            var call = CallShape.TryParse(tokens);
            if (call == null)
                return null;
            var shape = new ListShape { Kind = ListKind.Call, Open = call.OpenParen, Close = call.CloseParen };
            shape.Items.AddRange(call.Arguments);
            return shape;
        }

        /// <summary><c>name : TYPE := ( ... );</c>, <c>name : TYPE := [ ... ];</c> or <c>fb : FB_X( ... );</c></summary>
        public static ListShape Initializer(IList<Token> tokens)
        {
            int end = LastCode(tokens);
            if (end < 0 || !tokens[end].IsOperator(";"))
                return null;

            int depth = 0, colon = -1, assign = -1;
            for (int i = 0; i < end; i++)
            {
                var t = tokens[i];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                    return null;
                if (t.IsOperator("(") || t.IsOperator("[")) depth++;
                else if (t.IsOperator(")") || t.IsOperator("]")) depth--;
                else if (depth == 0 && t.IsOperator(":") && colon < 0) colon = i;
                else if (depth == 0 && t.IsOperator(":=") && colon >= 0 && assign < 0) assign = i;
            }
            if (colon <= 0)
                return null;

            int open;
            if (assign >= 0)
            {
                // the whole initializer is one list
                open = assign + 1;
                if (open >= end || !(tokens[open].IsOperator("(") || tokens[open].IsOperator("[")))
                    return null;
            }
            else
            {
                // FB initialisation: the type ends with "(...)"
                open = -1;
                for (int i = colon + 1; i < end; i++)
                {
                    if (tokens[i].IsOperator("("))
                    {
                        open = i;
                        break;
                    }
                    if (tokens[i].IsOperator("["))
                        return null; // ARRAY[..] OF FB_X(...) is not supported
                }
                if (open < 0)
                    return null;
            }

            var shape = new ListShape { Kind = ListKind.Initializer, Open = open };
            if (!ReadItems(tokens, shape, end) || shape.Close != end - 1)
                return null;
            return shape;
        }

        /// <summary><c>TYPE name : ( A, B := 2 ) [BASETYPE] ;</c></summary>
        public static ListShape Enum(IList<Token> tokens)
        {
            int end = LastCode(tokens);
            if (end < 3 || !tokens[end].IsOperator(";"))
                return null;
            // "TYPE name : (" or, after a line break behind TYPE, "name : ("
            int i = tokens[0].IsKeyword("TYPE") ? 1 : 0;
            if (tokens[i].Kind != TokenKind.Identifier)
                return null;
            while (i < end && !tokens[i].IsOperator(":"))
                i++;
            if (i + 1 >= end || !tokens[i + 1].IsOperator("("))
                return null;
            for (int k = 0; k < end; k++)
            {
                if (tokens[k].IsComment || tokens[k].Kind == TokenKind.Pragma)
                    return null;
            }

            var shape = new ListShape { Kind = ListKind.Enum, Open = i + 1 };
            if (!ReadItems(tokens, shape, end))
                return null;
            // after ')' only an optional base type
            for (int k = shape.Close + 1; k < end; k++)
            {
                if (tokens[k].Kind != TokenKind.TypeName && tokens[k].Kind != TokenKind.Identifier)
                    return null;
            }
            return shape;
        }

        private static bool ReadItems(IList<Token> tokens, ListShape shape, int end)
        {
            int depth = 0;
            int itemStart = shape.Open + 1;
            for (int j = shape.Open; j < end; j++)
            {
                var t = tokens[j];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                    return false;
                if (t.IsOperator("(") || t.IsOperator("["))
                {
                    depth++;
                }
                else if (t.IsOperator(")") || t.IsOperator("]"))
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (j > itemStart)
                            shape.Items.Add((itemStart, j));
                        shape.Close = j;
                        return shape.Items.Count > 0;
                    }
                }
                else if (depth == 1 && t.IsOperator(","))
                {
                    if (j == itemStart)
                        return false;
                    shape.Items.Add((itemStart, j));
                    itemStart = j + 1;
                }
            }
            return false;
        }

        private static int LastCode(IList<Token> tokens)
        {
            int i = tokens.Count - 1;
            while (i >= 0 && tokens[i].IsComment)
                i--;
            return i;
        }
    }
}
