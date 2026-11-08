using System;

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
        public FormatResult(string text, bool changed, string error)
        {
            Text = text;
            Changed = changed;
            Error = error;
        }

        /// <summary>Formatted text, or the unchanged input when <see cref="Error"/> is set.</summary>
        public string Text { get; }

        public bool Changed { get; }

        /// <summary>Set when formatting was refused (safety check failed or internal error). The input is returned unchanged.</summary>
        public string Error { get; }
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
                formatted = new FormatEngine(code, part, options).Run();
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
