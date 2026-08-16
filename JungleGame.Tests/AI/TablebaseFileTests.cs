using System.Security.Cryptography;
using JungleGame.Core.AI;

namespace JungleGame.Tests.AI;

/// <summary>
/// File format round-trips and corruption rejection. The round-trip test uses
/// zero-filled WDL/DTM sections (entry values are not validated on load), so
/// no tablebase build is involved.
/// </summary>
[Collection("Tablebase")]
public class TablebaseFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"jungle3-test-{Guid.NewGuid():N}.tb");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private static TablebaseBuilder.BuildResult ZeroResult(bool withDtm)
    {
        int wdl2Bytes = TablebaseIndex.Combo2Count * TablebaseIndex.EntriesPerCombo2;
        int wdl3Bytes = TablebaseIndex.Combo3Count * TablebaseIndex.EntriesPerCombo3;
        var result = new TablebaseBuilder.BuildResult
        {
            Wdl2 = new byte[wdl2Bytes],
            Wdl3 = new byte[wdl3Bytes],
        };
        if (withDtm)
        {
            result.Dtm2 = new byte[wdl2Bytes];
            result.Dtm3 = new byte[wdl3Bytes];
        }
        return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveLoad_RoundTrips(bool withDtm)
    {
        var result = ZeroResult(withDtm);
        TablebaseFile.Save(_path, result);

        var loaded = TablebaseFile.Load(_path);
        Assert.NotNull(loaded);
        // The on-disk WDL sections are packed at 2 bits per entry.
        Assert.Equal(TablebaseFile.Wdl2ByteCount, loaded!.Wdl2.Length);
        Assert.Equal(TablebaseFile.Wdl3ByteCount, loaded.Wdl3.Length);
        Assert.Equal(withDtm, loaded.Dtm2 != null);
        Assert.Equal(withDtm, loaded.Dtm3 != null);
        if (withDtm)
        {
            Assert.Equal(result.Wdl2.Length, loaded.Dtm2!.Length);
            Assert.Equal(result.Wdl3.Length, loaded.Dtm3!.Length);
        }
    }

    [Fact]
    public void Load_CorruptedHash_ReturnsNull()
    {
        TablebaseFile.Save(_path, ZeroResult(withDtm: false));

        // Flip a payload byte: the stored hash must reject the file.
        var bytes = File.ReadAllBytes(_path);
        bytes[bytes.Length - 1] ^= 0xFF;
        File.WriteAllBytes(_path, bytes);

        Assert.Null(TablebaseFile.Load(_path));
    }

    [Fact]
    public void Load_BadMagic_ReturnsNull()
    {
        File.WriteAllBytes(_path, new byte[128]);
        Assert.Null(TablebaseFile.Load(_path));
    }

    [Fact]
    public void Load_Truncated_ReturnsNull()
    {
        File.WriteAllBytes(_path, new byte[16]);
        Assert.Null(TablebaseFile.Load(_path));
    }

    [Fact]
    public void PackedWdl_RoundTripsThroughSaveLoad()
    {
        // Fill known WDL values and verify they come back through the file.
        var result = ZeroResult(withDtm: false);
        for (int i = 0; i < result.Wdl2.Length; i++)
            result.Wdl2[i] = (byte)(1 + (i % 3)); // 1,2,3 pattern
        TablebaseFile.Save(_path, result);

        var loaded = TablebaseFile.Load(_path);
        Assert.NotNull(loaded);
        for (int i = 0; i < 100; i++)
            Assert.Equal(result.Wdl2[i], TablebaseFile.GetWdl(loaded!.Wdl2, i));
    }

    [Fact]
    public void FileHash_MatchesPayload()
    {
        // The SHA-256 in the header covers the payload: verify it matches the
        // file's own bytes for a hand-constructed file.
        TablebaseFile.Save(_path, ZeroResult(withDtm: false));
        var bytes = File.ReadAllBytes(_path);
        byte[] storedHash = bytes[32..64];
        byte[] payload = bytes[64..];
        Assert.Equal(SHA256.HashData(payload), storedHash);
    }
}
