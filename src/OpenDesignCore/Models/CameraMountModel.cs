using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using PicoGK;

namespace OpenDesignCore.Models;

/// <summary>
/// The camera board and the fixture that holds it, mm (ADR-0004).
///
/// <para>
/// <b>Board figures are measurements of one physical board, not a datasheet.</b>
/// LILYGO publish 60 x 32 x 12 mm with four 2 mm positioning holes for the
/// T-CameraPlus-S3. This board measures 68.25 x 27.90 x 29.40 with two. The
/// published figure does not describe this revision.
/// </para>
///
/// <para>
/// <b>And the outline is not the whole story — that is what killed v0.1.</b>
/// See <see cref="CameraMountModel"/>'s remarks for the clearance zones this
/// board has and why a fixture must not touch them.
/// </para>
/// </summary>
public sealed record CameraMountParams
{
    /// <summary>Diameter of the lens barrel's cylindrical body, mm.</summary>
    public required float BarrelDiameterMm { get; init; }

    /// <summary>
    /// Bore diameter to cut, mm. <b>Not the barrel diameter.</b> This is the
    /// nominal figure that this printer turns into a hole the barrel fits —
    /// which is what <c>lens-fit-gauge/0.1</c> exists to measure. The gap
    /// between the two is the machine's offset and it is not predictable.
    /// </summary>
    public required float BoreDiameterMm { get; init; }

    /// <summary>Barrel tip to the lip that stops it, mm. The usable free length.</summary>
    public required float BarrelFreeLengthMm { get; init; }

    /// <summary>Diameter across the lip the barrel seats on, mm.</summary>
    public required float LipDiameterMm { get; init; }

    /// <summary>Lens axis to the PCB's bottom edge, mm. Sets how high the axis sits.</summary>
    public required float AxisToBoardBottomMm { get; init; }

    /// <summary>
    /// How far below the PCB's bottom edge the base sits, mm.
    ///
    /// <para>
    /// Not zero, and the reason matters: the bottom edge carries a USB-C
    /// receptacle and two JST connectors. v0.2 chose 1.0 mm believing their
    /// bodies stopped at the edge. The vendor STEP says they overhang it by
    /// 1.60 and 1.65 mm (OpenPartsCore features, V1.0-V1.1), so at 1.0 they sit
    /// inside the base — which the assembly check reports (ADR-0023) and which
    /// is one of the two reasons v0.2 could not be used.
    /// </para>
    /// </summary>
    public required float FloorDropMm { get; init; }

    /// <summary>Thickness of material the barrel passes through, mm.</summary>
    public required float BoreLandMm { get; init; }

    /// <summary>Front plate thickness, mm. The lip pocket is cut into its back.</summary>
    public required float PlateThicknessMm { get; init; }

    /// <summary>Square lip pocket's side, mm. Must clear the lip.</summary>
    public required float LipPocketMm { get; init; }

    /// <summary>Base plate thickness, mm.</summary>
    public required float BaseThicknessMm { get; init; }

    /// <summary>Plate front face to the base's front edge, mm.</summary>
    public required float BaseFrontMm { get; init; }

    /// <summary>Plate back face to the base's back edge, mm.</summary>
    public required float BaseBackMm { get; init; }

    /// <summary>Overall width of base and plate, mm.</summary>
    public required float WidthMm { get; init; }

    /// <summary>Material above the lip pocket before the plate ends, mm.</summary>
    public required float PlateHeadroomMm { get; init; }

    // ── Where the lens sits on the board: needed to place the board's
    //    features for the assembly check (ADR-0023). ──

    /// <summary>Lens tip to the PCB face it stands on, mm. Measured.</summary>
    public required float LensTipFromPcbMm { get; init; }

    /// <summary>
    /// Lens axis to the laminate edge at the registry's low x, mm, in the
    /// board frame of OpenPartsCore's features. Null until measured — and
    /// while it is null the board cannot be placed, so the run refuses.
    /// </summary>
    public float? LensAxisFromLowXEdgeMm { get; init; }

    /// <summary>
    /// True if the lens stands on the laminate face at the registry's z = 0
    /// (the face carrying FPC1), false if on the other. Null until confirmed.
    /// </summary>
    public bool? LensOnLowZFace { get; init; }

    /// <summary>
    /// The bench board as measured with calipers on 2026-09-25, plus fixture
    /// figures chosen for a 0.4 mm nozzle.
    /// </summary>
    public static CameraMountParams ODefault() => new()
    {
        // ── Measured: LILYGO T-CameraPlus-S3 V1.1, node obc-esp32-s3-003 ──
        BarrelDiameterMm = 14.95f,
        BarrelFreeLengthMm = 5.10f,
        LipDiameterMm = 16.65f,
        AxisToBoardBottomMm = 35.00f,
        LensTipFromPcbMm = 24.00f,
        // Not yet measured (asked 2026-09-26): where the axis sits across the
        // board, and which face the lens stands on. Null, not a guess.
        LensAxisFromLowXEdgeMm = null,
        LensOnLowZFace = null,

        // ── Derived from a measurement, no longer a guess ──
        // The barrel is 14.95 and this is not, because a printed hole comes out
        // undersize. How much is a property of the machine, and it was measured
        // on 2026-09-25 with a printed lens-fit-gauge — see
        // LensFitGaugeModel.MeasuredBoreUndersizeMm for the rungs and the
        // uncertainty. Derived rather than written out so the two cannot drift.
        BoreDiameterMm = 14.95f + LensFitGaugeModel.MeasuredBoreUndersizeMm,

        BoreLandMm = 3.00f,      // < 5.10 free length, leaving ~2 mm of barrel proud
        PlateThicknessMm = 6.00f,
        LipPocketMm = 19.00f,    // clears the 16.65 lip
        BaseThicknessMm = 4.00f,
        FloorDropMm = 1.00f,
        BaseFrontMm = 10.00f,
        BaseBackMm = 40.00f,     // the board and display hang ~23 mm behind the plate
        WidthMm = 34.00f,
        PlateHeadroomMm = 4.50f,
    };
}

/// <summary>
/// A bench fixture that holds the camera board still, located on the lens
/// barrel.
///
/// <para>
/// A vertical plate with a bore. The barrel passes through it and its 16.65 mm
/// lip seats in a square pocket in the plate's rear face, so only
/// <c>BoreLandMm</c> of material is in the barrel's way. The board hangs behind,
/// touching nothing but its own bottom edge resting on the base.
/// </para>
///
/// <b>Why v0.1 failed, recorded here because the fixture must not repeat it.</b>
/// <para>
/// v0.1 gripped the board's long edges in slots, 3 mm inboard, for 40 mm. The
/// board's lower region carries a USB-C receptacle and two JST connectors at the
/// very bottom corners — exactly there — so it would not enter. The outline was
/// measured and correct; the <i>surface</i> was never captured. Every geometric
/// check passed, because all of them compared the fixture to its own
/// specification and none could ask whether the specification matched the board.
/// </para>
///
/// <b>And why v0.2 — this version — cannot be used either.</b>
/// <para>
/// The USB-C port points straight down into the base, so no cable can be
/// plugged in, and its body overhangs the board edge far enough to sit inside
/// the base. The zone list below was written from photographs and called the
/// bottom edge safe; the vendor STEP shows it is not. The list is kept as the
/// record of what was believed. The authority is now the board's OpenPartsCore
/// features, which the run checks the fixture against before export (ADR-0023).
/// </para>
///
/// <b>Clearance zones on this board, as written for v0.2 from photographs.</b>
/// <list type="bullet">
/// <item>Bottom edge, both faces: USB-C centre, JST left (GND/3V3/TX/RX),
/// JST right (GND/3V3/SDA/SCL). Bodies overhang the board edge by about 1.6 mm.</item>
/// <item>Rear face, upper half: the display; rear face lower: speaker and power
/// inductors.</item>
/// <item>Front face around the camera: module body, FPC connector, a JST at the
/// right edge, and a twisted pair exiting left.</item>
/// <item><b>Believed safe:</b> the bottom edge itself — wrong, see above — and the
/// lens barrel, which is what this fixture uses.</item>
/// </list>
///
/// <para>
/// Retention is the bore's own friction. A snug slip fit holds a 15 g board
/// without help, which is why <c>lens-fit-gauge/0.1</c>'s criterion — slides in
/// snugly, no force — <i>is</i> the retention specification rather than a
/// separate concern.
/// </para>
///
/// <para>
/// Origin: base bottom at Z = 0, centred in X, with Y = 0 at the plate's front
/// face and the lens pointing toward -Y.
/// </para>
/// </summary>
public static class CameraMountModel
{
    public const string StrModelId = "camera-mount/0.2";

    /// <summary>
    /// Largest voxel size at which this model is meaningful.
    ///
    /// <para>
    /// The bore decides, not the walls. A fixture located on a cylinder is
    /// exactly as good as that cylinder's diameter, and a coarse voxel rounds
    /// the bore to something the barrel rattles in or will not enter. A
    /// hundredth of the bore keeps the discretisation inside the few hundredths
    /// of a millimetre a slip fit actually needs. A twentieth was tried first
    /// and rejected by measurement: at 20 voxels across the bore, the exported
    /// volume missed the analytic by more than half the bore itself.
    /// </para>
    /// </summary>
    public static float FResolutionFloorMm(CameraMountParams p) =>
        Math.Min(p.BoreDiameterMm / 100f, p.BoreLandMm / 10f);

    /// <summary>Height of the lens axis above the base's underside, mm.</summary>
    public static float FAxisHeightMm(CameraMountParams p) =>
        p.BaseThicknessMm + p.FloorDropMm + p.AxisToBoardBottomMm;

    /// <summary>Overall X, mm.</summary>
    public static float FOuterXMm(CameraMountParams p) => p.WidthMm;

    /// <summary>Overall Y, mm.</summary>
    public static float FOuterYMm(CameraMountParams p) =>
        p.BaseFrontMm + p.PlateThicknessMm + p.BaseBackMm;

    /// <summary>Overall Z, mm.</summary>
    public static float FOuterZMm(CameraMountParams p) =>
        FAxisHeightMm(p) + p.LipPocketMm / 2f + p.PlateHeadroomMm;

    /// <summary>The OpenPartsCore entry this fixture holds.</summary>
    public const string StrPartId = "boards/lilygo-t-camera-plus-s3";

    /// <summary>
    /// Where the board sits in this fixture, in the frame of its OpenPartsCore
    /// features (ADR-0023): the lens axis on the bore axis, the lip on the
    /// pocket floor, the board's bottom edge up the fixture's +Z. The laminate
    /// comes from the features' own <c>Board</c> item, so the placement rests
    /// on the registry rather than on a board size typed here.
    ///
    /// <para>
    /// Throws while the lens position is unmeasured. A placement guessed to
    /// let the check run would make its answer a statement about the guess.
    /// </para>
    /// </summary>
    public static PartPlacement OPlacement(CameraMountParams p, PartFeatures oFeatures)
    {
        if (p.LensAxisFromLowXEdgeMm is not float fLensX || p.LensOnLowZFace is not bool bLow)
        {
            throw new ArgumentException(
                $"{StrModelId}: the board cannot be placed for the assembly check: "
                + (p.LensAxisFromLowXEdgeMm is null ? "LensAxisFromLowXEdgeMm is not measured" : "")
                + (p.LensAxisFromLowXEdgeMm is null && p.LensOnLowZFace is null ? " and " : "")
                + (p.LensOnLowZFace is null ? "LensOnLowZFace is not confirmed" : "")
                + ". Measure the lens axis's distance from the board's long edge and confirm which "
                + "face it stands on; refusing to export an unchecked fixture.");
        }
        PartFeature oBoard = oFeatures.Items.FirstOrDefault(i => i.Kind == "board")
            ?? throw new ArgumentException($"{oFeatures.PartId}: features have no board item to place by.");
        (double[] aLo, double[] aHi) = oBoard.OBounds();

        // The lip is the barrel's free length back from the tip, and the tip
        // is LensTipFromPcbMm from the PCB face the lens stands on; the lip
        // seats on the pocket floor at Y = BoreLand.
        float fFaceY = p.BoreLandMm + (p.LensTipFromPcbMm - p.BarrelFreeLengthMm);
        return PartPlacement.OLanding(
            bLow ? Axis.NegX : Axis.PosX,
            Axis.PosZ,
            bLow ? Axis.PosY : Axis.NegY,
            [aLo[0] + fLensX, aLo[1] + p.AxisToBoardBottomMm, bLow ? aLo[2] : aHi[2]],
            new Vector3(0, fFaceY, FAxisHeightMm(p)),
            $"{StrModelId}: lens axis on the bore axis, lip seated on the pocket floor, the board's "
            + "USB edge down; lens position measured on the bench board");
    }

    public static Voxels VoxBuild(Library oLib, CameraMountParams p, float fVoxelSizeMm)
    {
        ValidateParams(p);

        float fFloor = FResolutionFloorMm(p);
        if (fVoxelSizeMm > fFloor)
        {
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor "
                + $"{fFloor:F4} mm for a {p.BoreDiameterMm} mm bore through {p.BoreLandMm} mm "
                + "of land. This fixture is located on a cylinder and is exactly as good as "
                + "that cylinder; a coarse voxel rounds the bore to something the barrel "
                + "rattles in or will not enter; refusing (ADR-0003).");
        }

        float fOuterX = FOuterXMm(p);
        float fOuterY = FOuterYMm(p);
        float fOuterZ = FOuterZMm(p);
        float fAxisZ = FAxisHeightMm(p);
        float fOver = 2 * fVoxelSizeMm + 1.0f;

        // Y = 0 is the plate's front face; the base runs from -BaseFront to
        // +PlateThickness+BaseBack.
        float fYFront = -p.BaseFrontMm;
        float fYPlateBack = p.PlateThicknessMm;
        float fYBack = p.PlateThicknessMm + p.BaseBackMm;
        float fBaseCy = (fYFront + fYBack) / 2f;

        // Bounding solid, then cut. Subtraction only, as every model here does.
        Voxels voxMount = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fOuterZ), new Vector3(0, fBaseCy, fOuterZ / 2)));

        // Above the base, behind the plate: open, so the board hangs in free air.
        float fBehindY = (fYBack - fYPlateBack) + fOver;
        voxMount -= VoxCut(oLib,
            fOuterX + 2 * fOver, fBehindY, fOuterZ - p.BaseThicknessMm + fOver,
            0, fYPlateBack + fBehindY / 2f, p.BaseThicknessMm);

        // Above the base, in front of the plate: open, so the lens is unobstructed.
        float fFrontY = p.BaseFrontMm + fOver;
        voxMount -= VoxCut(oLib,
            fOuterX + 2 * fOver, fFrontY, fOuterZ - p.BaseThicknessMm + fOver,
            0, -fFrontY / 2f, p.BaseThicknessMm);

        // The lip pocket: square, cut into the plate's rear face. Square rather
        // than round because a 19 mm circle only 3 mm deep cannot be made from a
        // lattice beam — its hemispherical caps are wider than the cut is long —
        // and a square pocket clears a round lip perfectly well.
        float fPocketY = (p.PlateThicknessMm - p.BoreLandMm) + fOver;
        voxMount -= VoxCut(oLib,
            p.LipPocketMm, fPocketY, p.LipPocketMm,
            0, p.BoreLandMm + fPocketY / 2f, fAxisZ - p.LipPocketMm / 2f);

        // The bore itself, overshooting both faces so the beam's hemispherical
        // caps land outside the material and what passes through is a cylinder.
        voxMount -= Voxels.voxLatticeBeam(
            oLib,
            new Vector3(0, -fOver, fAxisZ), p.BoreDiameterMm / 2f,
            new Vector3(0, p.PlateThicknessMm + fOver, fAxisZ), p.BoreDiameterMm / 2f);

        return voxMount;
    }

    private static Voxels VoxCut(
        Library oLib,
        float fSizeX, float fSizeY, float fSizeZ,
        float fCx, float fCy, float fZBottom)
        => new(Utils.mshCreateCube(
            oLib,
            new Vector3(fSizeX, fSizeY, fSizeZ),
            new Vector3(fCx, fCy, fZBottom + fSizeZ / 2f)));

    private static void ValidateParams(CameraMountParams p)
    {
        if (p.BarrelDiameterMm <= 0 || p.BoreDiameterMm <= 0 || p.LipDiameterMm <= 0)
            throw new ArgumentException("Barrel, bore and lip diameters must be > 0.");
        if (p.PlateThicknessMm <= 0 || p.BaseThicknessMm <= 0 || p.WidthMm <= 0)
            throw new ArgumentException("Plate, base and width must be > 0.");
        if (p.FloorDropMm < 0 || p.PlateHeadroomMm <= 0)
            throw new ArgumentException("FloorDropMm must be >= 0 and PlateHeadroomMm > 0.");

        // The barrel has to reach through the land, or it never seats.
        if (p.BoreLandMm >= p.BarrelFreeLengthMm)
        {
            throw new ArgumentException(
                $"BoreLandMm {p.BoreLandMm} mm is not less than the barrel's "
                + $"{p.BarrelFreeLengthMm} mm free length. The lip would hit the plate before "
                + "the barrel passed through, and the fixture would hold the board off its "
                + "own datum.");
        }

        // The lip has to fit the pocket, or it hits the plate instead of seating.
        if (p.LipPocketMm <= p.LipDiameterMm)
        {
            throw new ArgumentException(
                $"LipPocketMm {p.LipPocketMm} mm does not clear the {p.LipDiameterMm} mm lip.");
        }

        // The bore has to clear the barrel, and not by so much that it rattles.
        if (p.BoreDiameterMm <= p.BarrelDiameterMm)
        {
            throw new ArgumentException(
                $"BoreDiameterMm {p.BoreDiameterMm} mm does not clear the "
                + $"{p.BarrelDiameterMm} mm barrel. Note this is the NOMINAL figure to cut, "
                + "which must exceed the barrel because a printed hole comes out undersize; "
                + "lens-fit-gauge/0.1 measures by how much.");
        }
        if (p.BoreDiameterMm >= p.LipDiameterMm)
        {
            throw new ArgumentException(
                $"BoreDiameterMm {p.BoreDiameterMm} mm is not smaller than the "
                + $"{p.LipDiameterMm} mm lip. The lip would pass straight through and the "
                + "fixture would have no datum at all.");
        }

        // The pocket must fit in the plate, leaving the land behind it.
        if (p.LipPocketMm / 2f + p.PlateHeadroomMm > FAxisHeightMm(p))
        {
            throw new ArgumentException(
                "The lip pocket reaches below the base; raise the axis or shrink the pocket.");
        }
        if (p.LipPocketMm + 2 * 2.0f > p.WidthMm)
        {
            throw new ArgumentException(
                $"A {p.LipPocketMm} mm pocket leaves under 2 mm of plate either side of a "
                + $"{p.WidthMm} mm plate.");
        }
    }

    /// <summary>
    /// Validation gate: emptiness and dimensional sanity against the analytic
    /// envelope, within a 2-voxel band, matching every other model here.
    /// </summary>
    public static void Validate(Voxels voxResult, CameraMountParams p, float fVoxelSizeMm)
    {
        if (voxResult.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId}: result voxel field is empty.");

        float fOuterX = FOuterXMm(p);
        float fOuterY = FOuterYMm(p);
        float fOuterZ = FOuterZMm(p);

        BBox3 oBox = voxResult.oCalculateBoundingBox();
        Vector3 vecSize = oBox.vecSize();
        float fTol = 2 * fVoxelSizeMm;

        if (Math.Abs(vecSize.X - fOuterX) > fTol
            || Math.Abs(vecSize.Y - fOuterY) > fTol
            || Math.Abs(vecSize.Z - fOuterZ) > fTol)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: bounding box {vecSize.X:F2} x {vecSize.Y:F2} x {vecSize.Z:F2} mm "
                + $"deviates from expected {fOuterX:F2} x {fOuterY:F2} x {fOuterZ:F2} mm "
                + $"by more than {fTol:F2} mm (2 voxels).");
        }
    }

    /// <summary>
    /// Material the fixture should contain, mm³: base slab plus the plate
    /// standing on it, less the lip pocket and the bore through the land.
    /// </summary>
    public static double FAnalyticVolumeCubicMm(CameraMountParams p)
    {
        double fBase = (double)FOuterXMm(p) * FOuterYMm(p) * p.BaseThicknessMm;
        double fPlate = (double)p.WidthMm * p.PlateThicknessMm
                      * (FOuterZMm(p) - p.BaseThicknessMm);
        double fPocket = (double)p.LipPocketMm * p.LipPocketMm
                       * (p.PlateThicknessMm - p.BoreLandMm);
        double fBore = Math.PI * Math.Pow(p.BoreDiameterMm / 2.0, 2) * p.BoreLandMm;
        return fBase + fPlate - fPocket - fBore;
    }
}
