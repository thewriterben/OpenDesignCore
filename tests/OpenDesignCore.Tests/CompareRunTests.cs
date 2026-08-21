using System.Numerics;
using OpenDesignCore.Runs;
using OpenDesignCore.Verification;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The synthetic "print": a design mesh scaled by a known factor. If the
/// comparison cannot recover a shrinkage it was handed deliberately, it will
/// not recover a real one either.
/// </summary>
public sealed class CompareRunTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-compare-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    /// <summary>A box, and the same box scaled — a print that shrank uniformly.</summary>
    private (string strDesign, string strScan) MakePair(float fScale)
    {
        string strDesign = Path.Combine(_strTempDir, $"design-{fScale}.stl");
        string strScan = Path.Combine(_strTempDir, $"scan-{fScale}.stl");
        using Library oLib = new(0.2f);

        Mesh mshDesign = Utils.mshCreateCube(oLib, new Vector3(20, 30, 10), Vector3.Zero);
        mshDesign.SaveToStlFile(strDesign, Mesh.EStlUnit.MM);

        Mesh mshScan = mshDesign.mshCreateTransformed(Matrix4x4.CreateScale(fScale));
        mshScan.SaveToStlFile(strScan, Mesh.EStlUnit.MM);
        return (strDesign, strScan);
    }

    private CompareRunResult OExecute(
        string strDesign, string strScan, float fVoxelMm = 0.2f, float fScanAccuracyMm = 0f)
        => CompareRun.Execute(
            strDesign, strScan, Mesh.EStlUnit.MM, fVoxelMm,
            Path.Combine(_strTempDir, "artifacts"),
            Path.Combine(_strTempDir, "ledger.db"),
            "test-commit", "pla",
            fScanAccuracyMm);

    [Fact]
    public void RecoversAKnownUniformShrinkage()
    {
        // 0.3% shrink — the low end of PLA's published range.
        (string strDesign, string strScan) = MakePair(0.997f);
        CompareRunResult oResult = OExecute(strDesign, strScan);

        Assert.Equal(-0.3, oResult.Report.MeanDeviationPct, precision: 1);
        Assert.All(oResult.Report.Axes, a => Assert.True(a.DeviationMm < 0, "every axis should shrink"));
        // Uniform scaling: the axes must agree, so a single factor is valid.
        Assert.True(oResult.Report.DeviationPctSpread < 0.05,
            $"expected agreement across axes, spread was {oResult.Report.DeviationPctSpread}");
    }

    [Fact]
    public void IdenticalMeshesDeviateByNothing()
    {
        (string strDesign, string strScan) = MakePair(1.0f);
        CompareRunResult oResult = OExecute(strDesign, strScan, fScanAccuracyMm: 0.05f);

        Assert.True(oResult.Report.MaxAbsDeviationMm < 0.001);
        Assert.True(oResult.Report.WithinScanAccuracy);
    }

    [Fact]
    public void DeviationUnderTheDeclaredScannerAccuracyIsNotCalledShrinkage()
    {
        // 30 mm at 0.01% is 3 microns. A scanner good to 0.05 mm cannot see
        // that, so calling it shrinkage would be reporting instrument noise.
        (string strDesign, string strScan) = MakePair(0.9999f);
        CompareRunResult oResult = OExecute(strDesign, strScan, fScanAccuracyMm: 0.05f);

        Assert.True(oResult.Report.WithinScanAccuracy,
            $"max deviation {oResult.Report.MaxAbsDeviationMm} mm is under the declared 0.05 mm");
    }

    [Fact]
    public void WithoutADeclaredAccuracySignificanceIsUnknownNotFalse()
    {
        // The bug this replaced: significance was judged against the voxel
        // size, which never touches the extents. With nothing declared, the
        // honest answer is "unknown" — and the run does not count as passed.
        (string strDesign, string strScan) = MakePair(0.997f);
        CompareRunResult oResult = OExecute(strDesign, strScan);

        Assert.Null(oResult.Report.WithinScanAccuracy);
        Assert.False(oResult.Report.AccuracyDeclared);

        using OpenDesignCore.Provenance.Ledger oLedger =
            new(Path.Combine(_strTempDir, "ledger.db"));
        Assert.False(Assert.Single(oLedger.ARuns()).Passed);
    }

    [Fact]
    public void RealDeviationAboveTheDeclaredAccuracyIsReported()
    {
        // 0.3% of 30 mm is 90 microns — well above a 0.05 mm scanner.
        (string strDesign, string strScan) = MakePair(0.997f);
        CompareRunResult oResult = OExecute(strDesign, strScan, fScanAccuracyMm: 0.05f);

        Assert.False(oResult.Report.WithinScanAccuracy);
        Assert.True(oResult.Report.MaxAbsDeviationMm > 0.05);
    }

    [Fact]
    public void AnisotropicScalingShowsUpAsSpread()
    {
        // A print that shrank differently in Z — the case where a single
        // isotropic compensation is the wrong correction.
        string strDesign = Path.Combine(_strTempDir, "aniso-design.stl");
        string strScan = Path.Combine(_strTempDir, "aniso-scan.stl");
        using (Library oLib = new(0.2f))
        {
            Mesh mshDesign = Utils.mshCreateCube(oLib, new Vector3(20, 30, 10), Vector3.Zero);
            mshDesign.SaveToStlFile(strDesign, Mesh.EStlUnit.MM);
            Mesh mshScan = mshDesign.mshCreateTransformed(
                Matrix4x4.CreateScale(0.998f, 0.998f, 0.985f));
            mshScan.SaveToStlFile(strScan, Mesh.EStlUnit.MM);
        }

        CompareRunResult oResult = OExecute(strDesign, strScan);

        Assert.True(oResult.Report.DeviationPctSpread > 1.0,
            $"expected the axes to disagree, spread was {oResult.Report.DeviationPctSpread}");
        AxisDeviation oZ = oResult.Report.Axes.Single(a => a.Axis == "z");
        Assert.True(oZ.DeviationPct < -1.0, $"z should shrink most, was {oZ.DeviationPct}");
    }

    [Fact]
    public void ComparisonIsRecordedWithBothInputsHashChained()
    {
        (string strDesign, string strScan) = MakePair(0.997f);
        CompareRunResult oResult = OExecute(strDesign, strScan);

        Assert.NotEqual(oResult.DesignSha256, oResult.ScanSha256);

        string strRecord = File.ReadAllText(OpenDesignCore.Provenance.ArtifactStore.StrPathFor(
            Path.Combine(_strTempDir, "artifacts"), oResult.ReportSha256, ".comparison.json"));
        Assert.Contains($"\"design_sha256\":\"{oResult.DesignSha256}\"", strRecord);
        Assert.Contains($"\"scan_sha256\":\"{oResult.ScanSha256}\"", strRecord);
        // The caveats travel with the numbers, not in a README nobody opens.
        Assert.Contains("scanner's calibration", strRecord);

        using OpenDesignCore.Provenance.Ledger oLedger =
            new(Path.Combine(_strTempDir, "ledger.db"));
        Assert.Equal("dimensional-compare/0.1", Assert.Single(oLedger.ARuns()).Model);
    }
}
