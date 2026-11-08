using System;
using System.Text;
using System.Text.RegularExpressions;

namespace TcFormatter.Formatting
{
    /// <summary>
    /// Aligns a marker at the end of the lines of multi-line comments (e.g. the "&lt;br&gt;" of help headers)
    /// at a fixed column. Only the whitespace in front of and behind the marker changes.
    /// </summary>
    internal static class CommentLineEndMarkers
    {
        public static bool Enabled(FormatterOptions o) =>
            !string.IsNullOrEmpty(o.CommentLineEndMarker) && o.CommentLineEndMarkerColumn > 0;

        /// <param name="comment">Text of a block comment.</param>
        /// <param name="firstLineAtColumnZero">The comment starts at the beginning of a line, so its first line can be aligned too.</param>
        public static string Align(string comment, FormatterOptions o, bool firstLineAtColumnZero)
        {
            if (!Enabled(o) || comment.IndexOf(o.CommentLineEndMarker, StringComparison.Ordinal) < 0)
                return comment;

            int tabWidth = Math.Max(1, o.IndentSize);
            int column = o.CommentLineEndMarkerColumn - 1;
            bool tabs = o.IndentStyle == IndentStyle.Tabs && o.AlignWithTabs && column % tabWidth == 0;
            string marker = o.CommentLineEndMarker;

            var lines = comment.Split('\n');
            for (int i = firstLineAtColumnZero ? 0 : 1; i < lines.Length; i++)
            {
                string line = lines[i];
                bool cr = line.EndsWith("\r", StringComparison.Ordinal);
                string text = (cr ? line.Substring(0, line.Length - 1) : line).TrimEnd(' ', '\t');
                if (!text.EndsWith(marker, StringComparison.Ordinal))
                    continue;

                string content = text.Substring(0, text.Length - marker.Length).TrimEnd(' ', '\t');
                int width = Visual(content, tabWidth);
                var sb = new StringBuilder(content);
                if (width < column)
                {
                    if (tabs)
                    {
                        while (width < column)
                        {
                            sb.Append('\t');
                            width = (width / tabWidth + 1) * tabWidth;
                        }
                    }
                    else
                    {
                        sb.Append(' ', column - width);
                    }
                }
                else if (width > column)
                {
                    sb.Append(tabs ? '\t' : ' ');
                }
                // width == column: the marker follows directly ("-----<br>" lines end exactly at the column)
                sb.Append(marker);
                if (cr)
                    sb.Append('\r');
                lines[i] = sb.ToString();
            }
            return string.Join("\n", lines);
        }

        /// <summary>The comment without the whitespace around line end markers (for the safety check).</summary>
        public static string Normalize(string comment, FormatterOptions o) =>
            Enabled(o)
                ? Regex.Replace(comment, "[ \t]*" + Regex.Escape(o.CommentLineEndMarker) + "[ \t]*(?=\r?\n|$)", o.CommentLineEndMarker)
                : comment;

        private static int Visual(string text, int tabWidth)
        {
            int column = 0;
            foreach (char c in text)
                column = c == '\t' ? (column / tabWidth + 1) * tabWidth : column + 1;
            return column;
        }
    }
}
