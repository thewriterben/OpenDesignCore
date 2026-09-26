using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using PicoGK;

namespace OpenDesignCore.Tests.Reference.Historical;

/// <summary>
/// camera-mount/0.2 as it was cut (385feef, bore from 28796e7), kept only so
/// the assembly check can be shown to reject it. It held the board by its lens
/// with the USB-C edge down, and the port could not be plugged in.
///
/// <para>
/// The geometry is <c>CameraMountModel.VoxBuild</c> at 28796e7 with its
/// <c>ODefault()</c> inlined, frozen here so later versions of the model cannot
/// change what "v0.2" means. Origin: base bottom at Z = 0, centred in X on the
/// bore, Y = 0 at the plate's front face, lens toward -Y.
/// </para>
/// </summary>
internal static class CameraMountV02
{
    // ODefault() at 28796e7. Barrel, lip, axis and tip figures were caliper
    // measurements; the bore is 14.95 + the measured 0.25 printer offset.
    public const float BoreDiameterMm = 15.20f, BoreLandMm = 3.00f, PlateThicknessMm = 6.00f;
    public const float LipPocketMm = 19.00f, BaseThicknessMm = 4.00f, FloorDropMm = 1.00f;
    public const float BaseFrontMm = 10.00f, BaseBackMm = 40.00f, WidthMm = 34.00f, PlateHeadroomMm = 4.50f;
    public const float AxisToUsbEdgeMm = 35.00f, BarrelFreeLengthMm = 5.10f, LensTipFromPcbMm = 24.00f;

    public const float AxisZMm = BaseThicknessMm + FloorDropMm + AxisToUsbEdgeMm;

    public static Voxels VoxBuild(Library oLib, float fVoxelSizeMm)
    {
        float fOuterX = WidthMm;
        float fOuterY = BaseFrontMm + PlateThicknessMm + BaseBackMm;
        float fOuterZ = AxisZMm + LipPocketMm / 2f + PlateHeadroomMm;
        float fOver = 2 * fVoxelSizeMm + 1.0f;
        float fYFront = -BaseFrontMm;
        float fYPlateBack = PlateThicknessMm;
        float fYBack = PlateThicknessMm + BaseBackMm;
        float fBaseCy = (fYFront + fYBack) / 2f;

        Voxels vox = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fOuterZ), new Vector3(0, fBaseCy, fOuterZ / 2)));

        float fBehindY = (fYBack - fYPlateBack) + fOver;
        vox -= VoxCut(oLib, fOuterX + 2 * fOver, fBehindY, fOuterZ - BaseThicknessMm + fOver,
            0, fYPlateBack + fBehindY / 2f, BaseThicknessMm);

        float fFrontY = BaseFrontMm + fOver;
        vox -= VoxCut(oLib, fOuterX + 2 * fOver, fFrontY, fOuterZ - BaseThicknessMm + fOver,
            0, -fFrontY / 2f, BaseThicknessMm);

        float fPocketY = (PlateThicknessMm - BoreLandMm) + fOver;
        vox -= VoxCut(oLib, LipPocketMm, fPocketY, LipPocketMm,
            0, BoreLandMm + fPocketY / 2f, AxisZMm - LipPocketMm / 2f);

        vox -= Voxels.voxLatticeBeam(
            oLib,
            new Vector3(0, -fOver, AxisZMm), BoreDiameterMm / 2f,
            new Vector3(0, PlateThicknessMm + fOver, AxisZMm), BoreDiameterMm / 2f);
        return vox;
    }

    /// <summary>
    /// v0.2's placement: lens axis on the bore, lip on the pocket floor, the
    /// USB edge DOWN. The lens face and the axis's offset across the board are
    /// inputs because the reference case sweeps them.
    /// </summary>
    public static PartPlacement OPlacement(PartFeatures oF, bool bLensOnLowFace, double fLensOffsetXMm)
    {
        (double[] aLo, double[] aHi) = oF.Items.Single(i => i.Kind == "board").OBounds();
        float fFaceY = BoreLandMm + (LensTipFromPcbMm - BarrelFreeLengthMm);
        return PartPlacement.OLanding(
            bLensOnLowFace ? Axis.NegX : Axis.PosX,
            Axis.PosZ,
            bLensOnLowFace ? Axis.PosY : Axis.NegY,
            [(aLo[0] + aHi[0]) / 2 + fLensOffsetXMm, aLo[1] + AxisToUsbEdgeMm, bLensOnLowFace ? aLo[2] : aHi[2]],
            new Vector3(0, fFaceY, AxisZMm),
            "v0.2: lens axis on the bore axis, lip on the pocket floor, USB edge down");
    }

    private static Voxels VoxCut(Library oLib, float fSx, float fSy, float fSz, float fCx, float fCy, float fZBottom)
        => new(Utils.mshCreateCube(oLib, new Vector3(fSx, fSy, fSz), new Vector3(fCx, fCy, fZBottom + fSz / 2f)));
}
