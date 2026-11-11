using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace BatchVinci.Core;

/// <summary>
/// Decodifica/ricodifica il contenuto di &lt;CompositionBA&gt;:
/// hex → [u32 BE len][zlib] → mappa binaria con chiave "0_data" →
/// [header testo "Composition {...}\0"][u32 LE len][zlib] → script Fusion (UTF-8).
/// </summary>
public sealed class CompositionCodec
{
    static readonly byte[] DataKey = Encoding.BigEndianUnicode.GetBytes("0_data");

    byte[] _outer = [];
    int _lenFieldPos;      // posizione del campo lunghezza (u32 BE) del blob 0_data (dopo type e flag)
    int _dataStart;        // inizio del blob 0_data
    int _dataLen;
    byte[] _header = [];   // header testuale, escluso lo \0 finale

    public string Header => Encoding.UTF8.GetString(_header);
    public string Script { get; private set; } = "";

    public static CompositionCodec Decode(string hex)
    {
        var codec = new CompositionCodec();
        byte[] b = Convert.FromHexString(hex);
        codec._outer = Inflate(b.AsSpan(4));

        int k = codec._outer.AsSpan().IndexOf(DataKey);
        if (k < 0) throw new InvalidDataException("Chiave 0_data non trovata.");
        int p = k + DataKey.Length;                 // type(4) isNull(1) len(4)
        codec._lenFieldPos = p + 5;
        codec._dataLen = (int)BinaryPrimitives.ReadUInt32BigEndian(codec._outer.AsSpan(codec._lenFieldPos));
        codec._dataStart = codec._lenFieldPos + 4;
        if (codec._dataStart + codec._dataLen > codec._outer.Length)
            throw new InvalidDataException("Lunghezza 0_data non valida.");

        var data = codec._outer.AsSpan(codec._dataStart, codec._dataLen);
        int z = data.IndexOf((byte)0);
        if (z < 0 || z + 5 >= data.Length || data[z + 5] != 0x78)
            throw new InvalidDataException("Struttura di 0_data non riconosciuta.");
        codec._header = data[..z].ToArray();
        codec.Script = Encoding.UTF8.GetString(Inflate(data[(z + 5)..]));
        return codec;
    }

    /// <summary>Restituisce l'hex da scrivere nell'XML con lo script sostituito.</summary>
    public string Encode(string newScript)
    {
        byte[] script = Encoding.UTF8.GetBytes(newScript);
        byte[] z = Deflate(script);

        var data = new MemoryStream();
        data.Write(_header);
        data.WriteByte(0);
        Span<byte> le = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(le, (uint)script.Length);
        data.Write(le);
        data.Write(z);

        var outer = new MemoryStream();
        outer.Write(_outer.AsSpan(0, _lenFieldPos));
        Span<byte> be = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(be, (uint)data.Length);
        outer.Write(be);
        outer.Write(data.GetBuffer().AsSpan(0, (int)data.Length));
        outer.Write(_outer.AsSpan(_dataStart + _dataLen));   // eventuale coda

        byte[] outerBytes = outer.ToArray();
        byte[] oz = Deflate(outerBytes);
        var final = new byte[4 + oz.Length];
        BinaryPrimitives.WriteUInt32BigEndian(final, (uint)outerBytes.Length);
        oz.CopyTo(final, 4);
        return Convert.ToHexString(final).ToLower(CultureInfo.InvariantCulture);
    }

    static byte[] Inflate(ReadOnlySpan<byte> src)
    {
        using var ms = new MemoryStream(src.ToArray());
        using var z = new ZLibStream(ms, CompressionMode.Decompress);
        using var o = new MemoryStream();
        z.CopyTo(o);
        return o.ToArray();
    }

    static byte[] Deflate(byte[] src)
    {
        using var o = new MemoryStream();
        using (var z = new ZLibStream(o, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(src);
        return o.ToArray();
    }
}
