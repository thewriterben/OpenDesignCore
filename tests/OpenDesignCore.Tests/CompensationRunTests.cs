using System.Numerics;
using System.Text.Json;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using OpenDesignCore.Verification;
using PicoGK;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The refusals are the feature. A compensation that is emitted whenever a
/// number exists would launder instrument noise and axis disagreement into a
/// slicer setting, where it would look like a measurement.
///
/// Each test drives a real `compare` run first, so the record being read is
/// one the system actually wrote rather than a fixture that could drift.
/// </summary>
public sealed class CompensationRunTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-compensate-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>A comparison of a 20x30x10 box against a scaled copy.</summary>
    private string StrCompare(float fScaleX, float fScaleY, float fScaleZ, float fAccuracyMm)
    {
        string strDesign = Path.Combine(_strTempDir, $"d-{fScaleX}-{fScaleY}-{fScaleZ}.stl");
        string strScan = Path.Combine(_strTempDir, $"s-{fScaleX}-{fScaleY}-{fScaleZ}.stl");
        using (Library oLib = new(0.2f))
        {
            Mesh mshDesign = Utils.mshCreateCube(oLib, new Vector3(20, 30, 10), Vector3.Zero);
            mshDesign.SaveToStlFile(strDesign, Mesh.EStlUnit.MM);
            mshDesign.mshCreateTransformed(Matrix4x4.CreateScale(fScaleX, fScaleY, fScaleZ))
                .SaveToStlFile(strScan, Mesh.EStlUnit.MM);
        }

        return CompareRun.Execute(
            strDesign, strScan, Mesh.EStlUnit.MM, 0.2f,
            StrArtifacts, StrLedger, "test-commit", fAccuracyMm).ReportSha256;
    }

    private CompensationRunResult OCompensate(string strComparison, double fMaxSpreadPct)
        => CompensationRun.Execute(
            StrArtifacts, StrLedger, strComparison, fMaxSpreadPct, "test-commit");

    [Fact]
    public void UniformShrinkageAboveScannerAccuracy_IsProposed()
    {
        // 0.7% on a 30 mm edge is 210 microns — far above a 0.05 mm scanner.
        string strComparison = StrCompare(0.993f, 0.993f, 0.993f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        Assert.Equal(ECompensationVerdict.Proposed, oResult.Proposal.Verdict);
        Assert.True(oResult.Proposal.Actionable);
        // The pair handed on is the design and the scan, not a derived setting:
        // the arithmetic belongs to AdvancedStudio's calculators.
        Assert.True(oResult.Proposal.MeasuredXyMm < oResult.Proposal.NominalXyMm);
    }

    [Fact]
    public void DeviationInsideScannerAccuracy_IsRefusedAsNoise()
    {
        // 0.01% of 30 mm is 3 microns. A scanner good to 0.05 mm cannot see it,
        // so a compensation from it would be compensating for the instrument.
        string strComparison = StrCompare(0.9999f, 0.9999f, 0.9999f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        Assert.Equal(ECompensationVerdict.WithinScannerNoise, oResult.Proposal.Verdict);
        Assert.False(oResult.Proposal.Actionable);
        Assert.Contains("compensating for the", oResult.Proposal.Reason);
    }

    [Fact]
    public void UndeclaredScannerAccuracy_IsRefusedAsUnknown()
    {
        // Unknown, never "small enough to ignore" — the same rule as units.
        string strComparison = StrCompare(0.993f, 0.993f, 0.993f, fAccuracyMm: 0f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        Assert.Equal(ECompensationVerdict.AccuracyUndeclared, oResult.Proposal.Verdict);
        Assert.Contains("--scan-accuracy-mm", oResult.Proposal.Reason);
    }

    [Fact]
    public void AxesThatDisagree_AreRefusedRatherThanAveraged()
    {
        // X shrank 0.2%, Y shrank 1.4%. Their mean, 0.8%, is wrong on both.
        string strComparison = StrCompare(0.998f, 0.986f, 0.998f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        Assert.Equal(ECompensationVerdict.AxesDisagree, oResult.Proposal.Verdict);
        Assert.True(oResult.Proposal.AxisSpreadPct > 1.0);
        Assert.Contains("wrong on both axes", oResult.Proposal.Reason);
    }

    [Fact]
    public void TheSpreadLimitIsTheCallersToDeclare()
    {
        // The same measurement, judged against two different declared limits.
        // A tolerance that decided this itself would be a hidden constant.
        string strComparison = StrCompare(0.998f, 0.986f, 0.998f, fAccuracyMm: 0.05f);

        Assert.Equal(ECompensationVerdict.AxesDisagree,
            OCompensate(strComparison, fMaxSpreadPct: 0.2).Proposal.Verdict);
        Assert.Equal(ECompensationVerdict.Proposed,
            OCompensate(strComparison, fMaxSpreadPct: 5.0).Proposal.Verdict);
    }

    [Fact]
    public void AMissingOrZeroSpreadLimit_IsRefused()
    {
        string strComparison = StrCompare(0.993f, 0.993f, 0.993f, fAccuracyMm: 0.05f);
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => OCompensate(strComparison, fMaxSpreadPct: 0));
        Assert.Contains("no defensible default", oEx.Message);
    }

    [Fact]
    public void ZIsReportedButNeverFoldedIntoTheXyFigure()
    {
        // Z shrank five times as much as X and Y. If Z leaked into the XY
        // figure it would drag the pair; it must not, because Orca's
        // Shrinkage (XY) does not apply to Z at all.
        string strComparison = StrCompare(0.997f, 0.997f, 0.985f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        Assert.Equal(ECompensationVerdict.Proposed, oResult.Proposal.Verdict);
        Assert.NotNull(oResult.Proposal.ZDeviationPct);
        Assert.True(oResult.Proposal.ZDeviationPct < -1.0, "z should show its own large shrink");

        // The XY pair is the mean of x and y only: 20 and 30 nominal -> 25.
        Assert.Equal(25.0, oResult.Proposal.NominalXyMm, tolerance: 0.01);
    }

    [Fact]
    public void TheRecordIsHashChainedToTheComparisonItRead()
    {
        string strComparison = StrCompare(0.993f, 0.993f, 0.993f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        string strRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oResult.RecordSha256, ".compensation.json"));
        Assert.Contains($"\"comparison_sha256\":\"{strComparison}\"", strRecord);
        Assert.Contains("\"max_axis_spread_pct\":\"0.200\"", strRecord);
        // Caveats travel with the numbers, not in documentation nobody opens.
        Assert.Contains("does not compute the slicer setting", strRecord);
    }

    [Fact]
    public void ARefusalIsRecordedAndDoesNotCountAsAPass()
    {
        string strComparison = StrCompare(0.9999f, 0.9999f, 0.9999f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        using Ledger oLedger = new(StrLedger);
        RunRecord oRun = oLedger.ARuns().Single(r => r.Model == CompensationRun.StrModelId);
        Assert.False(oRun.Passed, "no proposal was produced, so nothing passed");
        Assert.Equal(oResult.RecordSha256, oRun.ArtifactSha256);

        using JsonDocument oDoc = JsonDocument.Parse(File.ReadAllBytes(
            ArtifactStore.StrPathFor(StrArtifacts, oResult.RecordSha256, ".compensation.json")));
        Assert.False(oDoc.RootElement.GetProperty("actionable").GetBoolean());
    }

    [Fact]
    public void ARefusalIsNeverProposedToTheStudio()
    {
        // The gate that matters for the seam: a refusal must not become a
        // profile change. A studio that received one would store a
        // compensation this engine had just declined to make.
        string strComparison = StrCompare(0.9999f, 0.9999f, 0.9999f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        CompensationException oEx = Assert.Throws<CompensationException>(
            () => CompensationRun.StrPropose(oResult, "http://127.0.0.1:1", "petg"));
        Assert.Contains("Refusing to propose", oEx.Message);
        Assert.Contains("WithinScannerNoise", oEx.Message);
    }

    [Fact]
    public void AnUnreachableStudioFailsLoudlyAndSaysTheRecordSurvived()
    {
        string strComparison = StrCompare(0.993f, 0.993f, 0.993f, fAccuracyMm: 0.05f);
        CompensationRunResult oResult = OCompensate(strComparison, fMaxSpreadPct: 0.2);

        CompensationException oEx = Assert.Throws<CompensationException>(
            () => CompensationRun.StrPropose(oResult, "http://127.0.0.1:1", "petg"));
        Assert.Contains("unreachable", oEx.Message);
        Assert.Contains("record is written", oEx.Message);
    }

    [Fact]
    public void AnUnknownComparisonHashIsRefusedByName()
    {
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => OCompensate(new string('a', 64), fMaxSpreadPct: 0.2));
        Assert.Contains("Run `compare` first", oEx.Message);
    }

    [Fact]
    public void ARecordThatIsNotAComparisonIsRefused()
    {
        // A provenance sidecar is a valid ODC record and the wrong one.
        byte[] abNotAComparison = System.Text.Encoding.ASCII.GetBytes(
            """{"schema":"odc/provenance/0.2"}""");
        string strHash = ArtifactStore.StrStore(StrArtifacts, abNotAComparison, ".comparison.json");

        CompensationException oEx = Assert.Throws<CompensationException>(
            () => OCompensate(strHash, fMaxSpreadPct: 0.2));
        Assert.Contains("not a comparison record", oEx.Message);
    }
}
