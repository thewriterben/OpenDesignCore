using System.Numerics;
using PicoGK;

namespace OpenDesignCore.Models;

public sealed record CradleParams
{
    /// <summary>Gap between the scanned surface and the cradle, per side, mm.</summary>
    public required float ClearanceMm { get; init; }
    /// <summary>Minimum material around the cavity and under its lowest point, mm.</summary>
    public required float WallMm { get; init; }
    /// <summary>Fraction of the scan's height the cradle rises to (0..1). Above it the object is exposed for grip and removal.</summary>
    public required float SplitFraction { get; init; }
}

/// <summary>
/// Scan-to-fit v0: a foam-insert-style cradle. A rectangular block — flat
/// bottom, printable, stable — with the clearance-offset scan volume carved
/// out of it. The block rises to SplitFraction of the scan's height, so the
/// object drops in from above and stands proud for removal.
///
/// Built only from operations verified against PicoGK 2.2.0 source: voxelize,
/// offset (mm), box, boolean subtract. Origin matches ScanImport: scan
/// centered in XY, its lowest point at Z = 0.
/// </summary>
public static class CradleModel
{
    public const string StrModelId = "scan-cradle/0.1";

    /// <summary>
    /// Floor: the wall must span >= 2 voxels, and a positive clearance below
    /// one voxel cannot be represented at all — either refuses loudly.
    /// </summary>
    public static float FResolutionFloorMm(float fWallMm, float fClearanceMm)
        => fClearanceMm > 0 ? Math.Min(fWallMm / 2f, fClearanceMm) : fWallMm / 2f;

    public static Voxels VoxBuild(Library oLib, Mesh mshScan, CradleParams oParams, float fVoxelSizeMm)
    {
        if (oParams.ClearanceMm < 0 || oParams.WallMm <= 0)
            throw new ArgumentException("ClearanceMm must be >= 0 and WallMm > 0.");
        if (oParams.SplitFraction is <= 0 or >= 1)
            throw new ArgumentException("SplitFraction must be between 0 and 1 exclusive.");

        float fFloor = FResolutionFloorMm(oParams.WallMm, oParams.ClearanceMm);
        if (fVoxelSizeMm > fFloor)
        {
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor " +
                $"{fFloor} mm (wall {oParams.WallMm} mm, clearance {oParams.ClearanceMm} mm). " +
                "Running coarser would erase the fit; refusing (ADR-0003).");
        }

        Voxels voxScan = new(mshScan);
        if (voxScan.bIsEmpty())
        {
            throw new GeometryValidationException(
                $"{StrModelId}: scan voxelized to an empty field — the mesh is " +
                "likely not watertight. Repair the scan and re-import (v0 requires closed meshes).");
        }

        Voxels voxCavity = oParams.ClearanceMm > 0
            ? voxScan.voxOffset(oParams.ClearanceMm)
            : voxScan;

        BBox3 oCavityBox = voxCavity.oCalculateBoundingBox();
        Vector3 vecCavitySize = oCavityBox.vecSize();
        Vector3 vecCavityCenter = oCavityBox.vecCenter();

        // Block: cavity XY footprint + wall on each side; from wall-below the
        // cavity's lowest point up to SplitFraction of the scan height.
        float fBlockX = vecCavitySize.X + 2 * oParams.WallMm;
        float fBlockY = vecCavitySize.Y + 2 * oParams.WallMm;
        float fBlockBottom = oCavityBox.vecMin.Z - oParams.WallMm;
        float fScanTop = oCavityBox.vecMax.Z - oParams.ClearanceMm; // scan height ~ cavity top minus clearance
        float fBlockTop = fScanTop * oParams.SplitFraction;
        float fBlockZ = fBlockTop - fBlockBottom;

        if (fBlockZ <= oParams.WallMm)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: split fraction {oParams.SplitFraction} leaves a " +
                $"{fBlockZ:F2} mm block — nothing to hold the part. Raise --split.");
        }

        Voxels voxBlock = new(Utils.mshCreateCube(
            oLib,
            new Vector3(fBlockX, fBlockY, fBlockZ),
            new Vector3(vecCavityCenter.X, vecCavityCenter.Y, fBlockBottom + fBlockZ / 2)));

        return voxBlock - voxCavity;
    }

    public static void Validate(Voxels voxResult, float fVoxelSizeMm)
    {
        if (voxResult.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId}: result voxel field is empty.");

        BBox3 oBox = voxResult.oCalculateBoundingBox();
        Vector3 vecSize = oBox.vecSize();
        if (vecSize.X < 4 * fVoxelSizeMm || vecSize.Y < 4 * fVoxelSizeMm || vecSize.Z < 2 * fVoxelSizeMm)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: result implausibly small " +
                $"({vecSize.X:F2} x {vecSize.Y:F2} x {vecSize.Z:F2} mm).");
        }
    }
}
