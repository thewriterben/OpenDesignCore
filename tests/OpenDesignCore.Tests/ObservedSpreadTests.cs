using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Measurement uncertainty is max(instrument, surface), and the surface
/// usually wins (ADR-0015).
///
/// The declared instrument accuracy is only the uncertainty when the surface
/// is flatter than the instrument. On a real print the final-layer top face
/// spread 0.08 mm under a 0.02 mm caliper, the available Z answers ran from
/// −0.43 % to exactly zero depending on where the jaw landed, and the tool
/// reported an unresolvable deviation as a real finding. These tests pin the
/// fix: the per-axis reading spread widens that axis's uncertainty, travels
/// into the comparison record, and comes back out of it into the verdict.
/// </summary>
public sealed class ObservedSpreadTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-spread-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    private static DimensionalReport OReport(
        double fXScan, double fYScan, double fZSpanScan, double fAccuracyMm,
        double fXSpread = 0, double fYSpread = 0, double fZSpread = 0)
        => new()
        {
            Axes =
            [
                new() { Axis = "x", DesignMm = 40.0, ScanMm = fXScan, ObservedSpreadMm = fXSpread },
                new() { Axis = "y", DesignMm = 60.0, ScanMm = fYScan, ObservedSpreadMm = fYSpread },
                new() { Axis = "z-span", DesignMm = 21.0, ScanMm = fZSpanScan, ObservedSpreadMm = fZSpread },
            ],
            DesignVolumeCubicMm = 30000,
            ScanVolumeCubicMm = 0,
            VoxelSizeMm = 0.2,
            ScanAccuracyMm = fAccuracyMm,
        };

    /// <summary>
    /// The measurement that motivated all of this, as a known answer. X and Y
    /// dead on nominal off vertical walls; Z readings off the final layer
    /// spread 0.09 mm against a 0.02 mm caliper, with the mean 0.045 mm short.
    /// Under declared-accuracy-only, that Z read as a real −0.2 % finding.
    /// With the spread as the uncertainty, the report says what was true: no
    /// axis's deviation is resolvable on this print.
    /// </summary>
    [Fact]
    public void TheUnmeasurableZCaseIsRefusedByTheToolAlone()
    {
        DimensionalReport oReport = OReport(
            40.0, 60.0, 20.955, fAccuracyMm: 0.02, fZSpread: 0.09);

        Assert.True(oReport.WithinScanAccuracy,
            "a 0.045 mm deviation under a 0.09 mm reading spread is not a finding");

        // And the same report under the old assumption — a single reading, so
        // spread zero — was the wrong answer this replaces.
        DimensionalReport oSingleReading = OReport(40.0, 60.0, 20.955, 0.02);
        Assert.False(oSingleReading.WithinScanAccuracy,
            "with one reading the spread is invisible and 0.045 mm looks real");
    }

    [Fact]
    public void TheSurfaceWidensAnAxisUncertaintyInTheVerdict()
    {
        // X off by 0.050 mm — 2.5× the caliper's accuracy, a real finding on a
        // flat face. But the readings spread 0.200 mm: the jaw was landing on
        // different surface, and 0.050 is inside that. Y likewise.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.05, 60.06, 21.0, 0.02, fXSpread: 0.20, fYSpread: 0.20),
            fMaxAxisSpreadPct: 0.15);

        Assert.Equal(ECompensationVerdict.WithinScannerNoise, oProp.Verdict);
        Assert.Contains("the surface", oProp.Reason);
    }

    [Fact]
    public void TheSpreadNeverNarrowsTheDeclaredAccuracy()
    {
        // Readings tighter than the instrument prove repeatability, not
        // accuracy — the uncertainty floor stays at the declared figure.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.015, 60.0, 21.0, 0.02, fXSpread: 0.005),
            fMaxAxisSpreadPct: 0.15);

        // 0.015 mm is inside the caliper's 0.02 even though it is three times
        // the observed spread.
        Assert.Equal(ECompensationVerdict.WithinScannerNoise, oProp.Verdict);
    }

    [Fact]
    public void ARoughFaceOnOneAxisDoesNotInfectAnother()
    {
        // Z's readings spread wide; X and Y came off vertical walls and are
        // clean, real, and in agreement. The Z surface must not veto the XY
        // verdict — the same scoping rule the XY fix established, now for
        // uncertainty.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(39.86, 59.79, 21.0, 0.02, fZSpread: 0.30),
            fMaxAxisSpreadPct: 0.15);

        Assert.Equal(ECompensationVerdict.Proposed, oProp.Verdict);
    }

    [Fact]
    public void CommaSeparatedReadingsParsePerDimension()
    {
        float[][] aM = CompareRun.AParseMeasured("40.00,40.01,40.02x60.00x4.00x25.00,24.98");
        Assert.Equal(4, aM.Length);
        Assert.Equal(3, aM[0].Length);
        Assert.Single(aM[1]);
        Assert.Single(aM[2]);
        Assert.Equal(2, aM[3].Length);
    }

    [Theory]
    [InlineData("40.00,abcx60x25")]
    [InlineData(",x60x25")]
    public void MalformedReadingsAreRefused(string strMeasured)
    {
        Assert.Throws<ArgumentException>(() => CompareRun.AParseMeasured(strMeasured));
    }

    [Fact]
    public void ASpanSpreadIsTheSumOfBothFaces()
    {
        // The span pairs one face's reading against the other's, so the
        // extreme answers pair opposite extremes: spread_high + spread_low.
        MeasuredReadings oHigh = new() { Values = [25.00f, 25.08f] }; // 0.08
        MeasuredReadings oLow = new() { Values = [4.00f, 4.03f] };    // 0.03
        Assert.Equal(0.08, oHigh.SpreadMm, tolerance: 1e-4);
        Assert.Equal(0.03, oLow.SpreadMm, tolerance: 1e-4);
        // The composition itself is pinned end to end in
        // TheSpreadTravelsIntoTheRecordAndBackIntoTheVerdict.
    }

    /// <summary>
    /// The property that matters most: the spread is not a command-line
    /// courtesy. It is recorded in the comparison, and a later `compensate`
    /// run over the stored record still sees it. Dropping it at either hop
    /// would launder an unresolvable deviation back into a finding.
    /// </summary>
    [Fact]
    public void TheSpreadTravelsIntoTheRecordAndBackIntoTheVerdict()
    {
        CalibrationBlockParams oParams = CalibrationBlockModel.ODefault();
        CalibrationBlockRunResult oBlock = CalibrationBlockRun.Execute(
            oParams, CalibrationBlockModel.FResolutionFloorMm(oParams), 0.02f,
            StrArtifacts, StrLedger, "test-commit");

        // Deviations of 0.05–0.06 mm — well above the 0.02 mm caliper, and
        // inside the 0.20 mm the readings spread. Without the spread this is
        // a Proposed compensation; with it there is nothing to compensate.
        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM,
            CalibrationBlockModel.FResolutionFloorMm(oParams),
            new MeasuredReadings { Values = [40.05f, 40.15f, 39.95f] },
            new MeasuredReadings { Values = [60.06f, 60.16f, 59.96f] },
            new MeasuredReadings { Values = [25.02f] },
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: "pla");

        string strRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oCmp.ReportSha256, ".comparison.json"));
        Assert.Contains("\"observed_spread_mm\":\"0.200\"", strRecord);
        Assert.Contains("\"uncertainty_mm\":\"0.200\"", strRecord);
        Assert.Contains("\"raw_readings_mm\"", strRecord);
        Assert.Contains("odc/comparison/0.3", strRecord);

        CompensationRunResult oComp = CompensationRun.Execute(
            StrArtifacts, StrLedger, oCmp.ReportSha256, 0.15, "test-commit");

        Assert.Equal(ECompensationVerdict.WithinScannerNoise, oComp.Proposal.Verdict);
        Assert.Contains("the surface", oComp.Proposal.Reason);
    }
}
