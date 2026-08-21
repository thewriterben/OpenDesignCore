using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Models;

public sealed record CalibrationBlockParams
{
    /// <summary>Nominal X and Y in mm. Deliberately unequal — see the model docs.</summary>
    public required float XMm { get; init; }
    public required float YMm { get; init; }

    /// <summary>Height of the tall region, mm, measured from the bed.</summary>
    public required float ZMm { get; init; }

    /// <summary>
    /// Height of the low shelf, mm. The Z measurement that matters is the
    /// span between these two top faces, not either height on its own.
    /// </summary>
    public required float StepZMm { get; init; }

    /// <summary>Depth of the tall region along Y, mm. The rest is shelf.</summary>
    public required float TallDepthYMm { get; init; }

    /// <summary>The offset-free Z baseline a caliper actually measures.</summary>
    public float ZSpanMm => ZMm - StepZMm;
}

/// <summary>
/// A block for measuring what the printer actually did.
///
/// <b>Why it has a step.</b> The first version was a plain box, and its
/// instructions said to measure Z "across the body, away from the first
/// layer". Benji pointed out that this is impossible: the Z dimension of a
/// bed-printed part *begins* at the first layer, so every external height
/// measurement contains it. The instruction was not merely hard to follow, it
/// was incoherent.
///
/// The fix comes from the two errors being different in kind:
///
/// <list type="bullet">
/// <item>First-layer squish is a <b>constant offset</b> — the same fraction of
///       a millimetre whether the part is 5 mm or 50 mm tall.</item>
/// <item>Shrinkage is <b>proportional</b> — a percentage of the dimension.</item>
/// </list>
///
/// Measure two heights on one part and the constant cancels. With top faces at
/// <c>StepZMm</c> and <c>ZMm</c>, both measured from the bed, both readings
/// carry the same squish; their <i>difference</i> carries none. That difference
/// against its nominal is a clean Z scale factor, and the leftover is the
/// squish itself — which is a useful number in its own right, being the
/// first-layer/elephant's-foot compensation rather than a shrinkage one.
///
/// <b>The X and Y axes are deliberately unequal.</b> A calibration *cube* is
/// the common choice and it is the wrong one: at 20×20×20 a measurement taken
/// across the wrong pair of faces is indistinguishable from a correct one, so
/// a transposed reading silently becomes a compensation applied to the wrong
/// axis. Unequal dimensions make a mis-read visible.
///
/// <b>Bigger is better, within reason.</b> A caliper's error is a fixed number
/// of millimetres, so the same shrinkage is a larger multiple of it on a
/// longer edge. The first real print measured 20 × 30 × 15 with 0.02 mm
/// calipers and Y came out 0.020 mm short — exactly the instrument accuracy,
/// which is to say unmeasurable. The defaults here are larger for that reason.
///
/// <b>Flat, generous faces, no chamfers.</b> Every feature is a place for a
/// measurement to go wrong, and this part's only job is to be measured.
/// </summary>
public static class CalibrationBlockModel
{
    public const string StrModelId = "calibration-block/0.2";

    /// <summary>
    /// Defaults chosen so every reading is comfortably above a 0.02 mm
    /// caliper: no two X/Y dimensions alike, and a 21 mm Z span so the
    /// difference of two height readings still resolves a fraction of a
    /// percent.
    /// </summary>
    public static CalibrationBlockParams ODefault() => new()
    {
        XMm = 40f,
        YMm = 60f,
        ZMm = 25f,
        StepZMm = 4f,
        TallDepthYMm = 25f,
    };

    /// <summary>
    /// Largest useful voxel size — and, importantly, <b>not</b> a function of
    /// how accurately the block will be measured.
    ///
    /// The first version tied this to the instrument: one tenth of the
    /// caliper's accuracy, on the reasoning that grid quantisation must sit
    /// well below instrument error. That sounded rigorous and was wrong,
    /// expensively — 0.02 mm calipers implied 0.002 mm voxels, over 10¹¹ for a
    /// modest block, and it consumed 21 GB before being killed.
    ///
    /// The reasoning was wrong because <b>the comparison does not use the
    /// requested dimensions</b>. It uses the exported artifact's own measured
    /// bounding box, recorded in provenance since schema 0.2. If the grid
    /// shifts a face by a third of a voxel, the export measures 40.007 mm and
    /// the comparison compares against 40.007 mm. Quantisation is measured
    /// out, not assumed away.
    ///
    /// What the voxel size must do is keep the block a block and the step a
    /// clean shelf. A twentieth of the shelf height does both — twenty voxels
    /// through the thinnest deliberate feature is ample, and going finer buys
    /// nothing measurable while costing real time on a part this size.
    /// </summary>
    public static float FResolutionFloorMm(CalibrationBlockParams oParams)
        => oParams.StepZMm / 20f;

    public static Voxels VoxBuild(Library oLib, CalibrationBlockParams oParams, float fVoxelSizeMm)
    {
        if (oParams.XMm <= 0 || oParams.YMm <= 0 || oParams.ZMm <= 0 || oParams.StepZMm <= 0)
            throw new ArgumentException("All block dimensions must be > 0.");

        if (oParams.StepZMm >= oParams.ZMm)
        {
            throw new ArgumentException(
                $"{StrModelId}: the shelf ({oParams.StepZMm} mm) must be lower than the tall "
                + $"region ({oParams.ZMm} mm), or there is no Z span to measure and the "
                + "first-layer offset cannot be separated from shrinkage.");
        }

        if (oParams.TallDepthYMm <= 0 || oParams.TallDepthYMm >= oParams.YMm)
        {
            throw new ArgumentException(
                $"{StrModelId}: the tall region's depth ({oParams.TallDepthYMm} mm) must be "
                + $"between 0 and the block depth ({oParams.YMm} mm), leaving a shelf to "
                + "measure from.");
        }

        // Refusing equal X/Y is the point of the model: with equal dimensions a
        // measurement across the wrong faces is indistinguishable from a
        // correct one, and the compensation lands on an axis nobody measured.
        if (oParams.XMm == oParams.YMm)
        {
            throw new ArgumentException(
                $"{StrModelId}: X and Y are both {oParams.XMm} mm. This model refuses that on "
                + "purpose: with equal dimensions a measurement taken across the wrong faces "
                + "is indistinguishable from a correct one, and the resulting compensation "
                + "gets applied to an axis that was never measured.");
        }

        Voxels voxBlock = new(Utils.mshCreateCube(
            oLib,
            new Vector3(oParams.XMm, oParams.YMm, oParams.ZMm),
            new Vector3(0, 0, oParams.ZMm / 2)));

        // Cut the shelf: remove everything above StepZ over the shelf's depth.
        // Overshoot in X and Z so the cut is unambiguous at the boundary.
        float fShelfDepth = oParams.YMm - oParams.TallDepthYMm;
        float fOvershoot = 2 * fVoxelSizeMm + 1.0f;
        float fCutZ = (oParams.ZMm - oParams.StepZMm) + fOvershoot;

        Voxels voxCut = new(Utils.mshCreateCube(
            oLib,
            new Vector3(oParams.XMm + fOvershoot, fShelfDepth, fCutZ),
            new Vector3(
                0,
                -(oParams.YMm / 2) + (fShelfDepth / 2),
                oParams.StepZMm + (fCutZ / 2))));

        return voxBlock - voxCut;
    }

    /// <summary>
    /// Validation gate: the exported block must be the block that was asked
    /// for, within the grid's own quantisation.
    ///
    /// This matters more here than for a functional part. Another model's
    /// dimensional error is a fit problem someone notices; this one's becomes
    /// a <i>compensation</i>, applied to every subsequent print in that
    /// material. An error here does not stay here.
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
                + $"deviates from the requested {oParams.XMm:F3} x {oParams.YMm:F3} x "
                + $"{oParams.ZMm:F3} mm by more than {fTol:F3} mm (2 voxels). A calibration "
                + "block that is not the size it claims would turn a printer's error and a "
                + "mesher's error into one number.");
        }
    }
}
