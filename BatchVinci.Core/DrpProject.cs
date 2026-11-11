using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace BatchVinci.Core;

/// <summary>Un testo (Text+/Fusion Title) trovato nella timeline.</summary>
public sealed record TextOccurrence(
    string Key,
    string EntryPath,
    string ClipId,
    string ClipName,
    string Sequence,
    string Template,
    long StartFrame,
    string Text);

public static class DrpProject
{
    static readonly Regex CompRx = new(@"<CompositionBA>([0-9a-fA-F]+)</CompositionBA>", RegexOptions.Compiled);
    static readonly Regex ClipRx = new(@"<Sm2TiVideoClip DbId=""([^""]+)""", RegexOptions.Compiled);
    static readonly Regex NameRx = new(@"<Name>([^<]*)</Name>", RegexOptions.Compiled);
    static readonly Regex StartRx = new(@"<Start>(-?\d+)</Start>", RegexOptions.Compiled);
    static readonly Regex SeqRefRx = new(@"<Sequence>([^<]+)</Sequence>", RegexOptions.Compiled);
    static readonly Regex TemplateRx = new(@"TEMPLATE_ID\s*=\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
    // StyledText = Input { Value = "..." }   oppure   Value = StyledText { Value = "..." }
    static readonly Regex TextRx = new(
        @"(?:\bStyledText\s*=\s*Input\s*\{\s*Value\s*=\s*|\bValue\s*=\s*StyledText\s*\{\s*Value\s*=\s*)""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled);

    public static IReadOnlyList<TextOccurrence> Scan(string drpPath)
    {
        var found = new List<TextOccurrence>();
        using var zip = ZipFile.OpenRead(drpPath);
        var seqNames = BuildSequenceNames(zip);
        // Solo i contenitori delle sequenze (timeline e compound clip), non i template del Media Pool.
        foreach (var e in zip.Entries.Where(x => IsXml(x) && x.FullName.StartsWith("SeqContainer/", StringComparison.OrdinalIgnoreCase)))
        {
            string xml = ReadText(e);
            if (!xml.Contains("<CompositionBA>", StringComparison.Ordinal)) continue;
            int n = 0;
            Transform(xml, e.FullName, seqNames, null, found, ref n);
        }
        return found;
    }

    /// <summary>Crea un NUOVO drp applicando i testi (key → nuovo testo). Il sorgente non viene toccato.</summary>
    public static int Export(string sourcePath, string destPath, IReadOnlyDictionary<string, string> newTexts)
    {
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Il file di destinazione coincide con il progetto originale.");

        string tmp = destPath + ".tmp";
        int replaced = 0;
        try
        {
            using (var src = ZipFile.OpenRead(sourcePath))
            using (var dst = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                foreach (var e in src.Entries)
                {
                    var ne = dst.CreateEntry(e.FullName, CompressionLevel.Optimal);
                    ne.LastWriteTime = e.LastWriteTime;
                    using var o = ne.Open();
                    if (newTexts.Count > 0 && IsXml(e))
                    {
                        string xml = ReadText(e);
                        if (xml.Contains("<CompositionBA>", StringComparison.Ordinal))
                        {
                            xml = Transform(xml, e.FullName, new(), newTexts, null, ref replaced);
                            o.Write(new UTF8Encoding(false).GetBytes(xml));
                            continue;
                        }
                    }
                    using var i = e.Open();
                    i.CopyTo(o);
                }
            }
            File.Move(tmp, destPath, overwrite: true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        return replaced;
    }

    static bool IsXml(ZipArchiveEntry e) => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    static string ReadText(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var r = new StreamReader(s, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
        return r.ReadToEnd();
    }

    static string Transform(string xml, string entry, Dictionary<string, string> seqNames,
        IReadOnlyDictionary<string, string>? replace, List<TextOccurrence>? found, ref int replaced)
    {
        string seqId = SeqRefRx.Match(xml) is { Success: true } sm ? sm.Groups[1].Value : "";
        string seqName = seqNames.TryGetValue(seqId, out var sn) ? sn
            : (seqId.Length > 0 ? seqId : Path.GetFileNameWithoutExtension(entry));

        // posizione dei clip, per associare ogni composizione al suo clip
        var clips = ClipRx.Matches(xml).Select(m => (m.Index, Id: m.Groups[1].Value)).ToList();

        int compIdx = 0;
        var sb = new StringBuilder(xml.Length);
        int last = 0;
        bool any = false;
        foreach (Match m in CompRx.Matches(xml))
        {
            int idx = compIdx++;
            CompositionCodec codec;
            try { codec = CompositionCodec.Decode(m.Groups[1].Value); }
            catch (Exception) { continue; }   // composizione non riconosciuta: la lasciamo intatta

            var texts = TextRx.Matches(codec.Script);
            if (texts.Count == 0) continue;

            string clipId = "", clipName = ""; long start = 0;
            int ci = clips.FindLastIndex(c => c.Index < m.Index);
            if (ci >= 0)
            {
                clipId = clips[ci].Id;
                string tail = xml.Substring(clips[ci].Index, Math.Min(1500, m.Index - clips[ci].Index));
                if (NameRx.Match(tail) is { Success: true } nm) clipName = nm.Groups[1].Value;
                if (StartRx.Match(tail) is { Success: true } st) start = long.Parse(st.Groups[1].Value);
            }
            string template = TemplateRx.Match(codec.Header) is { Success: true } tm ? tm.Groups[1].Value : "";
            int slash = template.LastIndexOf('/');
            if (slash >= 0) template = template[(slash + 1)..];

            var edits = new List<(int Index, int Length, string NewRaw)>();
            for (int o = 0; o < texts.Count; o++)
            {
                var g = texts[o].Groups[1];
                string key = $"{entry}|{clipId}|{idx}|{o}";
                string text = FusionText.Decode(g.Value);
                found?.Add(new TextOccurrence(key, entry, clipId, clipName, seqName, template, start, text));
                if (replace != null && replace.TryGetValue(key, out var nt) && nt != text)
                    edits.Add((g.Index, g.Length, FusionText.Encode(nt)));
            }
            if (edits.Count == 0) continue;

            var script = new StringBuilder(codec.Script);
            foreach (var ed in edits.OrderByDescending(x => x.Index))
                script.Remove(ed.Index, ed.Length).Insert(ed.Index, ed.NewRaw);
            replaced += edits.Count;

            sb.Append(xml, last, m.Groups[1].Index - last).Append(codec.Encode(script.ToString()));
            last = m.Groups[1].Index + m.Groups[1].Length;
            any = true;
        }
        if (!any) return xml;
        sb.Append(xml, last, xml.Length - last);
        return sb.ToString();
    }

    /// <summary>GUID Sm2Sequence → nome dell'elemento del media pool (timeline / compound clip).</summary>
    static Dictionary<string, string> BuildSequenceNames(ZipArchive zip)
    {
        var map = new Dictionary<string, string>();
        var rx = new Regex(@"<Name>([^<]*)</Name>|<Sm2Sequence DbId=""([^""]+)""");
        foreach (var e in zip.Entries.Where(x => x.FullName.StartsWith("MediaPool/", StringComparison.OrdinalIgnoreCase) && IsXml(x)))
        {
            string xml = ReadText(e);
            string name = "";
            foreach (Match m in rx.Matches(xml))
            {
                if (m.Groups[2].Success) { if (name.Length > 0) map[m.Groups[2].Value] = System.Net.WebUtility.HtmlDecode(name); }
                else name = m.Groups[1].Value;
            }
        }
        return map;
    }
}
