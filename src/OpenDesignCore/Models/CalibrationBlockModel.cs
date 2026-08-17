using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Models;

public sealed record CalibrationBlockParams
{
    /// <summary>Nominal X, Y and Z in mm. Deliberately not equal — see the model docs.</summary>
    public required float XMm { get; init; }
    public required float YMm { get; init; }
    public required float ZMm { get; init; }
}

/// <summary>
/// A block for measuring what the printer actually did.
///
/// This exists to be printed, measured, and compared against itself, closing
/// the design → print → measure → compensate loop. Everything about it is
/// chosen for measurability rather than for being interesting geometry.
///
/// <b>The axes are deliberately unequal.</b> A calibration *cube* is the
/// common choice and it is the wrong one: at 20×20×20 a measurement taken
/// across the wrong pair of faces is indistinguishable from a correct one, so
/// a transposed reading silently becomes a compensation applied to the wrong
/// axis. With 20×30×15 there is no way to confuse them — the number tells you
/// which face you measured. That is the same orientation-sensitivity the
/// comparison record already warns about, designed out at the source rather
/// than caveated afterwards.
///
/// <b>Flat, generous faces.</b> Calipers need parallel surfaces and somewhere
/// to sit. No chamfers, no fillets, no branding: every feature is a place for
/// a measurement to go wrong, and this part's only job is to be measured.
///
/// <b>Small.</b> At these dimensions it prints in well under an hour in any
/// material, because a calibration part nobody wants to wait for is a
/// calibration part nobody prints.
///
/// What it does <i>not</i> carry: holes, thin walls, overhangs, or a first
/// layer designed to reveal squish. Those measure different properties and
/// belong in different parts. This one measures bulk dimensional accuracy,
/// which is what a shrinkage compensation is derived from.
/// </summary>
public static class CalibrationBlockModel
{
    public const string StrModelId = "calibration-block/0.1";

    /// <summary>
    /// Sensible defaults: unequal on every axis, and no two dimensions within
    /// 5 mm of each other so a mis-read is obvious at a glance.
    /// </summary>
    public static CalibrationBlockParams ODefault() => new()
    {
        XMm = 20f,
        YMm = 30f,
        ZMm = 15f,
    };

    /// <summary>
    /// Largest useful voxel size for this block — and, importantly, <b>not</b>
    /// a function of how accurately it will be measured.
    ///
    /// The first version of this tied the floor to the instrument: one tenth
    /// of the caliper's accuracy, on the reasoning that grid quantisation must
    /// sit well below instrument error. That sounded rigorous and was wrong,
    /// expensively — 0.02 mm calipers implied 0.002 mm voxels, which for a
    /// 20×30×15 block is over 10¹¹ voxels. It consumed 21 GB before being
    /// killed.
    ///
    /// The reasoning was wrong because <b>the comparison does not use the
    /// requested dimensions</b>. It uses the exported artifact's own measured
    /// bounding box, recorded in provenance since schema 0.2. If the grid
    /// shifts a face by a third of a voxel, the export measures 20.007 mm and
    /// the comparison compares against 20.007 mm. Quantisation is measured
    /// out, not assumed away, so it never reaches the deviation figure.
    ///
    /// What the voxel size must actually do is keep the block a block: fine
    /// enough that the faces are flat and the part is recognisably the
    /// dimensions asked for. A fiftieth of the smallest edge does that with
    /// room to spare, and for a 15 mm edge lands at 0.3 mm — the same order as
    /// every other model here.
    /// </summary>
    public static float FResolutionFloorMm(CalibrationBlockParams oParams)
        => Math.Min(Math.Min(oParams.XMm, oParams.YMm), oParams.ZMm) / 50f;

    public static Voxels VoxBuild(Library oLib, CalibrationBlockParams oParams, float fVoxelSizeMm)
    {
        if (oParams.XMm <= 0 || oParams.YMm <= 0 || oParams.ZMm <= 0)
            throw new ArgumentException("All block dimensions must be > 0.");

        // Refusing equal axes is the whole point of the model. A caller who
        // genuinely wants a cube is asking for a part whose measurements
        // cannot be told apart, and should say so somewhere this can see.
        if (oParams.XMm == oParams.YMm || oParams.YMm == oParams.ZMm || oParams.XMm == oParams.ZMm)
        {
            throw new ArgumentException(
                $"{StrModelId}: two axes are equal ({oParams.XMm} x {oParams.YMm} x {oParams.ZMm} mm). "
                + "This model refuses that on purpose: with equal dimensions a measurement taken "
                + "across the wrong faces is indistinguishable from a correct one, and the "
                + "resulting compensation gets applied to an axis that was never measured.");
        }

        return new Voxels(Utils.mshCreateCube(
            oLib,
            new Vector3(oParams.XMm, oParams.YMm, oParams.ZMm),
            new Vector3(0, 0, oParams.ZMm / 2)));
    }

    /// <summary>
    /// Validation gate: the exported block must be the block that was asked
    /// for, within the grid's own quantisation.
    ///
    /// This matters more here than for a functional part. Every other model's
    /// dimensional error is a fit problem someone notices; this one's becomes
    /// a *compensation*, which is then applied to every subsequent print in
    /// that material. An error here does not stay here.
    /// </summary>
    public static void Validate(Voxels voxResult, CalibrationBlockParams oParams, float fVoxelSizeMm)
    {
        if (voxResult.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId}: result voxel field is empty.");

        BBox3 oBox = voxResult.oCalculateBoundingBox();
        Vector3 vecSize = oBox.vecSize();
        float fTol = 2 * fVoxelSizeMm;

        if (Math.Abs(vecSize.X - oParams.XMm) > fTol
            || Math.Abs(vecSize.Y - oParams.YMm) > fTol
            || Math.Abs(vecSize.Z - oParams.ZMm) > fTol)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: bounding box {vecSize.X:F3} x {vecSize.Y:F3} x {vecSize.Z:F3} mm "
                + $"deviates from the requested {oParams.XMm:F3} x {oParams.YMm:F3} x {oParams.ZMm:F3} mm "
                + $"by more than {fTol:F3} mm (2 voxels). A calibration block that is not the size "
                + "it claims would turn a printer's error and a mesher's error into one number.");
        }
    }
}
