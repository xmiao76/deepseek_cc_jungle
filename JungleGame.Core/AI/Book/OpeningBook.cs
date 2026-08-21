using System.Security.Cryptography;
using JungleGame.Core.Engine;
using JungleGame.Core.Model;

namespace JungleGame.Core.AI;

/// <summary>
/// Opening book: position hash → move + visit weight, built offline from
/// strong self-play and loaded at startup. The engine only uses it to seed
/// root move ordering (never to force a move — the search stays in charge, so
/// a wrong book entry costs ordering, not a blunder).
///
/// On-disk format (little endian):
///   magic "JGLBK1" (6 bytes), version byte (1), entry count u16 (max 65535),
///   SHA-256 (32 bytes) of the entry payload, then entries sorted by
///   (key, from, to): key u64, from byte (0-62), to byte (0-62), weight u16.
/// An absent or corrupt file degrades silently to no book.
/// </summary>
internal static class OpeningBook
{
    internal const string FileName = "jungle-book.bk";
    private const byte Version = 1;
    private const int HeaderSize = 6 + 1 + 2 + 32;
    private static readonly byte[] Magic = "JGLBK1"u8.ToArray();

    private static readonly object Gate = new();
    private static BookEntry[] _entries = Array.Empty<BookEntry>();

    internal static bool IsLoaded => _entries.Length > 0;
    internal static int Count => _entries.Length;

    internal readonly record struct BookEntry(ulong Key, byte From, byte To, ushort Weight);

    /// <summary>Idempotent: loads the book once from a path, the exe directory, or
    /// %LOCALAPPDATA%\JungleGame. Never throws; corrupt files are ignored.</summary>
    internal static void Initialize(string? path = null)
    {
        lock (Gate)
        {
            if (IsLoaded)
                return;
            string? resolved = path ?? FindBookFile();
            if (resolved == null)
                return;
            var entries = Load(resolved);
            if (entries != null)
                _entries = entries;
        }
    }

    internal static void ResetForTesting()
    {
        lock (Gate)
            _entries = Array.Empty<BookEntry>();
    }

    internal static string? FindBookFile()
    {
        string? env = Environment.GetEnvironmentVariable("JUNGLE_BOOK_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            string envFile = Path.Combine(env, FileName);
            if (File.Exists(envFile))
                return envFile;
        }

        string exeDir = Path.Combine(AppContext.BaseDirectory, FileName);
        if (File.Exists(exeDir))
            return exeDir;

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JungleGame", FileName);
        return File.Exists(appData) ? appData : null;
    }

    /// <summary>
    /// The highest-weight move for the position. The side to move is part of the
    /// hash (the Zobrist turn key), so entries never mix sides.
    /// </summary>
    internal static bool TryGetMove(ulong hash, out Move move)
    {
        move = default;
        var entries = _entries;
        int lo = 0, hi = entries.Length - 1, first = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (entries[mid].Key < hash)
                lo = mid + 1;
            else
            {
                if (entries[mid].Key == hash)
                    first = mid;
                hi = mid - 1;
            }
        }

        if (first < 0)
            return false;

        // The key block is contiguous; pick the highest weight inside it.
        bool found = false;
        ushort bestWeight = 0;
        byte bestFrom = 0, bestTo = 0;
        for (int i = first; i < entries.Length && entries[i].Key == hash; i++)
        {
            if (entries[i].Weight > bestWeight)
            {
                found = true;
                bestWeight = entries[i].Weight;
                bestFrom = entries[i].From;
                bestTo = entries[i].To;
            }
        }

        if (!found)
            return false;

        move = new Move(
            new Position(bestFrom % 7, bestFrom / 7),
            new Position(bestTo % 7, bestTo / 7),
            null);
        return true;
    }

    /// <summary>Writes the current entries (may be empty) to path.</summary>
    internal static void Save(string path)
    {
        var entries = _entries;
        Array.Sort(entries, static (a, b) =>
        {
            int c = a.Key.CompareTo(b.Key);
            if (c != 0) return c;
            c = a.From.CompareTo(b.From);
            return c != 0 ? c : a.To.CompareTo(b.To);
        });

        var payload = new byte[entries.Length * 12];
        for (int i = 0; i < entries.Length; i++)
        {
            int off = i * 12;
            BitConverter.TryWriteBytes(payload.AsSpan(off, 8), entries[i].Key);
            payload[off + 8] = entries[i].From;
            payload[off + 9] = entries[i].To;
            BitConverter.TryWriteBytes(payload.AsSpan(off + 10, 2), entries[i].Weight);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write((ushort)Math.Min(entries.Length, ushort.MaxValue));
        writer.Write(SHA256.HashData(payload));
        writer.Write(payload);
    }

    /// <summary>Null on corruption (bad magic, version, or payload hash).</summary>
    internal static BookEntry[]? Load(string path)
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
            int count = reader.ReadUInt16();
            byte[] storedHash = reader.ReadBytes(32);
            byte[] payload = reader.ReadBytes(bytes.Length - HeaderSize);

            if (!SHA256.HashData(payload).AsSpan().SequenceEqual(storedHash))
                return null;
            if (payload.Length < count * 12)
                return null;

            var entries = new BookEntry[count];
            for (int i = 0; i < count; i++)
            {
                int off = i * 12;
                ulong key = BitConverter.ToUInt64(payload, off);
                byte from = payload[off + 8];
                byte to = payload[off + 9];
                ushort weight = BitConverter.ToUInt16(payload, off + 10);
                entries[i] = new BookEntry(key, from, to, weight);
            }
            return entries;
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

    /// <summary>
    /// Records a play (position hash, move) with a visit count — the builder
    /// hook used by the Bench book generator (--gen-book).
    /// </summary>
    internal static void Record(ulong hash, Move move)
    {
        byte from = (byte)(move.From.Row * 7 + move.From.Col);
        byte to = (byte)(move.To.Row * 7 + move.To.Col);
        lock (Gate)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Key == hash && _entries[i].From == from && _entries[i].To == to)
                {
                    if (_entries[i].Weight < ushort.MaxValue)
                        _entries[i] = new BookEntry(hash, from, to, (ushort)(_entries[i].Weight + 1));
                    return;
                }
            }
            _entries = _entries.Append(new BookEntry(hash, from, to, 1)).ToArray();
        }
    }

    /// <summary>Bulk-replaces the in-memory entries (the book builder's final step).</summary>
    internal static void ReplaceAll(IEnumerable<BookEntry> entries)
    {
        lock (Gate)
            _entries = entries.ToArray();
    }
}
