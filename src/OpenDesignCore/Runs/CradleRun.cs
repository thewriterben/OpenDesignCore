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
/// Scan-to-fit executor: imported mesh in (units and origin declared, never
/// inferred), validated cradle STL + deterministic provenance out, run ledgered.
/// The raw scan file is itself content-addressed, so the sidecar chains
/// cradle → scan by hash.
///
/// Sidecar schema `odc/provenance/0.3` — the 0.2 → 0.3 bump adds `scan_origin`
/// and `scan_scale_reference` (ADR-0019). `EnclosureRun` is already on 0.3 for
/// its own reasons (ADR-0016) and is untouched here; the version tracks each
/// sidecar's own content, and the `inputs` block has always been model-specific.
/// </summary>
public static class CradleRun
{
    public static CradleRunResult Execute(
        string strStlPath,
        Mesh.EStlUnit eUnits,
        float fPostScale,
        EScanOrigin eOrigin,
        ScaleReference? oScaleRef,
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
        float fEffectiveScale;
        MeshTopologyReport oTopology;
        ArtifactGeometry oGeometry;

        using (Library oLib = new(fVoxelSizeMm))
        {
            ScanImportResult oScan = ScanImport.OImport(
                oLib, strStlPath, eUnits, fPostScale, eOrigin, oScaleRef, strArtifactsDir);
            strScanHash = oScan.ScanSha256;
            nScanTriangles = oScan.TriangleCount;
            fEffectiveScale = oScan.EffectiveScale;
            oTopology = oScan.Topology;

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

            oGeometry = ArtifactGeometry.OMeasure(mshCradle, voxCradle);

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
            ["schema"] = "odc/provenance/0.3",
            ["model"] = CradleModel.StrModelId,
            ["voxel_size_mm"] = StrF2(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["scan_sha256"] = strScanHash,
                ["scan_declared_units"] = eUnits.ToString().ToLowerInvariant(),
                // What was applied, not what was asked: for photogrammetry this
                // is derived from the reference below (ADR-0020).
                ["scan_post_scale"] = StrF4(fEffectiveScale),
                ["scan_scale_source"] = eOrigin == EScanOrigin.Photogrammetry
                    ? "derived from scale_reference"
                    : "declared by the caller",
                ["scan_origin"] = ScanProvenance.StrOrigin(eOrigin),
                // Null on purpose when there is none: a reader must be able to
                // tell "no reference was needed" from "one was recorded", and an
                // absent key reads as neither (ADR-0019).
                ["scan_scale_reference"] = oScaleRef is null
                    ? null
                    : new Dictionary<string, object?>
                    {
                        ["length_mm"] = StrF4(oScaleRef.LengthMm),
                        ["span_file_units"] = oScaleRef.SpanFileUnits is { } fSpan
                            ? StrF4(fSpan)
                            : null,
                        ["description"] = oScaleRef.Description,
                    },
                ["scan_triangle_count"] = nScanTriangles,
                // Measured, not enforced (ADR-0021). A hole makes the field
                // describe whatever the surface encloses; touching solids in an
                // assembly are non-manifold and voxelise correctly. The counts
                // let a reader tell those apart; the importer does not try to.
                ["scan_topology"] = new Dictionary<string, object?>
                {
                    ["boundary_edges"] = oTopology.BoundaryEdges,
                    ["nonmanifold_edges"] = oTopology.NonManifoldEdges,
                    ["edges"] = oTopology.Edges,
                    ["welded_vertices"] = oTopology.WeldedVertices,
                    ["closed_manifold"] = oTopology.IsClosedManifold,
                    ["euler_characteristic"] = oTopology.EulerCharacteristic,
                    // Vertices weld by exact equality, so a writer that emits
                    // shared vertices at differing precision can inflate
                    // boundary_edges. Measured on one KiCad export: 4 here
                    // against 0 from a tolerant weld.
                    ["weld"] = "exact vector equality",
                },
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
            ["artifact"] = oGeometry.OArtifactBlock(strArtifactHash),
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
