using System.Globalization;
using OpenDesignCore.Data;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record EnclosureRunResult
{
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
}

/// <summary>
/// The thin-thread executor: cited data in, validated STL + provenance out,
/// run recorded in the ledger. The provenance sidecar is deterministic —
/// same inputs, same voxel size, same pinned versions produce byte-identical
/// sidecar and (same machine) byte-identical STL. Timestamps go to the ledger
/// only.
/// </summary>
public static class EnclosureRun
{
    public const string StrToolVersion = "0.1.0";
    public const string StrPicoGKVersion = "2.2.0";               // ADR-0008
    public const string StrShapeKernelTag = "ShapeKernel-v2.1.0"; // ADR-0008

    public static EnclosureRunResult Execute(
        string strDataDir,
        string strPartId,
        float fVoxelSizeMm,
        float fClearanceMm,
        float fWallMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        DataSet oData = DataStore.LoadAll(strDataDir);
        PartEntry oPart = oData.Parts.FirstOrDefault(o => o.Id == strPartId)
            ?? throw new ArgumentException(
                $"Part '{strPartId}' not found in {strDataDir}. Known: " +
                string.Join(", ", oData.Parts.Select(o => o.Id)));

        EnclosureShellParams oParams = new()
        {
            Envelope = oPart.EnvelopeMm,
            ClearanceMm = fClearanceMm,
            WallMm = fWallMm,
        };

        byte[] abStl;
        using (Library oLib = new(fVoxelSizeMm))
        {
            Voxels voxShell = EnclosureShellModel.VoxBuild(oLib, oParams, fVoxelSizeMm);
            EnclosureShellModel.Validate(voxShell, oParams, fVoxelSizeMm);

            Mesh mshShell = voxShell.mshAsMesh();
            if (mshShell.nTriangleCount() == 0)
                throw new GeometryValidationException("Meshing produced zero triangles.");

            string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
            try
            {
                mshShell.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
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
            ["model"] = EnclosureShellModel.StrModelId,
            ["voxel_size_mm"] = StrMm(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["part_id"] = oPart.Id,
                ["part_envelope_mm"] = new Dictionary<string, object?>
                {
                    ["x"] = StrMm(oPart.EnvelopeMm.X),
                    ["y"] = StrMm(oPart.EnvelopeMm.Y),
                    ["z"] = StrMm(oPart.EnvelopeMm.Z),
                },
                ["part_source_citation"] = oPart.Source.Citation,
                ["clearance_mm"] = StrMm(fClearanceMm),
                ["wall_mm"] = StrMm(fWallMm),
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = StrToolVersion,
                ["picogk"] = StrPicoGKVersion,
                ["shapekernel"] = StrShapeKernelTag,
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
            Model = EnclosureShellModel.StrModelId,
            VoxelSizeMm = StrMm(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["versions"])),
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            Passed = true,
        });

        return new EnclosureRunResult
        {
            RunId = nRunId,
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            ArtifactPath = ArtifactStore.StrPathFor(strArtifactsDir, strArtifactHash, ".stl"),
        };
    }

    /// <summary>Deterministic mm formatting for provenance: fixed two decimals, invariant.</summary>
    private static string StrMm(double fValue) => fValue.ToString("F2", CultureInfo.InvariantCulture);
}
