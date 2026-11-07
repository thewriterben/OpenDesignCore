using System.Globalization;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record LensFitGaugeRunResult
{
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
    public required ArtifactGeometry Geometry { get; init; }
}

/// <summary>
/// Produces the bore ladder whose measurement sizes a real bore.
///
/// <para>
/// The sidecar is load-bearing here in a way it is not for most parts. Days
/// after printing, the useful sentence is "bore three fits" — and "bore three"
/// only means a diameter if the nominal ladder is recorded somewhere that
/// travels with the object. That is the same reason
/// <c>calibration-block/0.2</c> is a run rather than a script.
/// </para>
/// </summary>
public static class LensFitGaugeRun
{
    public static LensFitGaugeRunResult Execute(
        LensFitGaugeParams oParams,
        float fVoxelSizeMm,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        byte[] abStl;
        ArtifactGeometry oGeometry;
        using (OdcLibrary oLib = new(fVoxelSizeMm))
        {
            Voxels voxGauge = LensFitGaugeModel.VoxBuild(oLib, oParams, fVoxelSizeMm);
            LensFitGaugeModel.Validate(voxGauge, oParams, fVoxelSizeMm);

            Mesh mshGauge = voxGauge.mshAsMesh();
            if (mshGauge.nTriangleCount() == 0)
                throw new GeometryValidationException("Meshing produced zero triangles.");

            oGeometry = ArtifactGeometry.OMeasure(mshGauge, voxGauge);

            string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
            try
            {
                mshGauge.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
                abStl = File.ReadAllBytes(strTmp);
            }
            finally
            {
                if (File.Exists(strTmp))
                    File.Delete(strTmp);
            }
        }

        string strArtifactHash = ArtifactStore.StrStore(strArtifactsDir, abStl, ".stl");

        // The ladder, spelled out. "Bore three fits" is only a measurement if
        // this list is attached to the object that was measured.
        List<object?> aBores = [];
        for (int n = 0; n < oParams.BoreCount; n++)
        {
            aBores.Add(new Dictionary<string, object?>
            {
                ["tally_dots"] = n + 1,
                ["nominal_diameter_mm"] = StrF2(LensFitGaugeModel.FBoreDiameterMm(oParams, n)),
                ["centre_x_mm"] = StrF2(LensFitGaugeModel.FBoreCentreXMm(oParams, n)),
            });
        }

        Dictionary<string, object?> oSidecar = new()
        {
            ["schema"] = "odc/provenance/0.2",
            ["model"] = LensFitGaugeModel.StrModelId,
            ["voxel_size_mm"] = StrF4(fVoxelSizeMm),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["bore_start_mm"] = StrF2(oParams.BoreStartMm),
                ["bore_step_mm"] = StrF2(oParams.BoreStepMm),
                ["bore_count"] = oParams.BoreCount,
                ["bore_spacing_mm"] = StrF2(oParams.BoreSpacingMm),
                ["plate_thickness_mm"] = StrF2(oParams.PlateThicknessMm),
                ["edge_margin_mm"] = StrF2(oParams.EdgeMarginMm),
                ["tally_dot_mm"] = StrF2(oParams.TallyDotMm),
                ["target"] =
                    "LILYGO T-CameraPlus-S3 V1.1 lens barrel, measured 14.95 mm across "
                    + "with calipers 2026-09-25",
            },
            ["bores"] = aBores,
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
                "Bores are identified by TALLY DOTS, not by position: one dot is the "
                    + "smallest bore. Count the dots rather than trusting which way round the "
                    + "plate is lying.",
                "This gauge measures DIAMETER and nothing else. The plate is deliberately "
                    + "thin so a barrel cannot bottom out in it; how much barrel is available "
                    + "to pass through a thicker plate is a property of the board and wants "
                    + "calipers, not this part.",
                "The result is a property of THIS PRINTER and this material at these "
                    + "settings. A different filament or a different nozzle temperature moves "
                    + "the answer, and the answer is what a real bore gets sized from.",
                "The nominal ladder starts at the measured barrel diameter and only goes "
                    + "up, because printed holes come out undersize far more often than over. "
                    + "If the SMALLEST bore fits loosely, the ladder was aimed wrong and "
                    + "wants re-running downward from a lower start.",
            },
        };

        byte[] abSidecar = CanonicalJson.Serialize(oSidecar);
        string strProvenanceHash = ArtifactStore.StrStore(strArtifactsDir, abSidecar, ".provenance.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = LensFitGaugeModel.StrModelId,
            VoxelSizeMm = StrF4(fVoxelSizeMm),
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oSidecar["versions"])),
            ArtifactSha256 = strArtifactHash,
            ProvenanceSha256 = strProvenanceHash,
            Passed = true,
        });

        return new LensFitGaugeRunResult
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
