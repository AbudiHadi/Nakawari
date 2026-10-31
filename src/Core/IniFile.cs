using System.Text;

namespace Nakawari.Core;

/// <summary>
/// Minimal section-aware INI editor. It keeps every line it does not own byte-for-byte,
/// including the file's original encoding (ReShade.ini is sometimes saved with a BOM).
/// </summary>
public sealed class IniFile
{
    private readonly string _path;
    private readonly List<string> _lines;
    private readonly Encoding _encoding;

    private IniFile(string path, List<string> lines, Encoding encoding)
    {
        _path = path;
        _lines = lines;
        _encoding = encoding;
    }

    public static IniFile Open(string path)
    {
        if (!File.Exists(path)) return new IniFile(path, [], new UTF8Encoding(false));
        using var reader = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return new IniFile(path, lines, reader.CurrentEncoding);
    }

    public string? Get(string section, string key)
    {
        var (start, end) = FindSection(section);
        if (start < 0) return null;
        var index = FindKey(start, end, key);
        return index < 0 ? null : _lines[index][(_lines[index].IndexOf('=') + 1)..].Trim();
    }

    public void Set(string section, string key, string value)
    {
        var (start, end) = FindSection(section);
        if (start < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Length > 0) _lines.Add(string.Empty);
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        var index = FindKey(start, end, key);
        if (index >= 0) _lines[index] = $"{key}={value}";
        else _lines.Insert(end, $"{key}={value}");
    }

    public void RemoveSection(string section)
    {
        var (start, end) = FindSection(section);
        if (start < 0) return;
        _lines.RemoveRange(start - 1, end - start + 1);
        while (_lines.Count > 0 && _lines[^1].Trim().Length == 0) _lines.RemoveAt(_lines.Count - 1);
    }

    public void Save() => SafeFs.WriteText(_path, string.Join("\r\n", _lines) + "\r\n", _encoding);

    /// <summary>Returns (first line after the header, index just past the last key line).</summary>
    private (int Start, int End) FindSection(string section)
    {
        var header = $"[{section}]";
        var headerIndex = _lines.FindIndex(l => l.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0) return (-1, -1);

        var end = headerIndex + 1;
        while (end < _lines.Count && !_lines[end].TrimStart().StartsWith('[')) end++;
        while (end > headerIndex + 1 && _lines[end - 1].Trim().Length == 0) end--;
        return (headerIndex + 1, end);
    }

    private int FindKey(int start, int end, string key)
    {
        for (var i = start; i < end; i++)
        {
            var eq = _lines[i].IndexOf('=');
            if (eq > 0 && _lines[i][..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
