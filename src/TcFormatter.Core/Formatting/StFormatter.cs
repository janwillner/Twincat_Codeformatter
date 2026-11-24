using System;
using TcFormatter.Syntax;

namespace TcFormatter.Formatting
{
    /// <summary>Which text area of a TwinCAT object is formatted.</summary>
    public enum CodePart
    {
        /// <summary>Declaration area: POU header, VAR sections, TYPE definitions, GVL, interface.</summary>
        Declaration,

        /// <summary>Implementation area (Structured Text statements).</summary>
        Implementation,
    }

    public sealed class FormatResult
    {
        public FormatResult(string text, bool changed, string error, SyntaxError syntaxError = null)
        {
            Text = text;
            Changed = changed;
            Error = error;
            SyntaxError = syntaxError;
        }

        /// <summary>Formatted text, or the unchanged input when <see cref="Error"/> is set.</summary>
        public string Text { get; }

        public bool Changed { get; }

        /// <summary>Set when formatting was refused (syntax error, safety check failed or internal error). The input is returned unchanged.</summary>
        public string Error { get; }

        /// <summary>The syntax error when the code was refused because it does not parse, otherwise null.</summary>
        public SyntaxError SyntaxError { get; }
    }

    /// <summary>Entry point of the Structured Text formatter.</summary>
    public static class StFormatter
    {
        public static FormatResult Format(string code, CodePart part, FormatterOptions options = null)
        {
            code = code ?? string.Empty;
            options = options ?? new FormatterOptions();

            string formatted;
            try
            {
                var parse = StSyntax.ParseTree(code, part);
                if (parse.Error != null)
                    return new FormatResult(code, false, "Syntax error, code left unchanged: " + parse.Error, parse.Error);
                formatted = new FormatEngine(code, part, options, parse).Run();
            }
            catch (Exception ex)
            {
                return new FormatResult(code, false, "Internal formatter error: " + ex.Message);
            }

            string difference = Equivalence.FindDifference(code, formatted, options);
            if (difference != null)
                return new FormatResult(code, false, "Safety check failed, code left unchanged: " + difference);

            return new FormatResult(formatted, !string.Equals(code, formatted, StringComparison.Ordinal), null);
        }
    }
}
