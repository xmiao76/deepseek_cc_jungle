using System.Security.Cryptography;

namespace JungleGame.Core.AI;

/// <summary>
/// On-disk tablebase format (single file "jungle3.tb"):
///   header (64 bytes): magic "JGLTB3", version 1, flags (bit0 = DTM present),
///   header size, reserved, SHA-256 of the payload
///   payload: WDL sections packed at 2 bits per entry (values 1 = loss,
///   2 = draw, 3 = win; 0 never stored), then the DTM byte sections if the
///   flag is set. Entries are ordered combo-major, then by entry index.
/// </summary>
internal static class TablebaseFile
{
    internal const string FileName = "jungle3.tb";
    private const int HeaderSize = 64;
    private const byte Version = 1;
    private const byte FlagHasDtm = 1;

    private static readonly byte[] Magic = "JGLTB3"u8.ToArray();

    internal sealed class Loaded
    {
        internal required byte[] Wdl2;
        internal required byte[] Wdl3;
        internal byte[]? Dtm2;
        internal byte[]? Dtm3;
    }

    internal static void Save(string path, TablebaseBuilder.BuildResult result)
    {
        byte[] packed2 = PackWdl(result.Wdl2);
        byte[] packed3 = PackWdl(result.Wdl3);
        bool hasDtm = result.Dtm2 != null && result.Dtm3 != null;
        var payload = new byte[packed2.Length + packed3.Length +
            (hasDtm ? result.Dtm2!.Length + result.Dtm3!.Length : 0)];
        int offset = 0;
        packed2.CopyTo(payload, offset); offset += packed2.Length;
        packed3.CopyTo(payload, offset); offset += packed3.Length;
        if (hasDtm)
        {
            result.Dtm2!.CopyTo(payload, offset); offset += result.Dtm2.Length;
            result.Dtm3!.CopyTo(payload, offset);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write((byte)(hasDtm ? FlagHasDtm : 0));
        writer.Write((ushort)HeaderSize);
        writer.Write(new byte[22]); // reserved
        writer.Write(SHA256.HashData(payload));
        writer.Write(payload);
    }

    /// <summary>Null on any corruption (bad magic, version, or payload hash).</summary>
    internal static Loaded? Load(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < HeaderSize)
                return null;
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
                return null;
            if (reader.ReadByte() != Version)
                return null;
            bool hasDtm = (reader.ReadByte() & FlagHasDtm) != 0;
            if (reader.ReadUInt16() != HeaderSize)
                return null;
            reader.ReadBytes(22); // reserved
            byte[] storedHash = reader.ReadBytes(32);
            byte[] payload = reader.ReadBytes(bytes.Length - HeaderSize);

            if (!SHA256.HashData(payload).AsSpan().SequenceEqual(storedHash))
                return null;

            int wdl2Bytes = Wdl2ByteCount;
            int wdl3Bytes = Wdl3ByteCount;
            int dtm2Bytes = TablebaseIndex.Combo2Count * TablebaseIndex.EntriesPerCombo2;
            int dtm3Bytes = TablebaseIndex.Combo3Count * TablebaseIndex.EntriesPerCombo3;
            int expected = wdl2Bytes + wdl3Bytes + (hasDtm ? dtm2Bytes + dtm3Bytes : 0);
            if (payload.Length != expected)
                return null;

            var wdl2 = new byte[wdl2Bytes];
            var wdl3 = new byte[wdl3Bytes];
            Array.Copy(payload, 0, wdl2, 0, wdl2Bytes);
            Array.Copy(payload, wdl2Bytes, wdl3, 0, wdl3Bytes);
            byte[]? dtm2 = null, dtm3 = null;
            if (hasDtm)
            {
                dtm2 = new byte[dtm2Bytes];
                dtm3 = new byte[dtm3Bytes];
                Array.Copy(payload, wdl2Bytes + wdl3Bytes, dtm2, 0, dtm2Bytes);
                Array.Copy(payload, wdl2Bytes + wdl3Bytes + dtm2Bytes, dtm3, 0, dtm3Bytes);
            }

            return new Loaded { Wdl2 = wdl2, Wdl3 = wdl3, Dtm2 = dtm2, Dtm3 = dtm3 };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static int Wdl2ByteCount =>
        (TablebaseIndex.Combo2Count * TablebaseIndex.EntriesPerCombo2 * 2 + 7) / 8;

    internal static int Wdl3ByteCount =>
        (TablebaseIndex.Combo3Count * TablebaseIndex.EntriesPerCombo3 * 2 + 7) / 8;

    internal static byte GetWdl(byte[] packed, int entryIndex)
    {
        int bit = entryIndex * 2;
        return (byte)((packed[bit >> 3] >> (bit & 7)) & 3);
    }

    private static byte[] PackWdl(byte[] wdl)
    {
        var packed = new byte[(wdl.Length * 2 + 7) / 8];
        for (int i = 0; i < wdl.Length; i++)
        {
            int bit = i * 2;
            packed[bit >> 3] |= (byte)((wdl[i] & 3) << (bit & 7));
        }
        return packed;
    }
}
