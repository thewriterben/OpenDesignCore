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
        Assert.NotEqual(o.YMm, o.ZMm);
        Assert.NotEqual(o.XMm, o.ZMm);
    }

    [Fact]
    public void ACubeIsRefusedByName()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(new CalibrationBlockParams { XMm = 20, YMm = 20, ZMm = 20 }));
        Assert.Contains("indistinguishable from a correct one", oEx.Message);
    }

    [Fact]
    public void TwoEqualAxesAreEnoughToRefuse()
    {
        Assert.Throws<ArgumentException>(
            () => ORun(new CalibrationBlockParams { XMm = 20, YMm = 20, ZMm = 15 }));
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

        Assert.Equal(0.3f, fFloor, precision: 4);
        Assert.True(fFloor > 0.02f / 10f, "the floor must not track instrument accuracy");
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

        Assert.Contains("\"nominal_x_mm\":\"20.000\"", strSidecar);
        Assert.Contains("\"instrument_accuracy_mm\":\"0.0200\"", strSidecar);
        // The measuring advice travels with the part, not in a README.
        Assert.Contains("transposed measurement is visible", strSidecar);
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
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, 0.3f,
            fMeasuredXMm: 19.93f, fMeasuredYMm: 29.89f, fMeasuredZMm: 15.02f,
            fInstrumentAccuracyMm: 0.02f,
            StrArtifacts, StrLedger, "test-commit");

        Assert.Equal(3, oCmp.Report.Axes.Count);
        Assert.False(oCmp.Report.WithinScanAccuracy, "70 microns is well above a 0.02 mm caliper");

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
        CompareRunResult oCmp = CompareRun.ExecuteMeasured(
            oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, 0.3f,
            19.93f, 29.89f, 15.02f, 0.02f,
            StrArtifacts, StrLedger, "test-commit");

        CompensationRunResult oComp = CompensationRun.Execute(
            StrArtifacts, StrLedger, oCmp.ReportSha256, 0.15, "test-commit");

        Assert.Equal(ECompensationVerdict.Proposed, oComp.Proposal.Verdict);

        // The Z rule, demonstrated rather than asserted in the abstract: Z
        // moved the OTHER WAY (+0.13%) while X and Y shrank ~0.36%. Folding
        // all three into one mean would have proposed roughly half the right
        // compensation.
        Assert.True(oComp.Proposal.ZDeviationPct > 0, "z grew while x and y shrank");
        Assert.Equal(25.0, oComp.Proposal.NominalXyMm, tolerance: 0.01);
        Assert.True(oComp.Proposal.MeasuredXyMm < 24.95);
    }

    [Fact]
    public void AnUndeclaredAccuracyOnAHandMeasurementIsRefused()
    {
        CalibrationBlockRunResult oBlock = ORun();
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => CompareRun.ExecuteMeasured(
                oBlock.ArtifactPath, PicoGK.Mesh.EStlUnit.MM, 0.3f,
                19.93f, 29.89f, 15.02f, 0f,
                StrArtifacts, StrLedger, "test-commit"));
        Assert.Contains("takes no default", oEx.Message);
    }

    [Theory]
    [InlineData("20x30")]
    [InlineData("20x30x15x9")]
    [InlineData("20xbigx15")]
    public void AMalformedMeasurementIsRefused(string strMeasured)
    {
        Assert.Throws<ArgumentException>(() => CompareRun.OParseMeasured(strMeasured));
    }

    [Fact]
    public void AWellFormedMeasurementParses()
    {
        (float fX, float fY, float fZ) = CompareRun.OParseMeasured("19.93x29.89x15.02");
        Assert.Equal(19.93f, fX, precision: 3);
        Assert.Equal(29.89f, fY, precision: 3);
        Assert.Equal(15.02f, fZ, precision: 3);
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
