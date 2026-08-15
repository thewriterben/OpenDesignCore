using System.Numerics;
using OpenDesignCore.Data;
using PicoGK;

namespace OpenDesignCore.Models;

/// <summary>Thrown when a run is requested below the model's resolution floor (ADR-0003).</summary>
public sealed class ResolutionFloorException(string strMessage) : Exception(strMessage);

/// <summary>Thrown when generated geometry fails the validation gate.</summary>
public sealed class GeometryValidationException(string strMessage) : Exception(strMessage);

public sealed record EnclosureShellParams
{
    public required EnvelopeMm Envelope { get; init; }
    /// <summary>Clearance per side between part and cavity wall, mm.</summary>
    public required float ClearanceMm { get; init; }
    /// <summary>Wall and floor thickness, mm.</summary>
    public required float WallMm { get; init; }
}

/// <summary>
/// The thin-thread model: an open-top tray around a part envelope. Floor and
/// four walls of thickness WallMm; cavity is the envelope grown by ClearanceMm
/// per side; walls rise to the part top plus clearance. Origin: tray floor
/// bottom at Z = 0, centered in X/Y. All dimensions mm (ADR-0004).
/// </summary>
public static class EnclosureShellModel
{
    public const string StrModelId = "enclosure-shell/0.1";

    /// <summary>
    /// Largest voxel size at which this model is meaningful: the wall must be
    /// at least two voxels across (GLOSSARY: smallest feature >= 1 voxel, with
    /// a 2x margin so the wall never thins to a single-voxel sheet).
    /// </summary>
    public static float FResolutionFloorMm(float fWallMm) => fWallMm / 2f;

    public static Voxels VoxBuild(Library oLib, EnclosureShellParams oParams, float fVoxelSizeMm)
    {
        if (oParams.ClearanceMm < 0 || oParams.WallMm <= 0)
            throw new ArgumentException("ClearanceMm must be >= 0 and WallMm > 0.");

        if (fVoxelSizeMm > FResolutionFloorMm(oParams.WallMm))
        {
            throw new ResolutionFloorException(
                $"{StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the resolution floor " +
                $"{FResolutionFloorMm(oParams.WallMm)} mm for a {oParams.WallMm} mm wall. " +
                "Running coarser would silently thin the walls; refusing (ADR-0003).");
        }

        float fEx = (float)oParams.Envelope.X;
        float fEy = (float)oParams.Envelope.Y;
        float fEz = (float)oParams.Envelope.Z;
        float fC = oParams.ClearanceMm;
        float fW = oParams.WallMm;

        // Outer solid: floor + walls up to part top + clearance.
        float fOuterX = fEx + 2 * fC + 2 * fW;
        float fOuterY = fEy + 2 * fC + 2 * fW;
        float fOuterZ = fW + fEz + fC;

        // Cavity: envelope + clearance per side, from the floor top upward,
        // overshooting the outer top so the tray is open.
        float fCavityX = fEx + 2 * fC;
        float fCavityY = fEy + 2 * fC;
        float fOvershoot = 2 * fVoxelSizeMm + 1.0f;
        float fCavityZ = (fOuterZ - fW) + fOvershoot;

        Voxels voxOuter = new(Utils.mshCreateCube(
            oLib, new Vector3(fOuterX, fOuterY, fOuterZ), new Vector3(0, 0, fOuterZ / 2)));
        Voxels voxCavity = new(Utils.mshCreateCube(
            oLib, new Vector3(fCavityX, fCavityY, fCavityZ), new Vector3(0, 0, fW + fCavityZ / 2)));

        return voxOuter - voxCavity;
    }

    /// <summary>
    /// Validation gate: fails loudly and specifically before any export.
    /// Voxel-derived meshes are closed by construction, so the checks are
    /// emptiness and dimensional sanity of the result against the requested
    /// outer envelope (within a 2-voxel tolerance band).
    /// </summary>
    public static void Validate(Voxels voxResult, EnclosureShellParams oParams, float fVoxelSizeMm)
    {
        if (voxResult.bIsEmpty())
            throw new GeometryValidationException($"{StrModelId}: result voxel field is empty.");

        float fOuterX = (float)oParams.Envelope.X + 2 * oParams.ClearanceMm + 2 * oParams.WallMm;
        float fOuterY = (float)oParams.Envelope.Y + 2 * oParams.ClearanceMm + 2 * oParams.WallMm;
        float fOuterZ = oParams.WallMm + (float)oParams.Envelope.Z + oParams.ClearanceMm;

        BBox3 oBox = voxResult.oCalculateBoundingBox();
        Vector3 vecSize = oBox.vecSize();
        float fTol = 2 * fVoxelSizeMm;

        if (Math.Abs(vecSize.X - fOuterX) > fTol
            || Math.Abs(vecSize.Y - fOuterY) > fTol
            || Math.Abs(vecSize.Z - fOuterZ) > fTol)
        {
            throw new GeometryValidationException(
                $"{StrModelId}: bounding box {vecSize.X:F2} x {vecSize.Y:F2} x {vecSize.Z:F2} mm " +
                $"deviates from expected {fOuterX:F2} x {fOuterY:F2} x {fOuterZ:F2} mm " +
                $"by more than {fTol:F2} mm (2 voxels).");
        }
    }
}
