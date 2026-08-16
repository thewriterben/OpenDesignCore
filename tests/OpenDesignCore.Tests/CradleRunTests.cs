using OpenDesignCore.Import;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Scan-to-fit tests. The "scan" is synthetic — a voxel sphere meshed and
/// saved to STL by PicoGK itself — so the suite needs no external fixtures
/// and the input is watertight by construction.
/// </summary>
public sealed class CradleRunTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-cradle-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifactsDir => Path.Combine(_strTempDir, "artifacts");
    private string StrLedgerPath => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>A watertight synthetic scan: 8 mm radius sphere, mm units.</summary>
    private string StrMakeScanStl()
    {
        string strPath = Path.Combine(_strTempDir, "scan.stl");
        using Library oLib = new(0.4f);
        Voxels voxSphere = Voxels.voxSphere(oLib, new System.Numerics.Vector3(0, 0, 0), 8.0f);
        voxSphere.mshAsMesh().SaveToStlFile(strPath, Mesh.EStlUnit.MM);
        return strPath;
    }

    private CradleRunResult OExecute(string strStl, float fVoxelMm = 0.4f, float fWallMm = 2.4f)
        => CradleRun.Execute(
            strStlPath: strStl,
            eUnits: Mesh.EStlUnit.MM,
            fPostScale: 1.0f,
            fVoxelSizeMm: fVoxelMm,
            fClearanceMm: 0.40f,
            fWallMm: fWallMm,
            fSplitFraction: 0.45f,
            strArtifactsDir: StrArtifactsDir,
            strLedgerPath: StrLedgerPath,
            strCommit: "test-commit");

    [Fact]
    public void EndToEnd_CradleFromSyntheticScan()
    {
        string strScan = StrMakeScanStl();
        CradleRunResult oResult = OExecute(strScan);

        // Cradle artifact exists and re-hashes to its address.
        Assert.True(File.Exists(oResult.ArtifactPath));
        Assert.Equal(oResult.ArtifactSha256,
            ArtifactStore.StrSha256(File.ReadAllBytes(oResult.ArtifactPath)));

        // The raw scan itself was content-addressed into the store.
        string strScanStored = ArtifactStore.StrPathFor(StrArtifactsDir, oResult.ScanSha256, ".stl");
        Assert.True(File.Exists(strScanStored));
        Assert.Equal(File.ReadAllBytes(strScan), File.ReadAllBytes(strScanStored));

        // Sidecar chains cradle -> scan by hash and records declared units.
        string strSidecar = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifactsDir, oResult.ProvenanceSha256, ".provenance.json"));
        Assert.Contains($"\"scan_sha256\":\"{oResult.ScanSha256}\"", strSidecar);
        Assert.Contains("\"scan_declared_units\":\"mm\"", strSidecar);
        Assert.Contains("\"model\":\"scan-cradle/0.1\"", strSidecar);

        using Ledger oLedger = new(StrLedgerPath);
        Assert.Equal("scan-cradle/0.1", Assert.Single(oLedger.ARuns()).Model);
    }

    [Fact]
    public void SameScan_ProducesByteIdenticalCradle()
    {
        string strScan = StrMakeScanStl();
        CradleRunResult oFirst = OExecute(strScan);
        CradleRunResult oSecond = OExecute(strScan);

        Assert.Equal(oFirst.ArtifactSha256, oSecond.ArtifactSha256);
        Assert.Equal(oFirst.ProvenanceSha256, oSecond.ProvenanceSha256);
    }

    [Fact]
    public void AsciiStl_IsAccepted_AndMatchesTheBinaryEquivalent()
    {
        // KiCad's `pcb export stl` emits ASCII, which PicoGK 2.2.0 refuses
        // with NotImplementedException. The import boundary parses it, so the
        // OpenCircuitCore -> OpenDesignCore co-design bridge works on the
        // files KiCad actually produces.
        string strBinary = StrMakeScanStl();
        string strAscii = Path.Combine(_strTempDir, "scan-ascii.stl");
        WriteAsciiStlFrom(strBinary, strAscii);

        CradleRunResult oFromAscii = OExecute(strAscii);
        CradleRunResult oFromBinary = OExecute(strBinary);

        // Same geometry via two encodings -> same cradle.
        Assert.Equal(oFromBinary.ArtifactSha256, oFromAscii.ArtifactSha256);
        // ...but the recorded scan hashes differ: provenance tracks the bytes
        // that actually arrived, not an idea of them.
        Assert.NotEqual(oFromBinary.ScanSha256, oFromAscii.ScanSha256);
    }

    /// <summary>Transcode a binary STL to ASCII, the flavour KiCad emits.</summary>
    private static void WriteAsciiStlFrom(string strBinaryPath, string strAsciiPath)
    {
        byte[] ab = File.ReadAllBytes(strBinaryPath);
        uint nTriangles = BitConverter.ToUInt32(ab, 80);
        System.Text.StringBuilder oSb = new();
        oSb.AppendLine("solid odc-test");
        for (uint i = 0; i < nTriangles; i++)
        {
            int nBase = 84 + (int)i * 50 + 12; // skip the normal
            oSb.AppendLine("  facet normal 0 0 0");
            oSb.AppendLine("    outer loop");
            for (int v = 0; v < 3; v++)
            {
                float fX = BitConverter.ToSingle(ab, nBase + v * 12);
                float fY = BitConverter.ToSingle(ab, nBase + v * 12 + 4);
                float fZ = BitConverter.ToSingle(ab, nBase + v * 12 + 8);
                oSb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"      vertex {fX:R} {fY:R} {fZ:R}");
            }
            oSb.AppendLine("    endloop");
            oSb.AppendLine("  endfacet");
        }
        oSb.AppendLine("endsolid odc-test");
        File.WriteAllText(strAsciiPath, oSb.ToString());
    }

    [Fact]
    public void AutoUnits_AreRefused()
    {
        string strScan = StrMakeScanStl();
        Assert.Throws<ImportValidationException>(() => CradleRun.Execute(
            strScan, Mesh.EStlUnit.AUTO, 1.0f, 0.4f, 0.4f, 2.4f, 0.45f,
            StrArtifactsDir, StrLedgerPath, "test-commit"));
    }

    [Fact]
    public void BelowResolutionFloor_RefusesLoudly()
    {
        string strScan = StrMakeScanStl();
        // clearance 0.4 mm -> floor min(wall/2, clearance) = 0.4; 0.5 mm voxels refuse.
        Assert.Throws<ResolutionFloorException>(() => OExecute(strScan, fVoxelMm: 0.5f));
    }

    [Fact]
    public void MissingFile_FailsAtImportBoundary()
    {
        Assert.Throws<ImportValidationException>(() => OExecute(
            Path.Combine(_strTempDir, "nope.stl")));
    }
}
