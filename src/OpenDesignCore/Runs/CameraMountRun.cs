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
                ["barrel_diameter_mm"] = StrF2(oParams.BarrelDiameterMm),
                ["barrel_free_length_mm"] = StrF2(oParams.BarrelFreeLengthMm),
                ["lip_diameter_mm"] = StrF2(oParams.LipDiameterMm),
                ["axis_to_board_bottom_mm"] = StrF2(oParams.AxisToBoardBottomMm),
                ["board_source"] =
                    "measured 2026-09-25 with calipers; LILYGO T-CameraPlus-S3 V1.1, "
                    + "node obc-esp32-s3-003",
                ["bore_diameter_mm"] = StrF2(oParams.BoreDiameterMm),
                ["bore_diameter_source"] =
                    "measured 2026-09-25 from a printed lens-fit-gauge/0.1 (ledger run 86, "
                    + "artifact 67a96e7c): nominal 15.20 near-snug on the 14.95 barrel, "
                    + "nominal 15.45 loose. Bracketed to about half the ladder's 0.25 mm "
                    + "step, so 0.25 +/- 0.13 mm undersize. This is the nominal to cut, not "
                    + "the barrel diameter, and it is a property of one printer, one "
                    + "filament and one set of settings",
                ["bore_land_mm"] = StrF2(oParams.BoreLandMm),
                ["lip_pocket_mm"] = StrF2(oParams.LipPocketMm),
                ["plate_thickness_mm"] = StrF2(oParams.PlateThicknessMm),
                ["base_thickness_mm"] = StrF2(oParams.BaseThicknessMm),
                ["floor_drop_mm"] = StrF2(oParams.FloorDropMm),
                ["base_front_mm"] = StrF2(oParams.BaseFrontMm),
                ["base_back_mm"] = StrF2(oParams.BaseBackMm),
                ["width_mm"] = StrF2(oParams.WidthMm),
                ["plate_headroom_mm"] = StrF2(oParams.PlateHeadroomMm),
                ["axis_height_mm"] = StrF2(CameraMountModel.FAxisHeightMm(oParams)),
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
                "bore_diameter_mm is now MEASURED rather than assumed, but only to about "
                    + "half the gauge's 0.25 mm ladder step. If the bore comes out tight or "
                    + "loose, that is within the figure's stated uncertainty and wants a "
                    + "finer ladder, not a guess.",
                "v0.1 of this model gripped the board's long edges and did not fit: the "
                    + "bottom corners carry a micro-USB and two JST connectors exactly where "
                    + "the slots bit. The outline was right and the SURFACE was never "
                    + "captured. This version touches only the lens barrel and the board's "
                    + "bottom edge, where the castellations are plated flush.",
                "Retention is the bore's own friction, so a loose bore is not a cosmetic "
                    + "problem - it is the fixture failing to hold anything. If the board "
                    + "slides out under its own weight, the bore is too large; go down a rung.",
                "Roll is BOUNDED, not eliminated. The board hangs on a round bore and its "
                    + "bottom edge rests floor_drop_mm above the base, so it can rotate a "
                    + "degree or two about the lens axis before a corner touches down. That "
                    + "gap exists so the bottom-edge connectors bear on nothing. If the pose "
                    + "needs to repeat more tightly than that, the fix is a measured rest "
                    + "height, not a smaller gap chosen by eye.",
                "Acceptance is measurable: run scripts/vision/bench_floor.py in Oh-Ben-Claw "
                    + "with a deliberate desk knock, with and without the fixture, and "
                    + "compare the `edge` peak. This exists to make the detector's `nudge` "
                    + "class rare; that is the test.",
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
