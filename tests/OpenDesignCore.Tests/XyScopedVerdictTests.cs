using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The XY verdict must be a judgement about X and Y.
///
/// Both of these came off one real print: X 40.000 against 40.000, Y 60.000
/// against 60.000, z-span 20.900 against 21.000, caliper 0.02 mm. The right
/// answer is "nothing to compensate in XY, Z reported separately". What came
/// back was AxisNotSignificant, carrying the sentence "The other axis moved
/// 0.000 mm and is real".
/// </summary>
public sealed class XyScopedVerdictTests
{
    private static DimensionalReport OReport(
        double fXScan, double fYScan, double fZSpanScan, double fAccuracyMm)
        => new()
        {
            Axes =
            [
                new() { Axis = "x", DesignMm = 40.0, ScanMm = fXScan },
                new() { Axis = "y", DesignMm = 60.0, ScanMm = fYScan },
                new() { Axis = "z-span", DesignMm = 21.0, ScanMm = fZSpanScan },
            ],
            DesignVolumeCubicMm = 30000,
            ScanVolumeCubicMm = 0,
            VoxelSizeMm = 0.2,
            ScanAccuracyMm = fAccuracyMm,
        };

    [Fact]
    public void ZDeviationDoesNotDisqualifyAnXyVerdict()
    {
        // The bug. Z is 0.100 mm out, five times the instrument accuracy, so
        // the report's overall WithinScanAccuracy is false. That said nothing
        // about whether an XY shrinkage factor is warranted, and it was being
        // read as though it did.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.0, 60.0, 20.9, 0.02), fMaxAxisSpreadPct: 0.15);

        Assert.Equal(ECompensationVerdict.WithinScannerNoise, oProp.Verdict);
        Assert.False(oProp.Actionable);
    }

    [Fact]
    public void TheRefusalStillReportsZSeparately()
    {
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.0, 60.0, 20.9, 0.02), fMaxAxisSpreadPct: 0.15);

        Assert.NotNull(oProp.ZDeviationPct);
        Assert.Contains("reported separately", oProp.Reason);
        // The XY figure must not have absorbed it.
        Assert.Contains("nothing here to compensate", oProp.Reason);
    }

    [Fact]
    public void BothAxesQuietNeverClaimsOneOfThemIsReal()
    {
        // The self-refuting sentence, pinned. AxisNotSignificant asserts the
        // other axis "is real"; under an OR that fired with both axes silent.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.0, 60.0, 20.9, 0.02), fMaxAxisSpreadPct: 0.15);

        Assert.DoesNotContain("is real", oProp.Reason);
    }

    [Fact]
    public void ExactlyOneQuietAxisStillGivesAxisNotSignificant()
    {
        // The case that verdict exists for, and it must survive the fix: X
        // silent at 0.01 mm against a 0.02 mm caliper, Y genuinely moved.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(40.01, 59.70, 20.9, 0.02), fMaxAxisSpreadPct: 0.15);

        Assert.Equal(ECompensationVerdict.AxisNotSignificant, oProp.Verdict);
        Assert.Contains("is real", oProp.Reason);
    }

    [Fact]
    public void TwoRealAxesAreStillJudgedOnTheirSpread()
    {
        // Both axes outside noise and moving together: a genuine candidate.
        CompensationProposal oProp = CompensationProposal.OJudge(
            OReport(39.80, 59.70, 20.9, 0.02), fMaxAxisSpreadPct: 0.15);

        Assert.Equal(ECompensationVerdict.Proposed, oProp.Verdict);
        Assert.True(oProp.Actionable);
    }

    [Fact]
    public void AQuietXyWithAQuietZIsStillJustQuiet()
    {
        // No regression for the case the old code did handle.
        Assert.Equal(ECompensationVerdict.WithinScannerNoise,
            CompensationProposal.OJudge(
                OReport(40.0, 60.0, 21.0, 0.02), fMaxAxisSpreadPct: 0.15).Verdict);
    }
}
