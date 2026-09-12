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

    private CradleRunResult OExecute(
        string strStl,
        float fVoxelMm = 0.4f,
        float fWallMm = 2.4f,
        EScanOrigin eOrigin = EScanOrigin.MetrologyScan,
        ScaleReference? oScaleRef = null)
        => CradleRun.Execute(
            strStlPath: strStl,
            eUnits: Mesh.EStlUnit.MM,
            fPostScale: 1.0f,
            // The synthetic sphere stands in for an instrument scan: it has a
            // real size and nothing had to establish it. Photogrammetry is
            // exercised explicitly in the ADR-0019 tests below.
            eOrigin: eOrigin,
            oScaleRef: oScaleRef,
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

    // ---- ADR-0019: units are not scale -------------------------------------

    [Fact]
    public void Photogrammetry_WithoutAScaleReference_IsRefused()
    {
        string strScan = StrMakeScanStl();

        // The mesh is watertight, the units are declared, the voxel size is
        // legal — everything that used to be checked passes. What is missing is
        // the thing that gave it an absolute size, and before ADR-0019 nothing
        // asked.
        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => OExecute(strScan, eOrigin: EScanOrigin.Photogrammetry));

        Assert.Contains("scale-free", e.Message);
        Assert.Contains("Absence is UNKNOWN, not 1:1", e.Message);
    }

    [Fact]
    public void Photogrammetry_WithAScaleReference_RecordsItInTheSidecar()
    {
        string strScan = StrMakeScanStl();
        CradleRunResult oResult = OExecute(
            strScan,
            eOrigin: EScanOrigin.Photogrammetry,
            // The sphere is 16 mm across in file units; calling that 16 mm
            // means a 1:1 derived scale, so the cradle geometry is unchanged
            // and this test is about the record, not the resizing.
            oScaleRef: new ScaleReference
            {
                Description = "16 mm gauge block across the turntable, digital caliper",
                LengthMm = 16.0,
                SpanFileUnits = 16.0,
            });

        string strSidecar = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifactsDir, oResult.ProvenanceSha256, ".provenance.json"));

        Assert.Contains("\"scan_origin\":\"photogrammetry\"", strSidecar);
        Assert.Contains("\"length_mm\":\"16.0000\"", strSidecar);
        Assert.Contains("\"span_file_units\":\"16.0000\"", strSidecar);
        Assert.Contains("gauge block across the turntable", strSidecar);
        Assert.Contains("\"schema\":\"odc/provenance/0.3\"", strSidecar);

        // The record says where the scale came from, because "1.0000" alone
        // cannot distinguish a derived identity from a defaulted one.
        Assert.Contains("\"scan_scale_source\":\"derived from scale_reference\"", strSidecar);
    }

    /// <summary>
    /// The one that would have caught the gap: the reference does not describe
    /// the scale, it *is* the scale (ADR-0020). A 32 mm reference spanning 16
    /// file units must produce a mesh twice the file's size.
    /// </summary>
    [Fact]
    public void Photogrammetry_DerivesTheScaleFromTheReference()
    {
        string strScan = StrMakeScanStl();   // sphere, 16 mm across in file units

        using Library oLib = new(0.4f);
        ScanImportResult oAtOne = ScanImport.OImport(
            oLib, strScan, Mesh.EStlUnit.MM, 1.0f,
            EScanOrigin.Photogrammetry,
            new ScaleReference
            {
                Description = "16 mm gauge block, caliper",
                LengthMm = 16.0,
                SpanFileUnits = 16.0,
            },
            StrArtifactsDir);

        ScanImportResult oAtTwo = ScanImport.OImport(
            oLib, strScan, Mesh.EStlUnit.MM, 1.0f,
            EScanOrigin.Photogrammetry,
            new ScaleReference
            {
                Description = "32 mm gauge block, caliper",
                LengthMm = 32.0,
                SpanFileUnits = 16.0,
            },
            StrArtifactsDir);

        Assert.Equal(1.0f, oAtOne.EffectiveScale, 3);
        Assert.Equal(2.0f, oAtTwo.EffectiveScale, 3);
        Assert.Equal(oAtOne.SizeMm.X * 2.0f, oAtTwo.SizeMm.X, 2);
        Assert.Equal(oAtOne.SizeMm.Z * 2.0f, oAtTwo.SizeMm.Z, 2);
    }

    [Fact]
    public void Photogrammetry_DerivationAccountsForDeclaredUnits()
    {
        // The span is measured in raw file coordinates, so the unit conversion
        // PicoGK applies has to be divided back out. Declaring CM must not
        // multiply the derived size by ten.
        string strScan = StrMakeScanStl();

        using Library oLib = new(0.4f);
        ScanImportResult oCm = ScanImport.OImport(
            oLib, strScan, Mesh.EStlUnit.CM, 1.0f,
            EScanOrigin.Photogrammetry,
            new ScaleReference
            {
                Description = "32 mm gauge block, caliper",
                LengthMm = 32.0,
                SpanFileUnits = 16.0,
            },
            StrArtifactsDir);

        // 16 file units declared as the 32 mm reference → 32 mm across,
        // whatever the unit label says.
        Assert.Equal(32.0f, oCm.SizeMm.X, 1);
    }

    [Fact]
    public void Photogrammetry_WithoutASpan_IsRefused()
    {
        string strScan = StrMakeScanStl();

        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => OExecute(
                strScan,
                eOrigin: EScanOrigin.Photogrammetry,
                oScaleRef: new ScaleReference
                {
                    Description = "10 mm gauge block, caliper",
                    LengthMm = 10.0,
                }));

        Assert.Contains("what it spans", e.Message);
        Assert.Contains("reads as rigour and is not", e.Message);
    }

    [Fact]
    public void ASpanOnANonPhotogrammetryOrigin_IsRefused()
    {
        string strScan = StrMakeScanStl();

        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => OExecute(
                strScan,
                eOrigin: EScanOrigin.MetrologyScan,
                oScaleRef: new ScaleReference
                {
                    Description = "10 mm gauge block, caliper",
                    LengthMm = 10.0,
                    SpanFileUnits = 5.0,
                }));

        Assert.Contains("second source for one quantity", e.Message);
    }

    [Fact]
    public void Photogrammetry_WithAnExplicitScale_IsRefused()
    {
        string strScan = StrMakeScanStl();

        using Library oLib = new(0.4f);
        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => ScanImport.OImport(
                oLib, strScan, Mesh.EStlUnit.MM, 2.0f,   // an explicit scale
                EScanOrigin.Photogrammetry,
                new ScaleReference
                {
                    Description = "16 mm gauge block, caliper",
                    LengthMm = 16.0,
                    SpanFileUnits = 16.0,
                },
                StrArtifactsDir));

        Assert.Contains("takes no explicit scale", e.Message);
    }

    [Fact]
    public void CadExport_WithAScaleReference_IsRefused()
    {
        string strScan = StrMakeScanStl();

        // Not a harmless extra field: recording a reference here asserts a
        // measurement that was never made, against a mesh whose units were
        // authoritative to begin with.
        ImportValidationException e = Assert.Throws<ImportValidationException>(
            () => OExecute(
                strScan,
                eOrigin: EScanOrigin.CadExport,
                oScaleRef: new ScaleReference { Description = "caliper", LengthMm = 10.0 }));

        Assert.Contains("units are authoritative", e.Message);
    }

    [Fact]
    public void AScaleReference_WithoutAPositiveLength_IsRefused()
    {
        // The description is free text on purpose, so the measured length is
        // the field that cannot be satisfied by typing a word.
        Assert.Throws<ImportValidationException>(
            () => new ScaleReference { Description = "a ruler, I think", LengthMm = 0 }.Validate());

        Assert.Throws<ImportValidationException>(() => ScaleReference.OParse("0:a ruler"));
        Assert.Throws<ImportValidationException>(() => ScaleReference.OParse("no-colon-here"));
        Assert.Throws<ImportValidationException>(() => ScaleReference.OParse("10.0:   "));
    }

    [Fact]
    public void ScaleReference_RoundTripsThroughItsSurfaceForm()
    {
        ScaleReference oRef = ScaleReference.OParse(
            "10.5:gauge block across the turntable, digital caliper");

        Assert.Equal(10.5, oRef.LengthMm);
        Assert.Equal("gauge block across the turntable, digital caliper", oRef.Description);
    }

    [Fact]
    public void MetrologyScan_NeedsNoScaleReference_ButMayCarryOne()
    {
        string strScan = StrMakeScanStl();

        CradleRunResult oWithout = OExecute(strScan, eOrigin: EScanOrigin.MetrologyScan);
        string strSidecar = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifactsDir, oWithout.ProvenanceSha256, ".provenance.json"));

        // Present and null, not absent: a reader must be able to tell "none was
        // needed" from "nobody said".
        Assert.Contains("\"scan_origin\":\"metrology-scan\"", strSidecar);
        Assert.Contains("\"scan_scale_reference\":null", strSidecar);
    }

    [Fact]
    public void UnknownOriginString_IsRefusedRatherThanDefaulted()
    {
        Assert.False(ScanProvenance.BTryParseOrigin("photogrametry", out _));  // misspelled
        Assert.False(ScanProvenance.BTryParseOrigin("", out _));
        Assert.False(ScanProvenance.BTryParseOrigin(null, out _));
        Assert.False(ScanProvenance.BTryParseOrigin("Photogrammetry", out _)); // case is exact
        Assert.True(ScanProvenance.BTryParseOrigin("photogrammetry", out EScanOrigin e));
        Assert.Equal(EScanOrigin.Photogrammetry, e);
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
            strScan, Mesh.EStlUnit.AUTO, 1.0f, EScanOrigin.MetrologyScan, null,
            0.4f, 0.4f, 2.4f, 0.45f,
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
