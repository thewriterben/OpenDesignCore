using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// End-to-end thin-thread tests. These exercise the native PicoGK runtime
/// (bundled in the NuGet package) headlessly via a scoped Library instance.
/// </summary>
public sealed class EnclosureRunTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-run-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private static string StrRepoDataDir()
    {
        DirectoryInfo? oDir = new(AppContext.BaseDirectory);
        while (oDir is not null && !File.Exists(Path.Combine(oDir.FullName, "OpenDesignCore.sln")))
            oDir = oDir.Parent;
        Assert.NotNull(oDir);
        return Path.Combine(oDir.FullName, "data");
    }

    private EnclosureRunResult OExecute(float fVoxelMm = 0.5f)
        => EnclosureRun.Execute(
            strDataDir: StrRepoDataDir(),
            strPartId: "parts/esp32-s3-wroom-1",
            fVoxelSizeMm: fVoxelMm,
            fClearanceMm: 0.30f,
            fWallMm: 2.40f,
            strArtifactsDir: Path.Combine(_strTempDir, "artifacts"),
            strLedgerPath: Path.Combine(_strTempDir, "ledger.db"),
            strCommit: "test-commit");

    [Fact]
    public void EndToEnd_ProducesArtifactSidecarAndLedgerRow()
    {
        EnclosureRunResult oResult = OExecute();

        // Artifact exists and its content re-hashes to its address.
        Assert.True(File.Exists(oResult.ArtifactPath));
        byte[] abStl = File.ReadAllBytes(oResult.ArtifactPath);
        Assert.Equal(oResult.ArtifactSha256, ArtifactStore.StrSha256(abStl));
        Assert.True(abStl.Length > 84, "binary STL must contain triangles");

        // Sidecar exists, re-hashes, and is deterministic ASCII.
        string strSidecarPath = ArtifactStore.StrPathFor(
            Path.Combine(_strTempDir, "artifacts"), oResult.ProvenanceSha256, ".provenance.json");
        Assert.True(File.Exists(strSidecarPath));
        byte[] abSidecar = File.ReadAllBytes(strSidecarPath);
        Assert.Equal(oResult.ProvenanceSha256, ArtifactStore.StrSha256(abSidecar));
        string strSidecar = System.Text.Encoding.ASCII.GetString(abSidecar);
        Assert.Contains("\"sha256\":\"" + oResult.ArtifactSha256 + "\"", strSidecar);
        Assert.Contains("\"voxel_size_mm\":\"0.50\"", strSidecar);
        Assert.DoesNotContain("created", strSidecar); // timestamps live in the ledger only

        // Ledger row recorded.
        using Ledger oLedger = new(Path.Combine(_strTempDir, "ledger.db"));
        RunRecord oRun = Assert.Single(oLedger.ARuns());
        Assert.Equal(oResult.ArtifactSha256, oRun.ArtifactSha256);
        Assert.Equal(oResult.ProvenanceSha256, oRun.ProvenanceSha256);
        Assert.True(oRun.Passed);
    }

    [Fact]
    public void SameInputs_ProduceByteIdenticalArtifactAndSidecar()
    {
        EnclosureRunResult oFirst = OExecute();
        EnclosureRunResult oSecond = OExecute();

        Assert.Equal(oFirst.ArtifactSha256, oSecond.ArtifactSha256);
        Assert.Equal(oFirst.ProvenanceSha256, oSecond.ProvenanceSha256);
    }

    [Fact]
    public void BelowResolutionFloor_RefusesLoudly()
    {
        // 2.4 mm wall -> floor is 1.2 mm; 1.5 mm voxels must refuse.
        ResolutionFloorException oEx =
            Assert.Throws<ResolutionFloorException>(() => OExecute(fVoxelMm: 1.5f));
        Assert.Contains("resolution floor", oEx.Message);
    }
}
