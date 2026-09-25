using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Models;

/// <summary>
/// Board and fixture dimensions for the camera mount, mm (ADR-0004).
///
/// The board figures are MEASURED, not taken from a datasheet, and that is not
/// pedantry. LILYGO publish 60 x 32 x 12 mm with four 2 mm positioning holes
/// for the T-CameraPlus-S3. The board on this bench measures 68.25 x 27.90 with
/// a 29.40 mm total depth and has TWO holes. Longer, narrower, two and a half
/// times deeper, half the holes — the published figure does not describe this
/// revision at all. Same failure as the pin map: V1.2 is what is documented,
/// V1.1 is what is on the desk.
/// </summary>
public sealed record CameraMountParams
{
    /// <summary>PCB width across the short axis, mm. Sits horizontally (X).</summary>
    public required float BoardWidthMm { get; init; }

    /// <summary>PCB length along the long axis, mm. Stands vertically (Z).</summary>
    public required float BoardLengthMm { get; init; }

    /// <summary>Bare PCB thickness, mm. The slot is cut for this plus clearance.</summary>
    public required float BoardThicknessMm { get; init; }

    /// <summary>
    /// Lens tip to PCB front face, mm. Decides how far forward the base must
    /// reach: the board is 1.25 mm of plate with this much mass hanging off it.
    /// </summary>
    public required float LensProtrusionMm { get; init; }

    /// <summary>Outer diameter of the lens barrel, mm. Checked against the gap between uprights.</summary>
    public required float LensDiameterMm { get; init; }

    /// <summary>Lens tip to the back of the display/speaker stack, mm.</summary>
    public required float TotalDepthMm { get; init; }

    /// <summary>Clearance per side between PCB and slot wall, mm.</summary>
    public required float SlotClearanceMm { get; init; }

    /// <summary>How far each slot grips inboard of the PCB edge, mm.</summary>
    public required float GripDepthMm { get; init; }

    /// <summary>Upright and slot-wall thickness, mm.</summary>
    public required float WallMm { get; init; }

    /// <summary>Base plate thickness, mm.</summary>
    public required float BaseThicknessMm { get; init; }

    /// <summary>Base front edge to PCB mid-plane, mm. Must cover the lens overhang.</summary>
    public required float BaseFrontMm { get; init; }

    /// <summary>PCB mid-plane to base back edge, mm. Must cover the display stack.</summary>
    public required float BaseBackMm { get; init; }

    /// <summary>Base top to the slot floor, mm. Lifts the PCB clear of the base.</summary>
    public required float SeatLiftMm { get; init; }

    /// <summary>Height of PCB edge held in the slots, mm.</summary>
    public required float GripHeightMm { get; init; }

    /// <summary>
    /// The bench board as measured 2026-09-25 with calipers, with fixture
    /// dimensions chosen for a 0.4 mm nozzle. The board figures are measurements
    /// of one physical board; the fixture figures are choices.
    /// </summary>
    public static CameraMountParams ODefault() => new()
    {
        // Measured: LILYGO T-CameraPlus-S3 V1.1, node obc-esp32-s3-003.
        BoardWidthMm = 27.90f,
        BoardLengthMm = 68.25f,
        BoardThicknessMm = 1.25f,
        LensProtrusionMm = 24.00f,
        LensDiameterMm = 14.95f,
        TotalDepthMm = 29.40f,
        // Also measured and deliberately NOT used here: two 2 mm holes on the
        // top edge, 22.80 mm apart in X. Locating pins would be a second way to
        // hold the board and this model has one. Recorded so a later revision
        // does not have to re-measure; absent from the params so nothing carries
        // a number nothing reads.
        SlotClearanceMm = 0.20f,
        GripDepthMm = 3.00f,
        WallMm = 2.40f,
        BaseThicknessMm = 4.00f,
        BaseFrontMm = 32.00f,
        BaseBackMm = 13.00f,
        SeatLiftMm = 3.00f,
        GripHeightMm = 40.00f,
    };
}

/// <summary>
/// A bench fixture that holds the camera board still.
///
/// Two grooved uprights on a forward-reaching base. The PCB drops into a slot in
/// each upright from above; front and back are open, so nothing touches the lens
/// or the display. There are no fasteners — a friction slot returns the board to
/// the same pose every time, and every screw is something that works loose
/// between runs.
///
/// Why it exists: `camera_nudge` is a class the vision detector reports
/// (Oh-Ben-Claw `docs/VISION-DETECTOR-2026-09.md`), and the whole job of this
/// part is to make that class rare. That also gives it an acceptance test —
/// knock the desk with and without the fixture and compare the `edge` score —
/// rather than a subjective "feels stiffer".
///
/// Origin: base bottom at Z = 0, centered in X, with Y = 0 at the PCB
/// mid-plane. The lens points toward -Y. All dimensions mm (ADR-0004).
/// </summary>
public static class CameraMountModel
{
    public const string StrModelId = "camera-mount/0.1";

    /// <summary>
    /// Largest voxel size at which this model is meaningful.
    ///
    /// Two features compete and the tighter one wins. The thinnest SOLID is the
    /// upright wall; the narrowest VOID is the slot, and a slot that closes up
    /// is a fixture the board will not go into. Both need at least two voxels
    /// across, so the floor is half the smaller of them.
    /// </summary>
    public static float FResolutionFloorMm(CameraMountParams oParams)
    {
        float fSlotMm = oParams.BoardThicknessMm + 2 * oParams.SlotClearanceMm;
        return Math.Min(oParams.WallMm / 2f, fSlotMm / 2f);
    }

    /// <summary>Overall X of the finished fixture, mm.</summary>
    public static float FOuterXMm(CameraMountParams p) =>
        p.BoardWidthMm + 2 * p.SlotClearanceMm + 2 * p.WallMm;

    /// <summary>Overall Y of the finished fixture, mm.</summary>
    public static float FOuterYMm(CameraMountParams p) => p.BaseFrontMm + p.BaseBackMm;

    /// <summary>Overall Z of the finished fixture, mm.</summary>
    public static float FOuterZMm(CameraMountParams p) =>
        p.BaseThicknessMm + p.SeatLiftMm + p.GripHeightMm;

    public static Voxels VoxBuild(Library oLib, CameraMountParams p, float fVoxelSizeMm)
    {
        ValidateParams(p);

        float fFloor = FResolutionFloorMm(p);
        if (fVoxelSizeMm > fFloor)
        {
            float fSlotMm = p.BoardThicknessMm + 2 * p.SlotClearanceMm;
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor "
                + $"{fFloor:F4} mm — half the smaller of the {p.WallMm} mm upright wall and "
                + $"the {fSlotMm:F2} mm slot. Running coarser would thin the wall or close "
                + "the slot, and a slot the board does not fit is not a fixture; "
                + "refusing (ADR-0003).");
        }

        float fHalfBoard = p.BoardWidthMm / 2f;
        float fHalfSlotY = p.BoardThicknessMm / 2f + p.SlotClearanceMm;

        // Overshoot so every cut is unambiguous at the boundary it opens onto.
        float fOver = 2 * fVoxelSizeMm + 1.0f;

        float fOuterX = FOuterXMm(p);
        float fOuterY = FOuterYMm(p);
        float fOuterZ = FOuterZMm(p);
        float fBaseCy = (p.BaseBackMm - p.BaseFrontMm) / 2f;

        // Start from the full bounding solid and cut everything away. Subtraction
        // only: the three shipped models use `-` and nothing in this tree has
        // exercised a union, so this stays inside the vocabulary that is known
        // to work here.
        Voxels voxMount = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fOuterZ), new Vector3(0, fBaseCy, fOuterZ / 2)));

        // Everything above the base, between the two uprights' inboard faces.
        // This is the gap the board and lens live in.
        float fGapX = p.BoardWidthMm - 2 * p.GripDepthMm;
        voxMount -= VoxCut(oLib,
            fGapX, fOuterY + 2 * fOver, fOuterZ - p.BaseThicknessMm + fOver,
            0, fBaseCy, p.BaseThicknessMm);

        // Everything above the base forward of the uprights: the base reaches out
        // under the lens, but nothing stands up there.
        float fUprightFrontY = -(fHalfSlotY + p.WallMm);
        float fFrontVoidY = (fUprightFrontY + p.BaseFrontMm) + fOver;
        voxMount -= VoxCut(oLib,
            fOuterX + 2 * fOver, fFrontVoidY, fOuterZ - p.BaseThicknessMm + fOver,
            0, fUprightFrontY - fFrontVoidY / 2f, p.BaseThicknessMm);

        // And behind them, clearing the display and speaker stack.
        float fUprightBackY = fHalfSlotY + p.WallMm;
        float fBackVoidY = (p.BaseBackMm - fUprightBackY) + fOver;
        voxMount -= VoxCut(oLib,
            fOuterX + 2 * fOver, fBackVoidY, fOuterZ - p.BaseThicknessMm + fOver,
            0, fUprightBackY + fBackVoidY / 2f, p.BaseThicknessMm);

        // The two slots. Blind at the bottom — the floor is the seat the board
        // rests on — and open at the top so it drops in, and open inboard so the
        // board can reach them.
        float fSlotZ = (fOuterZ - (p.BaseThicknessMm + p.SeatLiftMm)) + fOver;
        float fSlotXOuter = -(fHalfBoard + p.SlotClearanceMm);
        float fSlotXInner = -(fHalfBoard - p.GripDepthMm) + fOver;
        float fSlotX = fSlotXInner - fSlotXOuter;
        float fSlotCx = (fSlotXOuter + fSlotXInner) / 2f;

        voxMount -= VoxCut(oLib,
            fSlotX, 2 * fHalfSlotY, fSlotZ,
            fSlotCx, 0, p.BaseThicknessMm + p.SeatLiftMm);
        voxMount -= VoxCut(oLib,
            fSlotX, 2 * fHalfSlotY, fSlotZ,
            -fSlotCx, 0, p.BaseThicknessMm + p.SeatLiftMm);

        return voxMount;
    }

    /// <summary>A cut box given its size and where its lower face sits in Z.</summary>
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
        if (p.BoardWidthMm <= 0 || p.BoardLengthMm <= 0 || p.BoardThicknessMm <= 0)
            throw new ArgumentException("Board dimensions must all be > 0.");
        if (p.WallMm <= 0 || p.BaseThicknessMm <= 0)
            throw new ArgumentException("WallMm and BaseThicknessMm must be > 0.");
        if (p.SlotClearanceMm < 0)
            throw new ArgumentException("SlotClearanceMm must be >= 0.");
        if (p.GripDepthMm <= 0)
            throw new ArgumentException("GripDepthMm must be > 0.");
        if (p.GripHeightMm <= 0 || p.SeatLiftMm < 0)
            throw new ArgumentException("GripHeightMm must be > 0 and SeatLiftMm >= 0.");

        // The board has to still exist between the two grips.
        float fGapX = p.BoardWidthMm - 2 * p.GripDepthMm;
        if (fGapX <= 0)
        {
            throw new ArgumentException(
                $"GripDepthMm {p.GripDepthMm} mm per side leaves no board between the "
                + $"uprights for a {p.BoardWidthMm} mm board.");
        }

        // ...and so does the lens, which passes between them.
        if (fGapX <= p.LensDiameterMm)
        {
            throw new ArgumentException(
                $"The {fGapX:F2} mm gap between uprights is not wider than the "
                + $"{p.LensDiameterMm} mm lens barrel. The lens sits between them and would "
                + "foul; reduce GripDepthMm.");
        }

        // The base has to actually be under the lens, or the fixture tips forward.
        float fLensTipY = p.BoardThicknessMm / 2f + p.LensProtrusionMm;
        if (p.BaseFrontMm < fLensTipY)
        {
            throw new ArgumentException(
                $"BaseFrontMm {p.BaseFrontMm} mm does not reach the lens tip at "
                + $"{fLensTipY:F2} mm. The board is {p.BoardThicknessMm} mm of plate with "
                + $"{p.LensProtrusionMm} mm of lens hanging off it; a base that stops short "
                + "of the tip is a fixture that falls over.");
        }

        // ...and under the display stack at the back.
        float fBackStackY = p.TotalDepthMm - p.LensProtrusionMm - p.BoardThicknessMm / 2f;
        if (p.BaseBackMm < fBackStackY)
        {
            throw new ArgumentException(
                $"BaseBackMm {p.BaseBackMm} mm does not reach the back of the display stack "
                + $"at {fBackStackY:F2} mm.");
        }

        if (p.GripHeightMm > p.BoardLengthMm)
        {
            throw new ArgumentException(
                $"GripHeightMm {p.GripHeightMm} mm exceeds the {p.BoardLengthMm} mm board.");
        }
    }

    /// <summary>
    /// Validation gate: fails loudly and specifically before any export.
    /// Voxel-derived meshes are closed by construction, so the checks are
    /// emptiness and dimensional sanity against the analytic outer envelope
    /// (within a 2-voxel tolerance band), matching enclosure-shell/0.1.
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
}
