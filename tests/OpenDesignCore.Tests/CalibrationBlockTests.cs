using System.Text.Json;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;
using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The block exists to be measured, so the tests are about measurability:
/// that its axes cannot be confused, that what it exports is what it claims,
/// and that a hand measurement travels the same road as a scan.
/// </summary>
public sealed class CalibrationBlockTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-calblock-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    private static float FVoxel() =>
        CalibrationBlockModel.FResolutionFloorMm(CalibrationBlockModel.ODefault());

    private CalibrationBlockRunResult ORun(CalibrationBlockParams? oParams = null)
    {
        CalibrationBlockParams oUse = oParams ?? CalibrationBlockModel.ODefault();
        return CalibrationBlockRun.Execute(
            oUse, CalibrationBlockModel.FResolutionFloorMm(oUse), 0.02f,
            StrArtifacts, StrLedger, "test-commit");
    }

    [Fact]
    public void TheDefaultBlockHasNoTwoAxesAlike()
    {
        // The entire reason this model is not a cube: with equal dimensions a
        // measurement across the wrong faces is indistinguishable from a
        // correct one, and the compensation lands on an axis nobody measured.
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();
        Assert.NotEqual(o.XMm, o.YMm);
        // And the shelf must sit below the tall face, or there is no Z span.
        Assert.True(o.StepZMm < o.ZMm);
        Assert.True(o.ZSpanMm > 15f, "the Z baseline must be long enough to resolve a fraction of a percent");
    }

    private static CalibrationBlockParams OWith(float fX, float fY) =>
        CalibrationBlockModel.ODefault() with { XMm = fX, YMm = fY };

    [Fact]
    public void EqualXAndYAreRefusedByName()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(() => ORun(OWith(40, 40)));
        Assert.Contains("indistinguishable from a correct one", oEx.Message);
    }

    [Fact]
    public void AShelfLevelWithTheTopIsRefused()
    {
        // No span, no way to separate the first-layer constant from shrinkage.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CalibrationBlockModel.ODefault() with { StepZMm = 25f }));
        Assert.Contains("no Z span to measure", oEx.Message);
    }

    [Fact]
    public void ATallRegionFillingTheWholeDepthIsRefused()
    {
        // Then there is no shelf to measure from at all.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CalibrationBlockModel.ODefault() with { TallDepthYMm = 60f }));
        Assert.Contains("leaving a shelf to measure from", oEx.Message);
    }

    [Fact]
    public void TheResolutionFloorDoesNotDependOnTheInstrument()
    {
        // The bug this replaced: the floor was instrument_accuracy / 10, so a
        // 0.02 mm caliper demanded 0.002 mm voxels — over 10^11 voxels for
        // this block, and 21 GB of RAM before it was killed. The comparison
        // uses the exported bounding box, so grid quantisation is measured
        // out rather than assumed away, and the instrument is irrelevant here.
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();
        float fFloor = CalibrationBlockModel.FResolutionFloorMm(o);

        Assert.Equal(o.StepZMm / 20f, fFloor, precision: 4);
        Assert.True(fFloor > 0.02f / 10f, "the floor must not track instrument accuracy");

        // And it must scale with the geometry, not sit at a constant.
        CalibrationBlockParams oThinner = o with { StepZMm = o.StepZMm / 2f };
        Assert.True(CalibrationBlockModel.FResolutionFloorMm(oThinner) < fFloor);
    }

    [Fact]
    public void WhatItExportsIsWhatItClaims()
    {
        CalibrationBlockRunResult oResult = ORun();
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();

        // Tight, because an error here does not stay here: it becomes a
        // compensation applied to every later print in that material.
        Assert.Equal(o.XMm, oResult.Geometry.BBoxXMm, tolerance: 0.01);
        Assert.Equal(o.YMm, oResult.Geometry.BBoxYMm, tolerance: 0.01);
        Assert.Equal(o.ZMm, oResult.Geometry.BBoxZMm, tolerance: 0.01);
    }

    [Fact]
    public void TheSidecarCarriesTheNominalsAndTheInstrument()
    {
        CalibrationBlockRunResult oResult = ORun();
        string strSidecar = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oResult.ProvenanceSha256, ".provenance.json"));

        Assert.Contains("\"nominal_x_mm\":\"40.000\"", strSidecar);
        Assert.Contains("\"nominal_step_z_mm\":\"4.000\"", strSidecar);
        Assert.Contains("\"nominal_z_span_mm\":\"21.000\"", strSidecar);
        Assert.Contains("\"instrument_accuracy_mm\":\"0.0200\"", strSidecar);
        // The measuring advice travels with the part, not in a README — and
        // it says WHY two Z readings are needed rather than just asking.
        Assert.Contains("transposed measurement is visible", strSidecar);
        Assert.Contains("the height begins there", strSidecar);
    }

    [Fact]
    public void AnUndeclaredInstrumentAccuracyIsRefused()
    {
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => CalibrationBlockRun.Execute(
                o, CalibrationBlockModel.FResolutionFloorMm(o), 0f,
                StrArtifacts, StrLedger, "test-commit"));
        Assert.Contains("takes no default", oEx.Message);
    }

    [Fact]
    public void TooCoarseIsRefused()
    {
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();
        Assert.Throws<ResolutionFloorException>(
            () => CalibrationBlockRun.Execute(
                o, 1.0f, 0.02f, StrArtifacts, StrLedger, "test-commit"));
    }

    /// <summary>
    /// A caliper is an instrument with a stated accuracy, exactly like a
    /// scanner. Everything downstream must be unchanged.
    /// </summary>
    [Fact]
    public void AHandMeasurementProducesAComparableRecord()
    {
        CalibrationBlockRunResult oBlock = ORun();

        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, FVoxel(),
            fMeasuredXMm: 39.85f, fMeasuredYMm: 59.78f, fMeasuredZMm: 24.85f,
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: "pla");

        Assert.Equal(3, oCmp.Report.Axes.Count);
        Assert.False(oCmp.Report.WithinScanAccuracy, "150 microns is well above a 0.02 mm caliper");

        string strRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oCmp.ReportSha256, ".comparison.json"));
        Assert.Contains("\"measured_by\":\"manual\"", strRecord);
        // No scan bytes exist to hash. Recorded as absent, never as empty.
        Assert.Contains("none-measured-by-hand", strRecord);
        // A caliper cannot give a volume, and one is not invented from extents.
        Assert.Contains("not-measurable-by-hand", strRecord);
    }

    [Fact]
    public void AHandMeasurementFlowsIntoACompensation()
    {
        CalibrationBlockRunResult oBlock = ORun();
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();

        // A part that shrank ~0.35% in X and Y and grew in Z, measured on the
        // 40 x 60 block where 0.35% is well clear of a 0.02 mm caliper.
        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, FVoxel(),
            fMeasuredXMm: 39.86f, fMeasuredYMm: 59.79f, fMeasuredZMm: 25.10f,
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: "pla",
            fMeasuredZLowMm: 4.05f, fNominalZLowMm: o.StepZMm);

        CompensationRunResult oComp = CompensationRun.Execute(
            StrArtifacts, StrLedger, oCmp.ReportSha256, 0.15, "test-commit");

        Assert.Equal(ECompensationVerdict.Proposed, oComp.Proposal.Verdict);

        // The Z rule, demonstrated rather than asserted in the abstract: the
        // Z span grew while X and Y shrank. Folding it into the XY mean would
        // drag the compensation toward zero.
        Assert.True(oComp.Proposal.ZDeviationPct > 0, "the z span grew while x and y shrank");
        Assert.Equal(50.0, oComp.Proposal.NominalXyMm, tolerance: 0.05);
        Assert.True(oComp.Proposal.MeasuredXyMm < oComp.Proposal.NominalXyMm);
    }

    [Fact]
    public void AnUndeclaredAccuracyOnAHandMeasurementIsRefused()
    {
        CalibrationBlockRunResult oBlock = ORun();
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => CompareRun.ExecuteMeasured(
                oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, 0.3f,
                19.93f, 29.89f, 15.02f, 0f,
                StrArtifacts, StrLedger, "test-commit", strMaterial: "pla"));
        Assert.Contains("takes no default", oEx.Message);
    }

    [Theory]
    [InlineData("20x30")]
    [InlineData("20x30x15x9x4")]
    [InlineData("20xbigx15")]
    public void AMalformedMeasurementIsRefused(string strMeasured)
    {
        Assert.Throws<ArgumentException>(() => CompareRun.AParseMeasured(strMeasured));
    }

    [Fact]
    public void ThreeOrFourReadingsBothParse()
    {
        Assert.Equal(3, CompareRun.AParseMeasured("19.93x29.89x15.02").Length);
        Assert.Equal(4, CompareRun.AParseMeasured("39.9x59.85x3.98x24.9").Length);
    }

    /// <summary>
    /// The point of the step, in arithmetic.
    ///
    /// A part shrunk 0.5% in Z and squished 0.08 mm into the bed. Both height
    /// readings carry that same 0.08 mm; their difference carries none, so the
    /// scale comes back clean and the squish comes back separately.
    /// </summary>
    [Fact]
    public void TheFirstLayerOffsetCancelsOutOfTheZSpan()
    {
        const double fScaleTruth = 0.995;
        const double fOffsetTruth = -0.08;
        double fMeasuredLow = 4.0 * fScaleTruth + fOffsetTruth;
        double fMeasuredHigh = 25.0 * fScaleTruth + fOffsetTruth;

        (double fScale, double fOffset) =
            CompareRun.OSolveZ(4.0, 25.0, fMeasuredLow, fMeasuredHigh);

        Assert.Equal(fScaleTruth, fScale, precision: 6);
        Assert.Equal(fOffsetTruth, fOffset, precision: 6);
    }

    [Fact]
    public void ASingleHeightCannotSeparateThem()
    {
        // Two unknowns, one equation. The refusal is in the geometry, not the
        // arithmetic: without a second face there is nothing to subtract.
        Assert.Throws<ArgumentException>(
            () => CompareRun.OSolveZ(fNominalLowMm: 25.0, fNominalHighMm: 25.0,
                                     fMeasuredLowMm: 24.9, fMeasuredHighMm: 24.9));
    }

    [Fact]
    public void AZSpanIsReportedInsteadOfABedReferencedHeight()
    {
        CalibrationBlockRunResult oBlock = ORun();
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();

        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM,
            CalibrationBlockModel.FResolutionFloorMm(o),
            fMeasuredXMm: 39.90f, fMeasuredYMm: 59.85f, fMeasuredZMm: 24.79f,
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: "pla",
            fMeasuredZLowMm: 3.90f, fNominalZLowMm: o.StepZMm);

        AxisDeviation oZ = oCmp.Report.Axes.Single(a => a.Axis == "z-span");
        Assert.Equal(o.ZSpanMm, oZ.DesignMm, tolerance: 0.05);
        Assert.Equal(24.79 - 3.90, oZ.ScanMm, precision: 3);

        string strRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oCmp.ReportSha256, ".comparison.json"));
        Assert.Contains("\"first_layer_offset_mm\"", strRecord);
        Assert.DoesNotContain("not-separable-from-a-single-height", strRecord);
    }

    [Fact]
    public void ASingleHeightRecordsTheOffsetAsInseparable()
    {
        CalibrationBlockRunResult oBlock = ORun();
        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM,
            CalibrationBlockModel.FResolutionFloorMm(CalibrationBlockModel.ODefault()),
            39.90f, 59.85f, 24.79f, 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: "pla");

        Assert.Contains(oCmp.Report.Axes, a => a.Axis == "z");
        string strRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oCmp.ReportSha256, ".comparison.json"));
        Assert.Contains("not-separable-from-a-single-height", strRecord);
    }

    /// <summary>
    /// The mistake this prevents actually happened.
    ///
    /// The walkthrough's step 6 said `--propose-to-profile petg` while never
    /// saying what to print. Benji printed PLA. Nothing in the pipeline
    /// recorded the material, so a PLA measurement would have landed in the
    /// PETG profile carrying a provenance hash that made it look sourced —
    /// worse than an unsourced guess, because it would have looked rigorous.
    /// </summary>
    [Fact]
    public void AMaterialMismatchIsRefusedBeforeItReachesAProfile()
    {
        CompensationRunResult oComp = OMeasureAndJudge("pla");
        Assert.Equal(ECompensationVerdict.Proposed, oComp.Proposal.Verdict);

        CompensationException oEx = Assert.Throws<CompensationException>(
            () => CompensationRun.StrPropose(
                oComp, "http://127.0.0.1:1", "petg", TestMachines.OCalibrated(StrArtifacts)));
        Assert.Contains("measured in 'pla'", oEx.Message);
        Assert.Contains("look sourced", oEx.Message);
    }

    [Fact]
    public void AMatchingProfileGetsPastTheMaterialGate()
    {
        CompensationRunResult oComp = OMeasureAndJudge("pla");
        // Reaches the studio call and fails there, which is how we know the
        // material gate let it through rather than the reverse.
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => CompensationRun.StrPropose(
                oComp, "http://127.0.0.1:1", "pla", TestMachines.OCalibrated(StrArtifacts)));
        Assert.Contains("unreachable", oEx.Message);
    }

    [Fact]
    public void AMoreSpecificProfileKeyIsAccepted()
    {
        // "petg" measured against a "petg-cf" or "esun-petg" profile is a
        // reasonable thing to do; refusing it would be pedantry rather than
        // safety. The gate exists for pla-vs-petg, not for naming schemes.
        CompensationRunResult oComp = OMeasureAndJudge("petg");
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => CompensationRun.StrPropose(
                oComp, "http://127.0.0.1:1", "esun-petg", TestMachines.OCalibrated(StrArtifacts)));
        Assert.Contains("unreachable", oEx.Message);
    }

    [Fact]
    public void AnUndeclaredMaterialIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => CompareRun.ExecuteMeasured(
                ORun().ArtifactPath, PicoGK.Mesh.EStlUnit.MM, FVoxel(),
                39.86f, 59.79f, 25.10f, 0.02f,
                StrArtifacts, StrLedger, "test-commit", strMaterial: "  "));
        Assert.Contains("takes no default", oEx.Message);
    }

    [Fact]
    public void TheMaterialIsRecordedInBothRecords()
    {
        CompensationRunResult oComp = OMeasureAndJudge("pla");
        Assert.Equal("pla", oComp.Material);

        string strCompRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oComp.RecordSha256, ".compensation.json"));
        Assert.Contains("\"material\":\"pla\"", strCompRecord);

        string strCmpRecord = File.ReadAllText(ArtifactStore.StrPathFor(
            StrArtifacts, oComp.ComparisonSha256, ".comparison.json"));
        Assert.Contains("\"material\":\"pla\"", strCmpRecord);
    }

    /// <summary>A clean measurement in one material, judged and ready to propose.</summary>
    private CompensationRunResult OMeasureAndJudge(string strMaterial)
    {
        CalibrationBlockRunResult oBlock = ORun();
        CalibrationBlockParams o = CalibrationBlockModel.ODefault();
        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, FVoxel(),
            fMeasuredXMm: 39.86f, fMeasuredYMm: 59.79f, fMeasuredZMm: 25.10f,
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit", strMaterial: strMaterial,
            fMeasuredZLowMm: 4.05f, fNominalZLowMm: o.StepZMm);

        return CompensationRun.Execute(
            StrArtifacts, StrLedger, oCmp.ReportSha256, 0.15, "test-commit");
    }

    [Fact]
    public void TheRunIsLedgeredWithItsModelId()
    {
        CalibrationBlockRunResult oResult = ORun();
        using Ledger oLedger = new(StrLedger);
        RunRecord oRow = Assert.Single(oLedger.ARuns());
        Assert.Equal(CalibrationBlockModel.StrModelId, oRow.Model);
        Assert.Equal(oResult.ArtifactSha256, oRow.ArtifactSha256);
        Assert.True(oRow.Passed);
    }

    [Fact]
    public void TheSidecarIsSchema02SoItCarriesItsOwnDimensions()
    {
        CalibrationBlockRunResult oResult = ORun();
        using JsonDocument oDoc = JsonDocument.Parse(File.ReadAllBytes(
            ArtifactStore.StrPathFor(StrArtifacts, oResult.ProvenanceSha256, ".provenance.json")));

        Assert.Equal("odc/provenance/0.2", oDoc.RootElement.GetProperty("schema").GetString());
        // The reason quantisation never reaches a comparison: the record says
        // what was actually exported, not what was requested.
        Assert.True(oDoc.RootElement.GetProperty("artifact").TryGetProperty("bbox_mm", out _));
    }
}
