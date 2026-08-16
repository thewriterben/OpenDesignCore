using System.Globalization;
using OpenDesignCore.Import;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record CradleRunResult
{
    public required long RunId { get; init; }
    public required string ScanSha256 { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
}

/// <summary>
/// Scan-to-fit executor: imported mesh in (units declared, never inferred),
/// validated cradle STL + deterministic provenance out, run ledgered. The raw
/// scan file is itself content-addressed, so the sidecar chains cradle → scan
/// by hash.
/// </summary>
public static class CradleRun
{
    public static CradleRunResult Execute(
        string strStlPath,
        Mesh.EStlUnit eUnits,
        float fPostScale,
        float fVoxelSizeMm,
        float fClearanceMm,
        float fWallMm,
        float fSplitFraction,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        byte[] abStl;
        string strScanHash;
        int nScanTriangles;

        using (Library oLib = new(fVoxelSizeMm))
        {
            ScanImportResult oScan = ScanImport.OImport(
                oLib, strStlPath, eUnits, fPostScale, strArtifactsDir);
            strScanHash = oScan.ScanSha256;
            nScanTriangles = oScan.TriangleCount;

            CradleParams oParams = new()
            {
                ClearanceMm = fClearanceMm,
                WallMm = fWallMm,
                SplitFraction = fSplitFraction,
            };

            Voxels voxCradle = CradleModel.VoxBuild(oLib, oScan.Mesh, oParams, fVoxelSizeMm);
            CradleModel.Validate(voxCradle, fVoxelSizeMm);

            Mesh mshCradle = voxCradle.mshAsMesh();
            if (mshCradle.nTriangleCount() == 0)
                throw new GeometryValidationException("Meshing produced zero triangles.");

            string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
            try
            {
                mshCradle.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
                abStl = File.ReadAllBytes(strTmp);
            }
            finally
            {
                if (File.Exists(strTmp))
                    File.Delete(strTmp);
            }
        }

        string strArtifactHash = ArtifactStore.StrStore(strArtifactsDir, abStl, ".stl");

        Dictionary<string, object?> oSidecar = new()
        {
            ["schema"] = "odc/provenance/0.1",
            ["model"] = CradleModel.StrModelId,
            ["voxel_size_mm"] = StrF2(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["scan_sha256"] = strScanHash,
                ["scan_declared_units"] = eUnits.ToString().ToLowerInvariant(),
                ["scan_post_scale"] = StrF4(fPostScale),
                ["scan_triangle_count"] = nScanTriangles,
                ["clearance_mm"] = StrF2(fClearanceMm),
                ["wall_mm"] = StrF2(fWallMm),
                ["split_fraction"] = StrF2(fSplitFraction),
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = EnclosureRun.StrToolVersion,
                ["picogk"] = EnclosureRun.StrPicoGKVersion,
                ["shapekernel"] = EnclosureRun.StrShapeKernelTag,
            },
            ["commit"] = strCommit,
            ["artifact"] = new Dictionary<string, object?>
            {
                ["media_type"] = "model/stl",
                ["sha256"] = strArtifactHash,
            },
        };

        byte[] abSidecar = CanonicalJson.Serialize(oSidecar);
        string strProvenanceHash = ArtifactStore.StrStore(strArtifactsDir, abSidecar, ".provenance.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = CradleModel.StrModelId,
            VoxelSizeMm = StrF2(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["versions"])),
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            Passed = true,
        });

        return new CradleRunResult
        {
            RunId = nRunId,
            ScanSha256 = strScanHash,
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            ArtifactPath = ArtifactStore.StrPathFor(strArtifactsDir, strArtifactHash, ".stl"),
        };
    }

    private static string StrF2(double fValue) => fValue.ToString("F2", CultureInfo.InvariantCulture);
    private static string StrF4(double fValue) => fValue.ToString("F4", CultureInfo.InvariantCulture);
}
