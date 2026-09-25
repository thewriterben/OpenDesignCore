using OpenDesignCore.Models;
using PicoGK;
using Xunit;
using Xunit.Abstractions;

namespace OpenDesignCore.Tests.Reference;

/// <summary>
/// Reference case: the camera mount's voxel-derived volume against its exact
/// analytic volume.
///
/// <para>
/// The mount is a rectangular base with two rectangular uprights standing on
/// it, each with a rectangular slot cut into it. Every constituent is a box, so
/// the volume has a closed form in the model's own parameters and the gap
/// between that and what <see cref="Voxels.CalculateProperties"/> reports is
/// discretization error and nothing else.
/// </para>
///
/// <para>
/// It is a more interesting case than the enclosure tray for one reason: the
/// enclosure is one box minus one box, while this is five boxes with two
/// internal cavities that open onto two different faces each. A closed form
/// that got the openings wrong would still produce a plausible number, which is
/// why the surface area below is derived rather than estimated.
/// </para>
/// </summary>
public sealed class CameraMountVolumeTest(ITestOutputHelper oOutput)
{
    /// <summary>
    /// Exact volume: base slab, plus two uprights, less the two slots.
    ///
    /// Each slot lies wholly inside its upright — flush with the inboard face
    /// and the top, but with a wall of material outboard, front, back and below
    /// — so it subtracts its whole box exactly once and there is no
    /// intersection term.
    /// </summary>
    private static double FExactVolumeCubicMm(CameraMountParams p)
    {
        double fBaseX = p.BoardWidthMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fBaseY = p.BaseFrontMm + p.BaseBackMm;

        double fUx = p.WallMm + p.SlotClearanceMm + p.GripDepthMm;
        double fUy = p.BoardThicknessMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fUz = p.SeatLiftMm + p.GripHeightMm;

        double fSx = p.SlotClearanceMm + p.GripDepthMm;
        double fSy = p.BoardThicknessMm + 2 * p.SlotClearanceMm;

        return (fBaseX * fBaseY * p.BaseThicknessMm)
             + (2 * fUx * fUy * fUz)
             - (2 * fSx * fSy * p.GripHeightMm);
    }

    /// <summary>
    /// Exact surface area of the same solid. Used only to size the error bound:
    /// discretization error lives on the surface, so a bound that ignored area
    /// would be a constant in disguise.
    ///
    /// <para>
    /// The slots contribute less than they look like they should, and the
    /// cancellation is exact. A slot removes its opening from the upright's
    /// inboard face (<c>sy·sz</c>) and its mouth from the upright's top
    /// (<c>sx·sy</c>); it adds its far wall (<c>sy·sz</c>), its floor
    /// (<c>sx·sy</c>) and its two side walls (<c>2·sx·sz</c>). The first two
    /// pairs cancel term for term, so the net contribution of each slot is
    /// exactly its two side walls.
    /// </para>
    /// </summary>
    private static double FExactSurfaceAreaSqMm(CameraMountParams p)
    {
        double fBaseX = p.BoardWidthMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fBaseY = p.BaseFrontMm + p.BaseBackMm;

        double fUx = p.WallMm + p.SlotClearanceMm + p.GripDepthMm;
        double fUy = p.BoardThicknessMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;
        double fUz = p.SeatLiftMm + p.GripHeightMm;

        double fSx = p.SlotClearanceMm + p.GripDepthMm;

        double fBaseBottom = fBaseX * fBaseY;
        double fBaseSides = 2 * (fBaseX + fBaseY) * p.BaseThicknessMm;
        // The uprights stand on the base, so their footprints are not exposed.
        double fBaseTop = (fBaseX * fBaseY) - (2 * fUx * fUy);
        double fUprights = 2 * ((2 * (fUx + fUy) * fUz) + (fUx * fUy));
        double fSlots = 2 * (2 * fSx * p.GripHeightMm);

        return fBaseBottom + fBaseSides + fBaseTop + fUprights + fSlots;
    }

    private static double FMeasuredVolumeCubicMm(CameraMountParams p, float fVoxelSizeMm)
    {
        using Library oLib = new(fVoxelSizeMm);
        Voxels voxMount = CameraMountModel.VoxBuild(oLib, p, fVoxelSizeMm);
        voxMount.CalculateProperties(out float fVolume, out BBox3 _);
        return fVolume;
    }

    [Fact]
    public void VoxelVolumeMatchesAnalyticVolumeWithinOneVoxelOfSurface()
    {
        CameraMountParams oParams = CameraMountParams.ODefault();
        const float fVoxelSizeMm = 0.30f;

        double fExact = FExactVolumeCubicMm(oParams);
        double fArea = FExactSurfaceAreaSqMm(oParams);

        // Same argument as EnclosureVolumeTest: a signed-distance field places
        // the interface within a band around the true surface, so the worst the
        // representation permits is being wrong by about one voxel of material
        // everywhere on that surface. Scales with two declared inputs, so it is
        // not an epsilon. A ceiling, not a claim about PicoGK's accuracy.
        double fBound = fArea * fVoxelSizeMm;

        double fMeasured = FMeasuredVolumeCubicMm(oParams, fVoxelSizeMm);
        double fError = Math.Abs(fMeasured - fExact);

        oOutput.WriteLine(
            $"analytic {fExact:F3} mm³, voxel {fMeasured:F3} mm³, "
            + $"error {fError:F3} mm³ ({fError / fExact * 100:F3} %), "
            + $"bound {fBound:F3} mm³ (area {fArea:F1} mm² × voxel {fVoxelSizeMm} mm), "
            + $"{fError / fBound * 100:F2} % of bound");

        Assert.True(
            fError <= fBound,
            $"Voxel volume {fMeasured:F3} mm³ differs from the analytic {fExact:F3} mm³ by "
            + $"{fError:F3} mm³, beyond the {fBound:F3} mm³ a one-voxel band over "
            + $"{fArea:F1} mm² of surface permits. That is not discretization — it is a "
            + "geometry or measurement defect.");
    }

    /// <summary>
    /// The convergence half of the reference-case contract, which
    /// <c>tests/reference/README.md</c> records as missing: "halving the voxel
    /// size should move the answer toward the reference".
    ///
    /// <para>
    /// The assertion here is the weakest defensible form of that — the finer
    /// run's error is smaller than the coarser run's — deliberately. The README
    /// explains why a rate assertion is not written yet: an unrun convergence
    /// check fails identically for a genuine bug and for an error already down
    /// at float noise, and telling those apart needs measured figures. So this
    /// reports the ratio on every run and asserts only the direction. Tighten it
    /// to a rate once several runs have produced the number, and never to
    /// whatever one run happened to give.
    /// </para>
    /// </summary>
    [Fact]
    public void RefiningTheVoxelSizeMovesTheAnswerTowardTheReference()
    {
        CameraMountParams oParams = CameraMountParams.ODefault();
        const float fCoarseMm = 0.30f;
        const float fFineMm = 0.15f;

        double fExact = FExactVolumeCubicMm(oParams);

        double fCoarse = FMeasuredVolumeCubicMm(oParams, fCoarseMm);
        double fFine = FMeasuredVolumeCubicMm(oParams, fFineMm);

        double fErrCoarse = Math.Abs(fCoarse - fExact);
        double fErrFine = Math.Abs(fFine - fExact);

        oOutput.WriteLine(
            $"analytic {fExact:F3} mm³\n"
            + $"  voxel {fCoarseMm} mm -> {fCoarse:F3} mm³, error {fErrCoarse:F3} mm³ "
            + $"({fErrCoarse / fExact * 100:F3} %)\n"
            + $"  voxel {fFineMm} mm -> {fFine:F3} mm³, error {fErrFine:F3} mm³ "
            + $"({fErrFine / fExact * 100:F3} %)\n"
            + $"  error ratio coarse/fine {fErrCoarse / Math.Max(fErrFine, 1e-9):F3} "
            + "(linear convergence in voxel size would give ~2)");

        Assert.True(
            fErrFine < fErrCoarse,
            $"Halving the voxel size from {fCoarseMm} to {fFineMm} mm did not move the answer "
            + $"toward the analytic volume: error went {fErrCoarse:F3} -> {fErrFine:F3} mm³. "
            + "A case that passes at one resolution and diverges as you refine is a failing "
            + "case (tests/reference/README.md).");
    }
}
