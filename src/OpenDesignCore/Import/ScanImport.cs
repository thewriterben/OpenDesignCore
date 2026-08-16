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

        Mesh mshLoaded = bIsAsciiStl(abScan)
            ? mshFromAsciiStl(oLib, abScan, eUnits, fPostScale)
            : Mesh.mshFromStlFile(strStlPath, eUnits, fPostScale, null, oLib);

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

    /// <summary>
    /// A binary STL is an 80-byte header then a uint32 triangle count; the
    /// file size is exactly 84 + 50*n. ASCII starts with "solid" — but so do
    /// some binary files, so the size arithmetic is what decides.
    /// </summary>
    private static bool bIsAsciiStl(byte[] abData)
    {
        if (abData.Length < 84)
            return true;
        uint nTriangles = BitConverter.ToUInt32(abData, 80);
        return abData.Length != 84L + 50L * nTriangles;
    }

    /// <summary>
    /// PicoGK 2.2.0 throws NotImplementedException for ASCII STL, and KiCad
    /// emits exactly that (`kicad-cli pcb export stl` has no binary option).
    /// This is file parsing, not geometry — the kernel keeps owning geometry.
    /// </summary>
    private static Mesh mshFromAsciiStl(Library oLib, byte[] abData, Mesh.EStlUnit eUnits, float fPostScale)
    {
        float fScale = fUnitScaleMm(eUnits) * fPostScale;
        Mesh msh = new(oLib);
        List<Vector3> aVertices = new(3);
        int nTriangles = 0;

        foreach (string strRawLine in System.Text.Encoding.ASCII.GetString(abData).Split('\n'))
        {
            string strLine = strRawLine.Trim();
            if (!strLine.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] aParts = strLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (aParts.Length < 4
                || !float.TryParse(aParts[1], System.Globalization.CultureInfo.InvariantCulture, out float fX)
                || !float.TryParse(aParts[2], System.Globalization.CultureInfo.InvariantCulture, out float fY)
                || !float.TryParse(aParts[3], System.Globalization.CultureInfo.InvariantCulture, out float fZ))
            {
                throw new ImportValidationException($"Malformed ASCII STL vertex line: '{strLine}'");
            }

            aVertices.Add(new Vector3(fX, fY, fZ) * fScale);
            if (aVertices.Count == 3)
            {
                msh.nAddTriangle(aVertices[0], aVertices[1], aVertices[2]);
                aVertices.Clear();
                nTriangles++;
            }
        }

        if (aVertices.Count != 0)
            throw new ImportValidationException("ASCII STL ended mid-triangle — file is truncated.");
        if (nTriangles == 0)
            throw new ImportValidationException("ASCII STL contained no triangles.");

        return msh;
    }

    private static float fUnitScaleMm(Mesh.EStlUnit eUnits) => eUnits switch
    {
        Mesh.EStlUnit.MM => 1f,
        Mesh.EStlUnit.CM => 10f,
        Mesh.EStlUnit.M => 1000f,
        Mesh.EStlUnit.IN => 25.4f,
        Mesh.EStlUnit.FT => 304.8f,
        _ => throw new ImportValidationException($"Unsupported unit {eUnits}."),
    };
}
