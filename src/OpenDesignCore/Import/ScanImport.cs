using System.Numerics;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Import;

public sealed class ImportValidationException(string strMessage) : Exception(strMessage);

public sealed record ScanImportResult
{
    public required Mesh Mesh { get; init; }
    /// <summary>SHA-256 of the raw scan file bytes, content-addressed into the artifact store.</summary>
    public required string ScanSha256 { get; init; }
    public required int TriangleCount { get; init; }
    /// <summary>Scan bounding-box size after unit conversion and recentring, mm.</summary>
    public required Vector3 SizeMm { get; init; }
}

/// <summary>
/// The mesh→SDF import boundary (ROADMAP "Next"; ADR-0007 scopes scanning to
/// exactly this — capture pipelines live elsewhere).
///
/// Units are declared by the caller, never inferred: an STL file has no
/// reliable unit truth (PicoGK's AUTO trusts an optional header comment), and
/// a silently mis-scaled scan is precisely the bug class the unit rules exist
/// to prevent. Scale and units are provenance fields.
///
/// v0 limitation, stated plainly: voxelization requires a closed (watertight)
/// mesh. A leaky scan produces a degenerate field, which the emptiness check
/// catches — repair belongs upstream in the scan app for now.
/// </summary>
public static class ScanImport
{
    public static ScanImportResult OImport(
        Library oLib,
        string strStlPath,
        Mesh.EStlUnit eUnits,
        float fPostScale,
        string strArtifactsDir)
    {
        if (eUnits == Mesh.EStlUnit.AUTO)
            throw new ImportValidationException(
                "Units must be declared explicitly (mm/cm/m/in/ft) — AUTO infers from an " +
                "unreliable STL header and silent unit inference is forbidden.");
        if (fPostScale <= 0)
            throw new ImportValidationException("Scale must be positive.");
        if (!File.Exists(strStlPath))
            throw new ImportValidationException($"Scan file not found: {strStlPath}");

        // The raw input is provenance: content-address it before touching it.
        byte[] abScan = File.ReadAllBytes(strStlPath);
        string strScanHash = ArtifactStore.StrStore(strArtifactsDir, abScan, ".stl");

        Mesh mshLoaded = Mesh.mshFromStlFile(strStlPath, eUnits, fPostScale, null, oLib);

        if (mshLoaded.nTriangleCount() == 0)
            throw new ImportValidationException(
                $"{Path.GetFileName(strStlPath)}: no triangles after import — empty or unreadable STL.");

        // Recentre deterministically: XY centered on origin, floor at Z = 0.
        // Matrix overload on purpose — the (vecScale, vecOffset) overload in
        // PicoGK 2.2.0 applies a different scale component per vertex (upstream bug).
        BBox3 oBox = mshLoaded.oBoundingBox();
        Vector3 vecCenter = oBox.vecCenter();
        Mesh mshCentered = mshLoaded.mshCreateTransformed(
            Matrix4x4.CreateTranslation(-vecCenter.X, -vecCenter.Y, -oBox.vecMin.Z));

        Vector3 vecSize = oBox.vecSize();
        if (vecSize.X <= 0 || vecSize.Y <= 0 || vecSize.Z <= 0)
            throw new ImportValidationException(
                $"{Path.GetFileName(strStlPath)}: degenerate bounding box " +
                $"{vecSize.X:F3} x {vecSize.Y:F3} x {vecSize.Z:F3} mm.");

        return new ScanImportResult
        {
            Mesh = mshCentered,
            ScanSha256 = strScanHash,
            TriangleCount = mshLoaded.nTriangleCount(),
            SizeMm = vecSize,
        };
    }
}
