using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Models;

/// <summary>
/// A ladder of bores for finding what diameter actually fits a cylinder, mm
/// (ADR-0004).
/// </summary>
public sealed record LensFitGaugeParams
{
    /// <summary>Diameter of the first (smallest) bore, mm.</summary>
    public required float BoreStartMm { get; init; }

    /// <summary>Increment between consecutive bores, mm.</summary>
    public required float BoreStepMm { get; init; }

    /// <summary>How many bores.</summary>
    public required int BoreCount { get; init; }

    /// <summary>Centre-to-centre spacing along the plate, mm.</summary>
    public required float BoreSpacingMm { get; init; }

    /// <summary>Material between the largest bore and the plate edge, mm.</summary>
    public required float EdgeMarginMm { get; init; }

    /// <summary>Plate thickness, mm.</summary>
    public required float PlateThicknessMm { get; init; }

    /// <summary>Diameter of the tally dots that number each bore, mm.</summary>
    public required float TallyDotMm { get; init; }

    /// <summary>
    /// Sized for the LILYGO T-CameraPlus-S3 V1.1's lens barrel, measured at
    /// 14.95 mm across on 2026-09-25.
    ///
    /// <para>
    /// The ladder starts AT the measured diameter and only goes up, because a
    /// printed hole comes out undersize far more often than over: the nozzle
    /// lays its bead on the inside of the curve and the plastic pulls inward as
    /// it cools. 14.95 is therefore expected to be the one that does not fit,
    /// and it is included so that expectation is tested rather than assumed.
    /// </para>
    ///
    /// <para>
    /// The plate is deliberately thin. Its job is to find a diameter, and how
    /// much barrel is available to pass through one is a property of the board
    /// that calipers answer directly. A thick gauge would risk failing for the
    /// wrong reason — the barrel bottoming out rather than the hole being the
    /// wrong size — and confound the one variable this part exists to isolate.
    /// </para>
    /// </summary>
    public static LensFitGaugeParams ODefault() => new()
    {
        BoreStartMm = 14.95f,
        BoreStepMm = 0.25f,
        BoreCount = 5,
        BoreSpacingMm = 24.0f,
        EdgeMarginMm = 4.0f,
        PlateThicknessMm = 2.5f,
        TallyDotMm = 2.0f,
    };
}

/// <summary>
/// A flat plate with a ladder of through-bores, for measuring what a printer
/// actually produces when asked for a hole of a given diameter.
///
/// <para>
/// This exists because of a failed part. <c>camera-mount/0.1</c> was designed
/// from the board's outline, printed, and did not fit — and the deeper problem
/// it exposed is that a fixture's critical dimension cannot be verified by
/// anything in this repository before it is printed. Geometry can be checked
/// against a closed form; the gap between a nominal hole and a printed one
/// cannot. It has to be measured on the machine that will make the part.
/// </para>
///
/// <para>
/// Same pattern as <c>calibration-block/0.2</c>: a part whose whole purpose is
/// to be measured, whose provenance records the nominal figures so that the
/// measurement taken days later has something to be a measurement *of*.
/// </para>
///
/// <para>
/// Bores are numbered by tally dots — one small through-hole beside the first
/// bore, two beside the second, and so on. Not text: at a 0.4 mm nozzle
/// embossed digits this small are a lottery, and a gauge whose labels are
/// unreadable tells you a diameter fits without telling you which one.
/// </para>
///
/// <para>
/// Origin: plate bottom at Z = 0, centred in X and Y. All dimensions mm.
/// </para>
/// </summary>
public static class LensFitGaugeModel
{
    public const string StrModelId = "lens-fit-gauge/0.1";

    /// <summary>
    /// Largest voxel size at which this gauge means anything.
    ///
    /// <para>
    /// Not the plate thickness, and not the bore diameter — <b>the step between
    /// bores</b>. The part's entire function is that consecutive bores are
    /// distinguishable, and a voxel size comparable to the step makes two
    /// neighbours round to the same solid, or worse, swaps their order. A
    /// gauge that cannot resolve its own increments reports a fit against a
    /// diameter it did not make.
    /// </para>
    ///
    /// <para>
    /// A quarter of the step, so each increment is at least four voxels wide.
    /// </para>
    /// </summary>
    public static float FResolutionFloorMm(LensFitGaugeParams p) => p.BoreStepMm / 4f;

    /// <summary>Diameter of bore <paramref name="n"/>, zero-based, mm.</summary>
    public static float FBoreDiameterMm(LensFitGaugeParams p, int n) =>
        p.BoreStartMm + n * p.BoreStepMm;

    /// <summary>Largest bore diameter, mm.</summary>
    public static float FLargestBoreMm(LensFitGaugeParams p) =>
        FBoreDiameterMm(p, p.BoreCount - 1);

    /// <summary>Overall X of the plate, mm.</summary>
    public static float FOuterXMm(LensFitGaugeParams p) =>
        (p.BoreCount - 1) * p.BoreSpacingMm + FLargestBoreMm(p) + 2 * p.EdgeMarginMm;

    /// <summary>Overall Y of the plate, mm.</summary>
    public static float FOuterYMm(LensFitGaugeParams p) =>
        FLargestBoreMm(p) + 2 * p.EdgeMarginMm + p.TallyDotMm + p.EdgeMarginMm;

    /// <summary>X centre of bore <paramref name="n"/>, zero-based, mm.</summary>
    public static float FBoreCentreXMm(LensFitGaugeParams p, int n) =>
        -FOuterXMm(p) / 2f + p.EdgeMarginMm + FLargestBoreMm(p) / 2f + n * p.BoreSpacingMm;

    /// <summary>Y centre of the bore row, mm. The tally row sits below it.</summary>
    public static float FBoreCentreYMm(LensFitGaugeParams p) =>
        -FOuterYMm(p) / 2f + p.EdgeMarginMm + FLargestBoreMm(p) / 2f;

    public static Voxels VoxBuild(Library oLib, LensFitGaugeParams p, float fVoxelSizeMm)
    {
        ValidateParams(p);

        float fFloor = FResolutionFloorMm(p);
        if (fVoxelSizeMm > fFloor)
        {
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor "
                + $"{fFloor:F4} mm — a quarter of the {p.BoreStepMm} mm step between bores. "
                + "Coarser than that and neighbouring bores round to the same hole, or come "
                + "out in the wrong order, and the gauge reports a fit against a diameter it "
                + "did not make; refusing (ADR-0003).");
        }

        float fOuterX = FOuterXMm(p);
        float fOuterY = FOuterYMm(p);
        float fZ = p.PlateThicknessMm;

        Voxels voxPlate = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fZ), new Vector3(0, 0, fZ / 2)));

        // Beams run past both faces so their hemispherical caps land outside the
        // plate and what passes through it is a true cylinder. One radius plus a
        // voxel is enough; the overshoot below is generous on purpose.
        float fOver = 2 * fVoxelSizeMm + 1.0f;
        float fBoreY = FBoreCentreYMm(p);
        float fTallyY = fBoreY + FLargestBoreMm(p) / 2f + p.EdgeMarginMm / 2f + p.TallyDotMm / 2f;

        for (int n = 0; n < p.BoreCount; n++)
        {
            float fRadius = FBoreDiameterMm(p, n) / 2f;
            float fX = FBoreCentreXMm(p, n);

            voxPlate -= VoxBore(oLib, fX, fBoreY, fRadius, fZ, fOver);

            // n + 1 tally dots, so the first bore is one dot rather than none —
            // "no dots" and "the dots did not print" look identical.
            float fDotPitch = p.TallyDotMm * 2f;
            float fRowWidth = n * fDotPitch;
            for (int d = 0; d <= n; d++)
            {
                float fDotX = fX - fRowWidth / 2f + d * fDotPitch;
                voxPlate -= VoxBore(oLib, fDotX, fTallyY, p.TallyDotMm / 2f, fZ, fOver);
            }
        }

        return voxPlate;
    }

    /// <summary>A through-bore at (x, y) of the given radius, overshooting both faces.</summary>
    private static Voxels VoxBore(
        Library oLib, float fX, float fY, float fRadius, float fPlateZ, float fOver)
    {
        // The caps add one radius beyond each axis endpoint, so the endpoints are
        // pulled in by the radius and then pushed out by the overshoot.
        float fZLo = -fRadius - fOver;
        float fZHi = fPlateZ + fRadius + fOver;
        return Voxels.voxLatticeBeam(
            oLib,
            new Vector3(fX, fY, fZLo), fRadius,
            new Vector3(fX, fY, fZHi), fRadius);
    }

    private static void ValidateParams(LensFitGaugeParams p)
    {
        if (p.BoreCount < 2)
            throw new ArgumentException("A ladder needs at least two bores to be a ladder.");
        if (p.BoreStartMm <= 0 || p.BoreStepMm <= 0)
            throw new ArgumentException("BoreStartMm and BoreStepMm must be > 0.");
        if (p.PlateThicknessMm <= 0 || p.EdgeMarginMm <= 0 || p.TallyDotMm <= 0)
            throw new ArgumentException("PlateThicknessMm, EdgeMarginMm and TallyDotMm must be > 0.");

        // Bores must not run into each other, or the ladder becomes a slot.
        float fLargest = FLargestBoreMm(p);
        if (p.BoreSpacingMm <= fLargest + 2 * p.EdgeMarginMm)
        {
            throw new ArgumentException(
                $"BoreSpacingMm {p.BoreSpacingMm} mm leaves less than {2 * p.EdgeMarginMm} mm "
                + $"of material between {fLargest} mm bores. Adjacent bores would break into "
                + "each other and the gauge would measure a slot.");
        }

        // The widest tally row must fit within its bore's spacing.
        float fWidestRow = (p.BoreCount - 1) * p.TallyDotMm * 2f + p.TallyDotMm;
        if (fWidestRow >= p.BoreSpacingMm)
        {
            throw new ArgumentException(
                $"The {p.BoreCount}-dot tally row spans {fWidestRow:F2} mm, which does not fit "
                + $"the {p.BoreSpacingMm} mm bore spacing; the tallies would merge and stop "
                + "identifying anything.");
        }
    }

    /// <summary>
    /// Validation gate. Bounding box against the analytic plate, plus a check
    /// that the bores actually removed material — a plate with no holes has the
    /// right bounding box and is not a gauge.
    /// </summary>
    public static void Validate(Voxels voxResult, LensFitGaugeParams p, float fVoxelSizeMm)
    {
        if (voxResult.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId}: result voxel field is empty.");

        float fOuterX = FOuterXMm(p);
        float fOuterY = FOuterYMm(p);
        float fZ = p.PlateThicknessMm;

        voxResult.CalculateProperties(out float fVolume, out BBox3 oBox);
        Vector3 vecSize = oBox.vecSize();
        float fTol = 2 * fVoxelSizeMm;

        if (Math.Abs(vecSize.X - fOuterX) > fTol
            || Math.Abs(vecSize.Y - fOuterY) > fTol
            || Math.Abs(vecSize.Z - fZ) > fTol)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: bounding box {vecSize.X:F2} x {vecSize.Y:F2} x {vecSize.Z:F2} mm "
                + $"deviates from expected {fOuterX:F2} x {fOuterY:F2} x {fZ:F2} mm "
                + $"by more than {fTol:F2} mm (2 voxels).");
        }

        double fSolid = (double)fOuterX * fOuterY * fZ;
        double fExpectedRemoved = FExpectedBoreVolumeCubicMm(p);
        double fActualRemoved = fSolid - fVolume;

        // Half a bore, for the same reason camera-mount uses half a slot: the
        // bound comes from the failure being guarded against rather than taste.
        double fSmallestBore = Math.PI * Math.Pow(p.BoreStartMm / 2.0, 2) * p.PlateThicknessMm;
        if (Math.Abs(fActualRemoved - fExpectedRemoved) > fSmallestBore / 2.0)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: the bores removed {fActualRemoved:F1} mm³ where "
                + $"{fExpectedRemoved:F1} mm³ was expected — a discrepancy larger than half of "
                + $"the smallest bore ({fSmallestBore:F1} mm³). A bore is missing, the wrong "
                + "size, or did not go through.");
        }
    }

    /// <summary>Material the bores and tally dots should remove, mm³.</summary>
    public static double FExpectedBoreVolumeCubicMm(LensFitGaugeParams p)
    {
        double fTotal = 0;
        for (int n = 0; n < p.BoreCount; n++)
        {
            double fR = FBoreDiameterMm(p, n) / 2.0;
            fTotal += Math.PI * fR * fR * p.PlateThicknessMm;
            // n + 1 tally dots of the same depth.
            fTotal += (n + 1) * Math.PI * Math.Pow(p.TallyDotMm / 2.0, 2) * p.PlateThicknessMm;
        }
        return fTotal;
    }
}
