using System.Globalization;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record CameraMountRunResult
{
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
    public required ArtifactGeometry Geometry { get; init; }
}

/// <summary>
/// Produces the bench fixture that holds the camera board still.
///
/// The sidecar matters here for a specific reason: the board dimensions in the
/// inputs are MEASUREMENTS of one physical board, not a datasheet lookup, and
/// the datasheet disagrees with them substantially. If this fixture turns out
/// not to fit, the first question is which numbers it was cut for — and that
/// has to be answerable from the artifact rather than from a chat log.
/// </summary>
public static class CameraMountRun
{
    public static CameraMountRunResult Execute(
        CameraMountParams oParams,
        float fVoxelSizeMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        byte[] abStl;
        ArtifactGeometry oGeometry;
        using (Library oLib = new(fVoxelSizeMm))
        {
            Voxels voxMount = CameraMountModel.VoxBuild(oLib, oParams, fVoxelSizeMm);
            CameraMountModel.Validate(voxMount, oParams, fVoxelSizeMm);

            Mesh mshMount = voxMount.mshAsMesh();
            if (mshMount.nTriangleCount() == 0)
                throw new GeometryValidationException("Meshing produced zero triangles.");

            oGeometry = ArtifactGeometry.OMeasure(mshMount, voxMount);

            string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
            try
            {
                mshMount.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
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
            ["model"] = CameraMountModel.StrModelId,
            ["voxel_size_mm"] = StrF4(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["board_width_mm"] = StrF2(oParams.BoardWidthMm),
                ["board_length_mm"] = StrF2(oParams.BoardLengthMm),
                ["board_thickness_mm"] = StrF2(oParams.BoardThicknessMm),
                ["lens_protrusion_mm"] = StrF2(oParams.LensProtrusionMm),
                ["lens_diameter_mm"] = StrF2(oParams.LensDiameterMm),
                ["total_depth_mm"] = StrF2(oParams.TotalDepthMm),
                ["board_source"] =
                    "measured 2026-09-25 with calipers; LILYGO T-CameraPlus-S3 V1.1, "
                    + "node obc-esp32-s3-003",
                ["slot_clearance_mm"] = StrF2(oParams.SlotClearanceMm),
                ["grip_depth_mm"] = StrF2(oParams.GripDepthMm),
                ["grip_height_mm"] = StrF2(oParams.GripHeightMm),
                ["wall_mm"] = StrF2(oParams.WallMm),
                ["base_thickness_mm"] = StrF2(oParams.BaseThicknessMm),
                ["base_front_mm"] = StrF2(oParams.BaseFrontMm),
                ["base_back_mm"] = StrF2(oParams.BaseBackMm),
                ["seat_lift_mm"] = StrF2(oParams.SeatLiftMm),
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
                "The board dimensions are measurements of ONE board, not a specification. "
                    + "LILYGO publish 60 x 32 x 12 mm with four 2 mm holes for this product; "
                    + "this board measures 68.25 x 27.90 x 29.40 with two. If a second board "
                    + "does not fit this fixture, measure it before assuming the fixture is "
                    + "wrong.",
                "The slot is a friction fit and its clearance is a guess until a part is "
                    + "printed and tried. Too tight and the board will not seat; too loose "
                    + "and the fixture does not solve the problem it exists for. Adjust "
                    + "--slot-clearance-mm and reprint rather than forcing the board.",
                "Acceptance is measurable, not subjective: run "
                    + "scripts/vision/bench_floor.py in Oh-Ben-Claw with a deliberate desk "
                    + "knock, with and without the fixture, and compare the `edge` score. "
                    + "The fixture exists to make the detector's `nudge` class rare.",
            },
        };

        byte[] abSidecar = CanonicalJson.Serialize(oSidecar);
        string strProvenanceHash = ArtifactStore.StrStore(strArtifactsDir, abSidecar, ".provenance.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = CameraMountModel.StrModelId,
            VoxelSizeMm = StrF4(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["versions"])),
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            Passed = true,
        });

        return new CameraMountRunResult
        {
            RunId = nRunId,
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            ArtifactPath = ArtifactStore.StrPathFor(strArtifactsDir, strArtifactHash, ".stl"),
            Geometry = oGeometry,
        };
    }

    private static string StrF2(double f) => f.ToString("F2", CultureInfo.InvariantCulture);
    private static string StrF4(double f) => f.ToString("F4", CultureInfo.InvariantCulture);
}
