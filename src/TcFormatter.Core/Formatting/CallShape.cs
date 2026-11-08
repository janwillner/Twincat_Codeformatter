using System.Collections.Generic;
using TcFormatter.Lexing;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Shape of a call statement: <c>[target :=] callee( arg, arg, ... );</c> followed only by comments.
    /// </summary>
    internal sealed class CallShape
    {
        public int OpenParen { get; private set; }
        public int CloseParen { get; private set; }

        /// <summary>Token ranges [start, end) of each argument.</summary>
        public List<(int Start, int End)> Arguments { get; } = new List<(int, int)>();

        public static CallShape TryParse(IList<Token> tokens)
        {
            int i = 0;
            if (!SkipPath(tokens, ref i))
                return null;

            if (i < tokens.Count && tokens[i].IsOperator(":="))
            {
                i++;
                if (!SkipPath(tokens, ref i))
                    return null;
            }

            if (i >= tokens.Count || !tokens[i].IsOperator("("))
                return null;

            var shape = new CallShape { OpenParen = i };
            int depth = 0;
            int argStart = i + 1;
            for (int j = i; j < tokens.Count; j++)
            {
                var t = tokens[j];
                if (t.IsComment || t.Kind == TokenKind.Pragma)
                    return null;
                if (t.Kind != TokenKind.Operator)
                    continue;
                if (t.Text == "(" || t.Text == "[")
                {
                    depth++;
                }
                else if (t.Text == ")" || t.Text == "]")
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (j > argStart)
                            shape.Arguments.Add((argStart, j));
                        shape.CloseParen = j;
                        break;
                    }
                }
                else if (t.Text == "," && depth == 1)
                {
                    if (j == argStart)
                        return null;
                    shape.Arguments.Add((argStart, j));
                    argStart = j + 1;
                }
                else if (t.Text == ";")
                {
                    return null;
                }
            }

            if (shape.CloseParen == 0)
                return null;

            // after ')' only ';' and trailing comments are allowed
            int k = shape.CloseParen + 1;
            if (k >= tokens.Count || !tokens[k].IsOperator(";"))
                return null;
            for (k++; k < tokens.Count; k++)
            {
                if (!tokens[k].IsComment)
                    return null;
            }
            return shape;
        }

        /// <summary>Skips an lvalue/callee path like <c>fbA.aItems[i]^.M</c>, <c>THIS^.M</c>, <c>SUPER^</c>.</summary>
        private static bool SkipPath(IList<Token> tokens, ref int i)
        {
            if (i >= tokens.Count)
                return false;
            var first = tokens[i];
            bool ok = first.Kind == TokenKind.Identifier ||
                      (first.Kind == TokenKind.Keyword && (first.Is("THIS") || first.Is("SUPER")));
            if (!ok)
                return false;
            i++;
            while (i < tokens.Count)
            {
                var t = tokens[i];
                if (t.IsOperator("^"))
                {
                    i++;
                }
                else if (t.IsOperator(".") && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Identifier)
                {
                    i += 2;
                }
                else if (t.IsOperator("["))
                {
                    int depth = 0;
                    for (; i < tokens.Count; i++)
                    {
                        if (tokens[i].IsOperator("["))
                            depth++;
                        else if (tokens[i].IsOperator("]") && --depth == 0)
                            break;
                        else if (tokens[i].IsOperator(";"))
                            return false;
                    }
                    if (i >= tokens.Count)
                        return false;
                    i++;
                }
                else
                {
                    break;
                }
            }
            return true;
        }
    }
}
