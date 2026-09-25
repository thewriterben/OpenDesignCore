using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The mount holds a board by its lens barrel, so the tests are about the ways
/// that can fail while still exporting cleanly — and about the failure that
/// produced v0.2 in the first place.
///
/// <para>
/// v0.1 gripped the board's long edges and did not fit: the bottom corners
/// carry a micro-USB and two JST connectors exactly where the slots bit. Every
/// geometric check it had passed, because all of them compared the fixture to
/// its own specification and none could ask whether the specification matched
/// the board. These tests cannot close that gap either — nothing in this repo
/// knows what a board is — so several of them instead pin the *measurements*,
/// which is the part that can be checked.
/// </para>
/// </summary>
public sealed class CameraMountTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-cammount-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>Coarse enough to keep tests quick, fine enough to pass the floor.</summary>
    private static float FVoxel() =>
        CameraMountModel.FResolutionFloorMm(CameraMountParams.ODefault());

    private CameraMountRunResult ORun(CameraMountParams? oParams = null, float? fVoxelMm = null)
    {
        CameraMountParams oUse = oParams ?? CameraMountParams.ODefault();
        return CameraMountRun.Execute(
            oUse,
            fVoxelMm ?? CameraMountModel.FResolutionFloorMm(oUse),
            StrArtifacts, StrLedger, "test-commit");
    }

    // ── the measured board ───────────────────────────────────────────────────

    [Fact]
    public void TheDefaultsAreTheMeasuredBoardAndNotTheDatasheet()
    {
        // LILYGO publish 60 x 32 x 12 mm with four 2 mm holes. None of it
        // describes this board. If someone "corrects" these toward the
        // published figure the fixture stops fitting, and the symptom would be
        // a dead part rather than a red test.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(14.95f, o.BarrelDiameterMm);
        Assert.Equal(5.10f, o.BarrelFreeLengthMm);
        Assert.Equal(16.65f, o.LipDiameterMm);
        Assert.Equal(35.00f, o.AxisToBoardBottomMm);
    }

    [Fact]
    public void TheBoreIsDerivedFromTheMeasuredPrinterOffset()
    {
        // 15.20 is not a number someone liked. It is 14.95 plus a measured
        // offset, and writing it out as a literal would let the two drift the
        // next time the offset is re-measured on a finer ladder.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            o.BarrelDiameterMm + LensFitGaugeModel.MeasuredBoreUndersizeMm,
            o.BoreDiameterMm,
            4);
    }

    [Fact]
    public void TheBoreIsNotTheBarrelDiameter()
    {
        // The single most likely "tidy-up" someone could make to this model is
        // setting the bore to the measured barrel, because they look like they
        // should match. They must not: a printed hole comes out undersize, and
        // the gap is what lens-fit-gauge/0.1 measures.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.True(
            o.BoreDiameterMm > o.BarrelDiameterMm,
            "the nominal bore must exceed the barrel, or the printed hole will not accept it");
        Assert.True(
            o.BoreDiameterMm < o.LipDiameterMm,
            "the bore must be smaller than the lip, or there is no datum at all");
    }

    // ── the ways it could fail to hold the board ─────────────────────────────

    [Fact]
    public void ALandThickerThanTheBarrelIsFreeIsRefused()
    {
        // The lip would hit the plate before the barrel passed through, holding
        // the board off its own datum — and it would export perfectly.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { BoreLandMm = 6.0f }));
        Assert.Contains("free length", oEx.Message);
    }

    [Fact]
    public void ABoreThatDoesNotClearTheBarrelIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { BoreDiameterMm = 14.50f }));
        Assert.Contains("does not clear", oEx.Message);
    }

    [Fact]
    public void ABoreTheLipWouldFallThroughIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { BoreDiameterMm = 17.0f }));
        Assert.Contains("no datum at all", oEx.Message);
    }

    [Fact]
    public void APocketThatDoesNotClearTheLipIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { LipPocketMm = 16.0f }));
        Assert.Contains("does not clear", oEx.Message);
    }

    // ── the resolution floor ─────────────────────────────────────────────────

    [Fact]
    public void TheFloorIsSetByTheBoreBecauseTheBoreIsTheDatum()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            Math.Min(o.BoreDiameterMm / 100f, o.BoreLandMm / 10f),
            CameraMountModel.FResolutionFloorMm(o),
            5);
    }

    [Fact]
    public void AVoxelThatWouldRoundTheBoreIsRefused()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        ResolutionFloorException oEx = Assert.Throws<ResolutionFloorException>(
            () => ORun(o, CameraMountModel.FResolutionFloorMm(o) * 2f));
        Assert.Contains("rattles in or will not enter", oEx.Message);
    }

    // ── what it actually produced ────────────────────────────────────────────

    [Fact]
    public void TheAxisSitsWhereTheBoardsGeometryPutsIt()
    {
        // Base, plus the deliberate gap under the board's bottom edge, plus the
        // measured 35 mm from that edge to the lens centre.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(
            o.BaseThicknessMm + o.FloorDropMm + o.AxisToBoardBottomMm,
            CameraMountModel.FAxisHeightMm(o),
            4);
    }

    [Fact]
    public void TheExportedFixtureIsTheSizeItWasAskedFor()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        CameraMountRunResult oResult = ORun(o);
        float fTol = 2 * FVoxel();

        Assert.Equal(CameraMountModel.FOuterXMm(o), oResult.Geometry.BBoxXMm, fTol);
        Assert.Equal(CameraMountModel.FOuterYMm(o), oResult.Geometry.BBoxYMm, fTol);
        Assert.Equal(CameraMountModel.FOuterZMm(o), oResult.Geometry.BBoxZMm, fTol);
    }

    [Fact]
    public void TheBoreAndPocketWereActuallyCut()
    {
        // A bounding box is identical whether or not the hole exists. The bound
        // is half the bore's own volume, derived from the failure being guarded
        // against rather than chosen — the same reasoning v0.1 used for its
        // slots, which is the one part of v0.1 worth keeping.
        CameraMountParams o = CameraMountParams.ODefault();
        CameraMountRunResult oResult = ORun(o);

        double fAnalytic = CameraMountModel.FAnalyticVolumeCubicMm(o);
        double fMeasured = oResult.Geometry.VolumeCubicMm;
        double fError = Math.Abs(fMeasured - fAnalytic);
        double fBore = Math.PI * Math.Pow(o.BoreDiameterMm / 2.0, 2) * o.BoreLandMm;

        Assert.True(
            fError < fBore / 2.0,
            $"analytic {fAnalytic:F1} mm³, voxel {fMeasured:F1} mm³, deviation {fError:F1} mm³ "
            + $"({fError / fAnalytic * 100:F2} %), bound {fBore / 2.0:F1} mm³ (half the "
            + $"{fBore:F1} mm³ bore). A deviation this large is not discretization — the bore "
            + "or the lip pocket is missing, misplaced, or the wrong size.");
    }

    [Fact]
    public void TwoRunsOfTheSameInputsAreByteIdentical()
    {
        CameraMountRunResult oA = ORun();
        CameraMountRunResult oB = ORun();
        Assert.Equal(oA.ArtifactSha256, oB.ArtifactSha256);
        Assert.Equal(oA.ProvenanceSha256, oB.ProvenanceSha256);
    }

    [Fact]
    public void TheSidecarSaysWhereTheBoreCameFromAndCarriesNoTimestamp()
    {
        // The bore IS measured now, but only to half the gauge's ladder step.
        // An artifact that stated the figure without its provenance would read
        // as though it were exact.
        CameraMountRunResult oResult = ORun();
        string strSidecar = File.ReadAllText(
            Path.Combine(
                StrArtifacts,
                oResult.ProvenanceSha256[..2],
                oResult.ProvenanceSha256 + ".provenance.json"));

        Assert.DoesNotContain("created", strSidecar);
        Assert.Contains("measured 2026-09-25 from a printed lens-fit-gauge", strSidecar);
        Assert.Contains("0.25 +/- 0.13 mm undersize", strSidecar);
        Assert.Contains("measured 2026-09-25 with calipers", strSidecar);
        // And the reason v0.1 failed, so the next fixture does not repeat it.
        Assert.Contains("JST connectors", strSidecar);
    }
}
