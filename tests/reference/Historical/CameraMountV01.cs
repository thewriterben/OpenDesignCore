using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Tests.Reference.Historical;

/// <summary>
/// camera-mount/0.1 as it was cut (commit a56a7ac), kept only so the assembly
/// check can be shown to reject it. Not a model this engine offers: v0.1 did
/// not fit the board, and this copy exists as evidence of that, not as a part.
///
/// <para>
/// The geometry is <c>CameraMountModel.VoxBuild</c> from that commit with its
/// <c>ODefault()</c> parameters inlined and the validation stripped. Origin:
/// base bottom at Z = 0, centred in X, Y = 0 at the PCB mid-plane, lens toward
/// -Y. The board stands in two slots whose floor — the seat its bottom edge
/// rests on — is at <see cref="SeatZMm"/>.
/// </para>
/// </summary>
internal static class CameraMountV01
{
    // ODefault() at a56a7ac. Board figures were caliper measurements.
    private const float BoardWidthMm = 27.90f, BoardThicknessMm = 1.25f;
    private const float SlotClearanceMm = 0.20f, GripDepthMm = 3.00f, WallMm = 2.40f;
    private const float BaseThicknessMm = 4.00f, BaseFrontMm = 32.00f, BaseBackMm = 13.00f;
    private const float SeatLiftMm = 3.00f, GripHeightMm = 40.00f;

    /// <summary>Height of the slot floor the board's bottom edge rests on.</summary>
    public const float SeatZMm = BaseThicknessMm + SeatLiftMm;

    public static Voxels VoxBuild(Library oLib, float fVoxelSizeMm)
    {
        float fHalfBoard = BoardWidthMm / 2f;
        float fHalfSlotY = BoardThicknessMm / 2f + SlotClearanceMm;
        float fOver = 2 * fVoxelSizeMm + 1.0f;
        float fOuterX = BoardWidthMm + 2 * SlotClearanceMm + 2 * WallMm;
        float fOuterY = BaseFrontMm + BaseBackMm;
        float fOuterZ = BaseThicknessMm + SeatLiftMm + GripHeightMm;
        float fBaseCy = (BaseBackMm - BaseFrontMm) / 2f;

        Voxels vox = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fOuterZ), new Vector3(0, fBaseCy, fOuterZ / 2)));

        float fGapX = BoardWidthMm - 2 * GripDepthMm;
        vox -= VoxCut(oLib, fGapX, fOuterY + 2 * fOver, fOuterZ - BaseThicknessMm + fOver, 0, fBaseCy, BaseThicknessMm);

        float fUprightFrontY = -(fHalfSlotY + WallMm);
        float fFrontVoidY = (fUprightFrontY + BaseFrontMm) + fOver;
        vox -= VoxCut(oLib, fOuterX + 2 * fOver, fFrontVoidY, fOuterZ - BaseThicknessMm + fOver,
            0, fUprightFrontY - fFrontVoidY / 2f, BaseThicknessMm);

        float fUprightBackY = fHalfSlotY + WallMm;
        float fBackVoidY = (BaseBackMm - fUprightBackY) + fOver;
        vox -= VoxCut(oLib, fOuterX + 2 * fOver, fBackVoidY, fOuterZ - BaseThicknessMm + fOver,
            0, fUprightBackY + fBackVoidY / 2f, BaseThicknessMm);

        float fSlotZ = (fOuterZ - (BaseThicknessMm + SeatLiftMm)) + fOver;
        float fSlotXOuter = -(fHalfBoard + SlotClearanceMm);
        float fSlotXInner = -(fHalfBoard - GripDepthMm) + fOver;
        float fSlotX = fSlotXInner - fSlotXOuter;
        float fSlotCx = (fSlotXOuter + fSlotXInner) / 2f;
        vox -= VoxCut(oLib, fSlotX, 2 * fHalfSlotY, fSlotZ, fSlotCx, 0, SeatZMm);
        vox -= VoxCut(oLib, fSlotX, 2 * fHalfSlotY, fSlotZ, -fSlotCx, 0, SeatZMm);
        return vox;
    }

    private static Voxels VoxCut(Library oLib, float fSx, float fSy, float fSz, float fCx, float fCy, float fZBottom)
        => new(Utils.mshCreateCube(oLib, new Vector3(fSx, fSy, fSz), new Vector3(fCx, fCy, fZBottom + fSz / 2f)));
}
