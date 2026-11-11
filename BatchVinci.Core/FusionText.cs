using System.Text;

namespace BatchVinci.Core;

/// <summary>Conversione tra testo reale e stringa Lua/Fusion (\n, \", \\ ...).</summary>
public static class FusionText
{
    public static string Decode(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c != '\\' || i == raw.Length - 1) { sb.Append(c); continue; }
            char n = raw[++i];
            switch (n)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': break;
                case 't': sb.Append('\t'); break;
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                default: sb.Append('\\').Append(n); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>CR e CRLF (es. dalla TextBox di WinUI) diventano LF, come nel drp.</summary>
    public static string NormalizeNewLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string Encode(string text)
    {
        text = NormalizeNewLines(text);
        var sb = new StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
