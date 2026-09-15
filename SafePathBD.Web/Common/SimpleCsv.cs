using System.Text;

namespace SafePathBD.Web.Common;

/// <summary>Small RFC-4180-style CSV helper used for project-owned development data files.</summary>
public static class SimpleCsv
{
    public static IEnumerable<string[]> ReadRows(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return ParseLine(line).ToArray();
        }
    }

    public static IReadOnlyList<string> ParseLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
