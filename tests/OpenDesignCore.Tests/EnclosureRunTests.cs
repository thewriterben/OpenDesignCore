using System.Globalization;
using System.Text.Json;
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

    /// <summary>
    /// The sidecar must describe the artifact, not only its inputs. Before
    /// schema 0.2 it recorded the *part* envelope and never the size of the
    /// thing produced, so a consumer holding the record could not answer
    /// "does this fit a 220 mm bed" without re-parsing the STL.
    ///
    /// Expected outer dimensions are recomputed here from the recorded inputs
    /// rather than taken from the model, so this checks the recorded number
    /// against geometry rather than against the code that produced it.
    /// </summary>
    [Fact]
    public void Sidecar_RecordsTheArtifactsOwnBoundingBox()
    {
        const float fVoxelMm = 0.5f;
        const float fClearance = 0.30f;
        const float fWall = 2.40f;

        EnclosureRunResult oResult = OExecute(fVoxelMm);
        JsonElement oSidecar = OReadSidecar(oResult.ProvenanceSha256);

        Assert.Equal("odc/provenance/0.2", oSidecar.GetProperty("schema").GetString());

        JsonElement oEnvelope = oSidecar.GetProperty("inputs").GetProperty("part_envelope_mm");
        double dEx = FMm(oEnvelope, "x");
        double dEy = FMm(oEnvelope, "y");
        double dEz = FMm(oEnvelope, "z");

        JsonElement oBBox = oSidecar.GetProperty("artifact").GetProperty("bbox_mm");
        double dTol = 2 * fVoxelMm;

        Assert.Equal(dEx + 2 * fClearance + 2 * fWall, FMm(oBBox, "x"), tolerance: dTol);
        Assert.Equal(dEy + 2 * fClearance + 2 * fWall, FMm(oBBox, "y"), tolerance: dTol);
        Assert.Equal(fWall + dEz + fClearance, FMm(oBBox, "z"), tolerance: dTol);

        // Volume is enclosed material, so it must be well under the bounding
        // box of a tray that is mostly cavity.
        double dVolume = double.Parse(
            oSidecar.GetProperty("artifact").GetProperty("volume_cubic_mm").GetString()!,
            CultureInfo.InvariantCulture);
        double dBoxVolume = FMm(oBBox, "x") * FMm(oBBox, "y") * FMm(oBBox, "z");
        Assert.True(dVolume > 0, "an exported artifact encloses some material");
        Assert.True(dVolume < dBoxVolume,
            $"volume {dVolume} should be less than its bounding box {dBoxVolume}");
    }

    /// <summary>
    /// Lengths in the sidecar are unit-keyed strings, never JSON floats:
    /// float text form is not stable across languages and this record is
    /// hash-compared against Python.
    /// </summary>
    [Fact]
    public void RecordedDimensions_AreStringsNotFloats()
    {
        EnclosureRunResult oResult = OExecute();
        JsonElement oBBox = OReadSidecar(oResult.ProvenanceSha256)
            .GetProperty("artifact").GetProperty("bbox_mm");

        foreach (string strAxis in new[] { "x", "y", "z" })
            Assert.Equal(JsonValueKind.String, oBBox.GetProperty(strAxis).ValueKind);
    }

    private JsonElement OReadSidecar(string strProvenanceSha256)
    {
        string strPath = ArtifactStore.StrPathFor(
            Path.Combine(_strTempDir, "artifacts"), strProvenanceSha256, ".provenance.json");
        return JsonDocument.Parse(File.ReadAllBytes(strPath)).RootElement.Clone();
    }

    private static double FMm(JsonElement oParent, string strKey)
        => double.Parse(oParent.GetProperty(strKey).GetString()!, CultureInfo.InvariantCulture);

    [Fact]
    public void BelowResolutionFloor_RefusesLoudly()
    {
        // 2.4 mm wall -> floor is 1.2 mm; 1.5 mm voxels must refuse.
        ResolutionFloorException oEx =
            Assert.Throws<ResolutionFloorException>(() => OExecute(fVoxelMm: 1.5f));
        Assert.Contains("resolution floor", oEx.Message);
    }
}
