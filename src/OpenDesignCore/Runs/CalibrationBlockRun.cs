using System.Globalization;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record CalibrationBlockRunResult
{
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
    public required ArtifactGeometry Geometry { get; init; }
}

/// <summary>
/// Produces the block whose measurement closes the loop.
///
/// The sidecar is the reason this is a run rather than a script. When the
/// block is measured days later, "what were the nominal dimensions" must be
/// answerable from the artifact rather than from memory or a chat log — and
/// since schema 0.2 the record carries the artifact's own bounding box, so
/// the nominal figures a comparison needs are already in it.
///
/// The declared instrument accuracy is an *input*, recorded in provenance,
/// because it sets the resolution floor: exporting a block coarser than the
/// caliper can resolve would mean measuring the mesher.
/// </summary>
public static class CalibrationBlockRun
{
    public static CalibrationBlockRunResult Execute(
        CalibrationBlockParams oParams,
        float fVoxelSizeMm,
        float fInstrumentAccuracyMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        if (fInstrumentAccuracyMm <= 0)
        {
            throw new ArgumentException(
                "--instrument-accuracy-mm is required and takes no default. It is recorded "
                + "in provenance and carried into the comparison, where it decides whether a "
                + "deviation is real or instrument error. It does NOT set the voxel size — "
                + "see CalibrationBlockModel.FResolutionFloorMm for why that was wrong.");
        }

        float fFloor = CalibrationBlockModel.FResolutionFloorMm(oParams);
        if (fVoxelSizeMm > fFloor)
        {
            throw new ResolutionFloorException(
                $"{CalibrationBlockModel.StrModelId}: voxel size {fVoxelSizeMm} mm exceeds the "
                + $"resolution floor {fFloor:F4} mm (a fiftieth of the smallest edge). Coarser "
                + "than that and the exported block stops being recognisably the block that "
                + "was asked for; refusing (ADR-0003).");
        }

        byte[] abStl;
        ArtifactGeometry oGeometry;
        using (Library oLib = new(fVoxelSizeMm))
        {
            Voxels voxBlock = CalibrationBlockModel.VoxBuild(oLib, oParams, fVoxelSizeMm);
            CalibrationBlockModel.Validate(voxBlock, oParams, fVoxelSizeMm);

            Mesh mshBlock = voxBlock.mshAsMesh();
            if (mshBlock.nTriangleCount() == 0)
                throw new GeometryValidationException("Meshing produced zero triangles.");

            oGeometry = ArtifactGeometry.OMeasure(mshBlock, voxBlock);

            string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
            try
            {
                mshBlock.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
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
            ["schema"] = "odc/provenance/0.2",
            ["model"] = CalibrationBlockModel.StrModelId,
            ["voxel_size_mm"] = StrF4(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["nominal_x_mm"] = StrF3(oParams.XMm),
                ["nominal_y_mm"] = StrF3(oParams.YMm),
                ["nominal_z_mm"] = StrF3(oParams.ZMm),
                ["nominal_step_z_mm"] = StrF3(oParams.StepZMm),
                ["nominal_z_span_mm"] = StrF3(oParams.ZSpanMm),
                ["nominal_tall_depth_y_mm"] = StrF3(oParams.TallDepthYMm),
                ["instrument_accuracy_mm"] = StrF4(fInstrumentAccuracyMm),
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = EnclosureRun.StrToolVersion,
                ["picogk"] = EnclosureRun.StrPicoGKVersion,
                ["shapekernel"] = EnclosureRun.StrShapeKernelTag,
            },
            ["commit"] = strCommit,
            ["artifact"] = oGeometry.OArtifactBlock(strArtifactHash),
            ["caveats"] = new List<object?>
            {
                "Measure X and Y across flat faces on a cooled part, a few millimetres up "
                    + "from the bed so the reading is not taken over elephant's foot.",
                "Measure Z TWICE — bed to shelf, and bed to the tall face. Both contain the "
                    + "same first-layer squish, so their difference contains none. There is "
                    + "no way to measure a bed-printed height 'away from the first layer': "
                    + "the height begins there. Two readings is how the constant is removed.",
                "X and Y are deliberately unequal so a transposed measurement is visible. "
                    + "If two readings are close to each other and far from nominal, check "
                    + "which faces you measured before believing the numbers.",
            },
        };

        byte[] abSidecar = CanonicalJson.Serialize(oSidecar);
        string strProvenanceHash = ArtifactStore.StrStore(strArtifactsDir, abSidecar, ".provenance.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = CalibrationBlockModel.StrModelId,
            VoxelSizeMm = StrF4(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["versions"])),
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            Passed = true,
        });

        return new CalibrationBlockRunResult
        {
            RunId = nRunId,
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            ArtifactPath = ArtifactStore.StrPathFor(strArtifactsDir, strArtifactHash, ".stl"),
            Geometry = oGeometry,
        };
    }

    private static string StrF3(double f) => f.ToString("F3", CultureInfo.InvariantCulture);
    private static string StrF4(double f) => f.ToString("F4", CultureInfo.InvariantCulture);
}
