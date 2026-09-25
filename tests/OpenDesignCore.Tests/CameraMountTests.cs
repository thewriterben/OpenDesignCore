using OpenDesignCore.Models;
using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The mount exists to hold a board still, so the tests are about the ways it
/// could fail to do that and still export cleanly: a slot the board will not
/// enter, uprights the lens fouls, and a base that does not reach under the
/// mass hanging off the front.
///
/// The board dimensions are measurements of one physical board and the vendor
/// datasheet disagrees with them substantially, so there is also a test that
/// the defaults have not quietly drifted back toward the datasheet.
/// </summary>
public sealed class CameraMountTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-cammount-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrArtifacts => Path.Combine(_strTempDir, "artifacts");
    private string StrLedger => Path.Combine(_strTempDir, "ledger.db");

    /// <summary>Comfortably inside the floor, and coarse enough to keep tests quick.</summary>
    private static float FVoxel() => 0.4f;

    private CameraMountRunResult ORun(CameraMountParams? oParams = null, float? fVoxelMm = null)
    {
        CameraMountParams oUse = oParams ?? CameraMountParams.ODefault();
        return CameraMountRun.Execute(
            oUse, fVoxelMm ?? FVoxel(), StrArtifacts, StrLedger, "test-commit");
    }

    // ── the measured board ───────────────────────────────────────────────────

    [Fact]
    public void TheDefaultsAreTheMeasuredBoardAndNotTheDatasheet()
    {
        // LILYGO publish 60 x 32 x 12 mm for this product. The board on the
        // bench is none of those. If someone "corrects" these toward the
        // published figure the fixture stops fitting, and the failure would be
        // a part that does not work rather than a test that goes red — so the
        // test goes red instead.
        CameraMountParams o = CameraMountParams.ODefault();
        Assert.Equal(68.25f, o.BoardLengthMm);
        Assert.Equal(27.90f, o.BoardWidthMm);
        Assert.Equal(29.40f, o.TotalDepthMm);
        Assert.NotEqual(60f, o.BoardLengthMm);
        Assert.NotEqual(32f, o.BoardWidthMm);
        Assert.NotEqual(12f, o.TotalDepthMm);
    }

    // ── the ways it could hold the board badly ───────────────────────────────

    [Fact]
    public void AGripThatWouldFoulTheLensIsRefusedByName()
    {
        // The lens passes between the uprights. Grip deep enough and the
        // fixture is one the board cannot be pushed into, which a bounding-box
        // check would never notice.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { GripDepthMm = 7.0f }));
        Assert.Contains("lens barrel", oEx.Message);
    }

    [Fact]
    public void AGripDeeperThanTheBoardIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { GripDepthMm = 20.0f }));
        Assert.Contains("no board between the", oEx.Message);
    }

    [Fact]
    public void ABaseThatStopsShortOfTheLensTipIsRefused()
    {
        // 1.25 mm of plate with 24 mm of lens hanging off it. A base that does
        // not reach the tip is a fixture that falls over, and it exports
        // perfectly.
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { BaseFrontMm = 10.0f }));
        Assert.Contains("falls over", oEx.Message);
    }

    [Fact]
    public void ABaseThatDoesNotReachTheDisplayStackIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { BaseBackMm = 1.0f }));
        Assert.Contains("display stack", oEx.Message);
    }

    [Fact]
    public void AGripTallerThanTheBoardIsRefused()
    {
        ArgumentException oEx = Assert.Throws<ArgumentException>(
            () => ORun(CameraMountParams.ODefault() with { GripHeightMm = 100.0f }));
        Assert.Contains("exceeds the", oEx.Message);
    }

    // ── the resolution floor ─────────────────────────────────────────────────

    [Fact]
    public void TheFloorIsSetByTheSlotWhenTheSlotIsTheNarrowerFeature()
    {
        // Default: 2.40 mm wall vs a 1.65 mm slot. The slot is narrower, so it
        // decides — this is the case the enclosure model does not have, and
        // getting it backwards would allow a voxel size that closes the slot.
        CameraMountParams o = CameraMountParams.ODefault();
        float fSlotMm = o.BoardThicknessMm + 2 * o.SlotClearanceMm;
        Assert.True(fSlotMm < o.WallMm, "precondition: the slot is the narrower feature");
        Assert.Equal(fSlotMm / 2f, CameraMountModel.FResolutionFloorMm(o), 4);
    }

    [Fact]
    public void AVoxelThatWouldCloseTheSlotIsRefused()
    {
        CameraMountParams o = CameraMountParams.ODefault();
        float fTooCoarse = CameraMountModel.FResolutionFloorMm(o) * 1.5f;
        ResolutionFloorException oEx = Assert.Throws<ResolutionFloorException>(
            () => ORun(o, fTooCoarse));
        Assert.Contains("slot the board does not fit", oEx.Message);
    }

    // ── what it actually produced ────────────────────────────────────────────

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
    public void BothSlotsAreActuallyCut()
    {
        // A bounding box is identical whether or not the slots exist, so the
        // check has to be on volume — and it has to be an absolute check, not a
        // nearest-of-two.
        //
        // The first version of this test asked whether the measured volume was
        // closer to "both slots cut" than to "neither cut". A mutation removing
        // ONE of the two cuts passed it, because one slot missing lands exactly
        // halfway between those two figures and the comparison becomes a coin
        // flip. Asymmetric slots is the most plausible defect this model has:
        // the two cuts are near-identical call sites differing by a sign.
        //
        // So: absolute deviation, with the bound set at half of one slot. That
        // is derived from the failure being guarded against rather than picked
        // — anything that loses a whole slot cannot hide inside it.
        CameraMountParams o = CameraMountParams.ODefault();
        CameraMountRunResult oResult = ORun(o);

        double fSlotX = o.SlotClearanceMm + o.GripDepthMm;
        double fSlotY = o.BoardThicknessMm + 2 * o.SlotClearanceMm;
        double fOneSlot = fSlotX * fSlotY * o.GripHeightMm;
        double fBound = fOneSlot / 2.0;

        double fAnalytic = FAnalyticVolumeCubicMm(o);
        double fMeasured = oResult.Geometry.VolumeCubicMm;
        double fError = Math.Abs(fMeasured - fAnalytic);

        Assert.True(
            fError < fBound,
            $"analytic {fAnalytic:F1} mm^3, voxel {fMeasured:F1} mm^3, deviation {fError:F1} mm^3 "
            + $"({fError / fAnalytic * 100:F2} %), bound {fBound:F1} mm^3 (half of one "
            + $"{fOneSlot:F1} mm^3 slot). A deviation this large is not discretization — a slot "
            + "is missing, misplaced, or the wrong size.");
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
    public void TheSidecarCarriesNoTimestamp()
    {
        // Byte-identical reruns depend on it: the clock lives in the ledger.
        CameraMountRunResult oResult = ORun();
        string strSidecar = File.ReadAllText(
            Path.Combine(
                StrArtifacts,
                oResult.ProvenanceSha256[..2],
                oResult.ProvenanceSha256 + ".provenance.json"));
        Assert.DoesNotContain("created", strSidecar);
        Assert.Contains("measured 2026-09-25 with calipers", strSidecar);
    }

    /// <summary>
    /// Closed form for the default geometry, re-derived here rather than
    /// imported from the model so the two can disagree.
    ///
    /// Base slab, plus two uprights standing on it, less the two slots.
    /// </summary>
    internal static double FAnalyticVolumeCubicMm(CameraMountParams p)
    {
        double fOuterX = p.BoardWidthMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fOuterY = p.BaseFrontMm + p.BaseBackMm;
        double fAboveBaseZ = p.SeatLiftMm + p.GripHeightMm;

        double fBase = fOuterX * fOuterY * p.BaseThicknessMm;

        // Two uprights: each spans WallMm + SlotClearanceMm + GripDepthMm in X,
        // and the slot depth plus two walls in Y.
        double fUprightX = p.WallMm + p.SlotClearanceMm + p.GripDepthMm;
        double fUprightY = p.BoardThicknessMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fUprights = 2.0 * fUprightX * fUprightY * fAboveBaseZ;

        // Each slot removes its own box from the upright it is cut into.
        double fSlotX = p.SlotClearanceMm + p.GripDepthMm;
        double fSlotY = p.BoardThicknessMm + 2 * p.SlotClearanceMm;
        double fSlots = 2.0 * fSlotX * fSlotY * p.GripHeightMm;

        return fBase + fUprights - fSlots;
    }
}
