namespace NugetDocs.Cli.Services;

/// <summary>
/// Myers diff algorithm for producing unified-style diffs between two sequences of lines.
/// </summary>
internal static class MyersDiff
{
    public enum EditKind
    {
        Equal,
        Insert,
        Delete,
    }

    public record Edit(EditKind Kind, string Line);

    /// <summary>
    /// Compute the diff between two line sequences using the Myers algorithm.
    /// Returns a list of edits (Equal, Insert, Delete).
    /// </summary>
    public static List<Edit> Compute(string[] a, string[] b)
    {
        var n = a.Length;
        var m = b.Length;
        var max = n + m;

        if (max == 0)
        {
            return [];
        }

        // V stores the endpoint x for each k-diagonal
        // V[k + offset] = x
        var offset = max;
        var vSize = 2 * max + 1;
        var v = new int[vSize];

        // Store the trace of V at each step for backtracking
        var trace = new List<int[]>();

        for (var d = 0; d <= max; d++)
        {
            // Save current V state
            trace.Add((int[])v.Clone());

            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[k - 1 + offset] < v[k + 1 + offset]))
                {
                    x = v[k + 1 + offset]; // move down
                }
                else
                {
                    x = v[k - 1 + offset] + 1; // move right
                }

                var y = x - k;

                // Follow diagonal (equal elements)
                while (x < n && y < m && string.Equals(a[x].TrimEnd(), b[y].TrimEnd(), StringComparison.Ordinal))
                {
                    x++;
                    y++;
                }

                v[k + offset] = x;

                if (x >= n && y >= m)
                {
                    // Found the shortest edit script — backtrack
                    return Backtrack(trace, a, b, offset);
                }
            }
        }

        // Fallback (shouldn't reach here)
        return Backtrack(trace, a, b, offset);
    }

    private static List<Edit> Backtrack(List<int[]> trace, string[] a, string[] b, int offset)
    {
        var edits = new List<Edit>();
        var x = a.Length;
        var y = b.Length;

        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;

            int prevK;
            if (k == -d || (k != d && v[k - 1 + offset] < v[k + 1 + offset]))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }

            var prevX = v[prevK + offset];
            var prevY = prevX - prevK;

            // Diagonal moves (equal lines)
            while (x > prevX && y > prevY)
            {
                x--;
                y--;
                edits.Add(new Edit(EditKind.Equal, a[x]));
            }

            if (d > 0)
            {
                if (x == prevX)
                {
                    // Insert (move down)
                    y--;
                    edits.Add(new Edit(EditKind.Insert, b[y]));
                }
                else
                {
                    // Delete (move right)
                    x--;
                    edits.Add(new Edit(EditKind.Delete, a[x]));
                }
            }
        }

        edits.Reverse();
        return edits;
    }

    /// <summary>
    /// Format edits as unified diff output, showing only changed hunks with context.
    /// </summary>
    public static List<string> FormatUnified(List<Edit> edits, int contextLines = 3)
    {
        var output = new List<string>();

        // Find ranges of changes with surrounding context
        var changeIndices = new List<int>();
        for (var i = 0; i < edits.Count; i++)
        {
            if (edits[i].Kind != EditKind.Equal)
            {
                changeIndices.Add(i);
            }
        }

        if (changeIndices.Count == 0)
        {
            return output;
        }

        // Group changes into hunks (merge nearby changes)
        var hunks = new List<(int Start, int End)>();
        var hunkStart = Math.Max(0, changeIndices[0] - contextLines);
        var hunkEnd = Math.Min(edits.Count, changeIndices[0] + contextLines + 1);

        for (var i = 1; i < changeIndices.Count; i++)
        {
            var changeStart = changeIndices[i] - contextLines;
            var changeEnd = Math.Min(edits.Count, changeIndices[i] + contextLines + 1);

            if (changeStart <= hunkEnd)
            {
                // Merge with current hunk
                hunkEnd = changeEnd;
            }
            else
            {
                hunks.Add((hunkStart, hunkEnd));
                hunkStart = changeStart;
                hunkEnd = changeEnd;
            }
        }
        hunks.Add((hunkStart, hunkEnd));

        // Output each hunk
        foreach (var (start, end) in hunks)
        {
            for (var i = start; i < end; i++)
            {
                var edit = edits[i];
                var line = edit.Line.TrimEnd();
                var prefix = edit.Kind switch
                {
                    EditKind.Delete => "- ",
                    EditKind.Insert => "+ ",
                    _ => "  ",
                };
                output.Add($"{prefix}{line}");
            }

            // Add separator between hunks
            if (hunks.Count > 1 && (start, end) != hunks[^1])
            {
                output.Add("  ...");
            }
        }

        return output;
    }
}
