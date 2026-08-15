using OpenDesignCore.Provenance;
using Xunit;

namespace OpenDesignCore.Tests;

public sealed class LedgerTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-ledger-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private static RunRecord ORecord(string strModel) => new()
    {
        CreatedUtc = "2026-08-15T00:00:00.0000000Z",
        Model = strModel,
        VoxelSizeMm = "0.20",
        InputsJson = """{"part_id":"parts/esp32-s3-wroom-1"}""",
        VersionsJson = """{"picogk":"2.2.0","tool":"0.1.0"}""",
        ArtifactSha256 = new string('a', 64),
        ProvenanceSha256 = new string('b', 64),
        Passed = true,
    };

    [Fact]
    public void AppendsAndReadsBackInOrder()
    {
        string strDb = Path.Combine(_strTempDir, "ledger.db");
        using Ledger oLedger = new(strDb);

        long nFirst = oLedger.NAppend(ORecord("model-a"));
        long nSecond = oLedger.NAppend(ORecord("model-b"));
        Assert.True(nSecond > nFirst);

        IReadOnlyList<RunRecord> aRuns = oLedger.ARuns();
        Assert.Equal(2, aRuns.Count);
        Assert.Equal("model-a", aRuns[0].Model);
        Assert.Equal("model-b", aRuns[1].Model);
        Assert.Equal("0.20", aRuns[0].VoxelSizeMm);
        Assert.True(aRuns[0].Passed);
    }

    [Fact]
    public void PersistsAcrossReopen()
    {
        string strDb = Path.Combine(_strTempDir, "ledger2.db");
        using (Ledger oLedger = new(strDb))
        {
            oLedger.NAppend(ORecord("model-a"));
        }

        using Ledger oReopened = new(strDb);
        Assert.Single(oReopened.ARuns());
    }
}
