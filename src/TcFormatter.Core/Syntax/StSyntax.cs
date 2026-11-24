using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using TcFormatter.Formatting;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Syntax
{
    /// <summary>A syntax error found by the Structured Text parser. Line and column are 1-based.</summary>
    public sealed class SyntaxError
    {
        public SyntaxError(int line, int column, string message)
        {
            Line = line;
            Column = column;
            Message = message;
        }

        public int Line { get; }
        public int Column { get; }
        public string Message { get; }

        public override string ToString() => $"line {Line}, column {Column}: {Message}";
    }

    /// <summary>Result of parsing one code part: all tokens (every channel), the parse tree and the first syntax error.</summary>
    internal sealed class ParseResult
    {
        public ParseResult(IList<IToken> tokens, IParseTree tree, SyntaxError error)
        {
            Tokens = tokens;
            Tree = tree;
            Error = error;
        }

        /// <summary>Every token of the source including the hidden ones; their texts concatenate to the source.</summary>
        public IList<IToken> Tokens { get; }

        public IParseTree Tree { get; }

        /// <summary>The first syntax error, or null.</summary>
        public SyntaxError Error { get; }
    }

    /// <summary>Entry point of the ANTLR based Structured Text parser.</summary>
    public static class StSyntax
    {
        /// <summary>
        /// Parses a declaration or implementation part. Returns no errors for valid code, otherwise the first
        /// syntax error (parsing does not continue behind it).
        /// </summary>
        public static IReadOnlyList<SyntaxError> Parse(string code, CodePart part)
        {
            var error = ParseTree(code, part).Error;
            return error == null ? new SyntaxError[0] : new[] { error };
        }

        internal static StLexer CreateLexer(string code)
        {
            var lexer = new StLexer(new AntlrInputStream(code ?? string.Empty), TextWriter.Null, TextWriter.Null);
            lexer.RemoveErrorListeners();
            return lexer;
        }

        internal static ParseResult ParseTree(string code, CodePart part)
        {
            var tokens = new CommonTokenStream(CreateLexer(code));
            tokens.Fill();

            // Two-stage parsing: the fast SLL mode is enough for almost all code; only when it fails the
            // full LL mode decides whether there really is a syntax error.
            var parser = new StParser(tokens, TextWriter.Null, TextWriter.Null);
            parser.RemoveErrorListeners();
            parser.Interpreter.PredictionMode = PredictionMode.SLL;
            parser.ErrorHandler = new BailErrorStrategy();
            try
            {
                var tree = Run(parser, part);
                return new ParseResult(tokens.GetTokens(), tree, null);
            }
            catch (ParseCanceledException)
            {
            }

            tokens.Seek(0);
            parser.Reset();
            var listener = new FirstErrorListener();
            parser.AddErrorListener(listener);
            parser.Interpreter.PredictionMode = PredictionMode.LL;
            parser.ErrorHandler = new DefaultErrorStrategy();
            var fullTree = Run(parser, part);
            return new ParseResult(tokens.GetTokens(), fullTree, listener.Error);
        }

        private static IParseTree Run(StParser parser, CodePart part) =>
            part == CodePart.Declaration ? (IParseTree)parser.declarationPart() : parser.implementationPart();

        private sealed class FirstErrorListener : BaseErrorListener
        {
            public SyntaxError Error { get; private set; }

            public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line,
                int charPositionInLine, string msg, RecognitionException e)
            {
                if (Error == null)
                    Error = new SyntaxError(line, charPositionInLine + 1, msg);
            }
        }
    }
}
