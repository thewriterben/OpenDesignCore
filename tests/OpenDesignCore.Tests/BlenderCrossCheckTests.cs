using System.Text.Json;
using OpenDesignCore.Provenance;
using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The cross-check's logic against fixture numbers (CI has no Blender), plus one
/// test against the real thing that returns early when Blender is not installed —
/// the repo's convention for checks that need a neighbour.
/// </summary>
public sealed class BlenderCrossCheckTests
{
    // Run 50 (2026-09-07): the FireBeetle 2 tray, voxel 0.30 mm. Sidecar and
    // Blender 5.2.1 numbers as measured; a known answer, not a pinned output.
    private static readonly SidecarClaims s_oRun50 = new()
    {
        ArtifactSha256 = "06ac53848bc575284a835b83be4f033c7aef433ecd75be8ec974b3548aadf824",
        VoxelSizeMm = 0.30,
        BboxX = 30.90,
        BboxY = 66.87,
        BboxZ = 11.15,
        VolumeCubicMm = 8843.81,
    };

    private static BlenderMeasurement ORun50Blender(double fVolume = 8854.27, int nNonManifold = 0) => new()
    {
        BlenderVersion = "5.2.1 LTS",
        Vertices = 88356,
        Faces = 176708,
        BboxX = 30.90,
        BboxY = 66.87,
        BboxZ = 11.15,
        VolumeCubicMm = fVolume,
        NonManifoldEdges = nNonManifold,
        BoundaryEdges = 0,
    };

    [Fact]
    public void TwoKernelsAgreeingWithinTolerance_Passes()
    {
        IReadOnlyList<ClaimCheck> a = BlenderCrossCheck.ACompare(s_oRun50, ORun50Blender(), fBboxTolMm: 0.6, fVolumeTolPct: 1.0);
        Assert.All(a, c => Assert.True(c.Agrees, c.Claim));
        // +0.12 % on volume is what a voxel shell at 0.3 mm looks like.
        ClaimCheck oVol = Assert.Single(a, c => c.Claim == "volume_cubic_mm");
        Assert.InRange(oVol.Delta / oVol.Sidecar * 100.0, 0.10, 0.14);
    }

    [Fact]
    public void VolumeOutsideTheDeclaredTolerance_Fails()
    {
        IReadOnlyList<ClaimCheck> a = BlenderCrossCheck.ACompare(s_oRun50, ORun50Blender(), fBboxTolMm: 0.6, fVolumeTolPct: 0.05);
        Assert.False(a.Single(c => c.Claim == "volume_cubic_mm").Agrees);
        Assert.True(a.Single(c => c.Claim == "bbox_mm.x").Agrees, "the other claims still judged on their own");
    }

    [Fact]
    public void ANonManifoldMesh_FailsTheRecordEvenWhenNumbersAgree()
    {
        BlenderMeasurement m = ORun50Blender(nNonManifold: 3);
        var a = BlenderCrossCheck.ACompare(s_oRun50, m, 0.6, 1.0);
        Dictionary<string, object?> o = BlenderCrossCheck.OBuildRecord(
            50, s_oRun50, "sidecar-hash", m, a, 0.6, "2 x voxel_size_mm (0.300)", 1.0, "blender.exe", "script-hash", "test");
        Assert.False((bool)o["passed"]!);
        Assert.False(BlenderCrossCheck.BManifold(m));
    }

    [Fact]
    public void DefaultBboxTolerance_IsTwiceTheVoxel()
    {
        Assert.Equal(0.6, BlenderCrossCheck.FDefaultBboxToleranceMm(0.3), precision: 9);
    }

    [Fact]
    public void ToleranceMustBePositive()
    {
        Assert.Throws<CrossCheckException>(() => BlenderCrossCheck.ACompare(s_oRun50, ORun50Blender(), 0, 1.0));
        Assert.Throws<CrossCheckException>(() => BlenderCrossCheck.ACompare(s_oRun50, ORun50Blender(), 0.6, 0));
    }

    [Fact]
    public void Record_IsCanonicalAndCarriesBothNumbersAndTheToleranceDerivation()
    {
        BlenderMeasurement m = ORun50Blender();
        var a = BlenderCrossCheck.ACompare(s_oRun50, m, 0.6, 1.0);
        Dictionary<string, object?> o = BlenderCrossCheck.OBuildRecord(
            50, s_oRun50, "sidecar-hash", m, a, 0.6, "2 x voxel_size_mm (0.300)", 1.0, "blender.exe", "script-hash", "test");

        byte[] ab1 = CanonicalJson.Serialize(o);
        byte[] ab2 = CanonicalJson.Serialize(o);
        Assert.Equal(ab1, ab2);

        using JsonDocument oDoc = JsonDocument.Parse(ab1);
        JsonElement r = oDoc.RootElement;
        Assert.Equal("odc/verification/0.1", r.GetProperty("schema").GetString());
        Assert.Equal("2 x voxel_size_mm (0.300)", r.GetProperty("inputs").GetProperty("bbox_tolerance_source").GetString());
        Assert.Equal("5.2.1 LTS", r.GetProperty("versions").GetProperty("blender").GetString());
        JsonElement oVol = r.GetProperty("claims").EnumerateArray().Single(c => c.GetProperty("claim").GetString() == "volume_cubic_mm");
        Assert.Equal("8843.810", oVol.GetProperty("sidecar").GetString());
        Assert.Equal("8854.270", oVol.GetProperty("blender").GetString());
        Assert.True(r.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void MeasureScript_IsEmbeddedAndHashed()
    {
        (string strText, string strSha) = BlenderCrossCheck.MeasureScript();
        Assert.Contains("__ODC_RESULT__", strText);
        Assert.Equal(64, strSha.Length);
    }

    [Fact]
    public void SidecarClaims_ReadTheStringifiedLengths()
    {
        SidecarClaims c = SidecarClaims.OParse("""
            {"schema":"odc/provenance/0.3","voxel_size_mm":"0.30",
             "artifact":{"sha256":"abc","bbox_mm":{"x":"30.90","y":"66.87","z":"11.15"},"volume_cubic_mm":"8843.81"}}
            """);
        Assert.Equal(0.30, c.VoxelSizeMm);
        Assert.Equal(66.87, c.BboxY);
        Assert.Equal(8843.81, c.VolumeCubicMm);
    }

    /// <summary>
    /// The real thing, when Blender is on this machine: the known answer for
    /// run 50's artifact. Returns early otherwise — CI runs on a Windows image
    /// with no Blender, and a check that cannot run must not look like one that
    /// passed.
    /// </summary>
    [Fact]
    public void RealBlender_MeasuresRun50sArtifactToTheKnownAnswer()
    {
        string? strBlender = Environment.GetEnvironmentVariable("ODC_BLENDER");
        if (string.IsNullOrEmpty(strBlender) || !File.Exists(strBlender)) return;
        string strStl = Path.Combine(TestRegistry.StrRepoRoot(), "artifacts", "06",
            "06ac53848bc575284a835b83be4f033c7aef433ecd75be8ec974b3548aadf824.stl");
        if (!File.Exists(strStl)) return;

        BlenderMeasurement m = BlenderCrossCheck.OMeasure(strBlender, strStl, TimeSpan.FromMinutes(5));
        Assert.Equal(30.90, m.BboxX, 0.005);
        Assert.Equal(66.87, m.BboxY, 0.005);
        Assert.Equal(11.15, m.BboxZ, 0.005);
        Assert.InRange(m.VolumeCubicMm, 8843.81 * 0.995, 8843.81 * 1.005);
        Assert.True(BlenderCrossCheck.BManifold(m));
    }
}
