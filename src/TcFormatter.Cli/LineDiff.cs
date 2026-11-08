using System.Text;

namespace TcFormatter.Cli;

/// <summary>Small LCS based unified diff for '--diff' output.</summary>
internal static class LineDiff
{
    public static string Unified(string file, string before, string after, int context = 2)
    {
        var a = Split(before);
        var b = Split(after);
        var ops = Diff(a, b);

        var sb = new StringBuilder();
        sb.Append("--- ").Append(file).Append('\n');
        sb.Append("+++ ").Append(file).Append(" (formatted)\n");

        int i = 0;
        while (i < ops.Count)
        {
            if (ops[i].Kind == ' ')
            {
                i++;
                continue;
            }
            int start = Math.Max(0, i - context);
            int end = i;
            // extend the hunk while changes are close together
            while (end < ops.Count)
            {
                if (ops[end].Kind != ' ')
                {
                    end++;
                    continue;
                }
                int next = end;
                while (next < ops.Count && ops[next].Kind == ' ' && next - end <= context * 2)
                    next++;
                if (next < ops.Count && ops[next].Kind != ' ' && next - end <= context * 2)
                {
                    end = next;
                    continue;
                }
                break;
            }
            int stop = Math.Min(ops.Count, end + context);
            sb.Append("@@ line ").Append(ops[start].LineA + 1).Append(" @@\n");
            for (int k = start; k < stop; k++)
                sb.Append(ops[k].Kind).Append(ops[k].Text).Append('\n');
            i = stop;
        }
        return sb.ToString();
    }

    private static string[] Split(string text) => text.Replace("\r\n", "\n").Split('\n');

    private static List<(char Kind, string Text, int LineA)> Diff(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        var lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var result = new List<(char, string, int)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add((' ', a[x], x));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add(('-', a[x], x));
                x++;
            }
            else
            {
                result.Add(('+', b[y], x));
                y++;
            }
        }
        for (; x < n; x++)
            result.Add(('-', a[x], x));
        for (; y < m; y++)
            result.Add(('+', b[y], x));
        return result;
    }
}
