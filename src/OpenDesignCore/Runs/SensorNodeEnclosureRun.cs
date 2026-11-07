using System.Globalization;
using System.Numerics;
using OpenDesignCore.Data;
using OpenDesignCore.Fit;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using PicoGK;

namespace OpenDesignCore.Runs;

public sealed record SensorNodeEnclosurePartResult
{
    public required EnclosurePart Part { get; init; }
    public required long RunId { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required string ProvenanceSha256 { get; init; }
    public required string ArtifactPath { get; init; }
    public required ArtifactGeometry Geometry { get; init; }
}

public sealed record SensorNodeEnclosureRunResult
{
    public required SensorNodeEnclosurePartResult Base { get; init; }
    public required SensorNodeEnclosurePartResult Lid { get; init; }
    public required AssemblyCheckResult Fit { get; init; }
}

/// <summary>
/// Produces the two halves of the sensor-node enclosure, each as its own STL,
/// sidecar and ledger row, in its print pose. The assembly check runs once,
/// on base and lid together in the assembled pose, before anything is
/// written; both sidecars carry its result and each names its sibling's hash,
/// so neither half is ever mistaken for a pair with a different other half.
/// </summary>
public static class SensorNodeEnclosureRun
{
    public static SensorNodeEnclosureRunResult Execute(
        SensorNodeEnclosureParams oParams,
        float fVoxelSizeMm,
        PartFeatures oFeatures,
        float fToleranceMm,
        IReadOnlyCollection<string> aAcceptUnverifiedAccess,
        string strArtifactsDir,
        string strLedgerPath,
        string strCommit)
    {
        SensorNodeLayout oLayout = SensorNodeLayout.OFrom(oFeatures);
        AssemblyCheckResult oFit;
        Dictionary<EnclosurePart, (byte[] Stl, ArtifactGeometry Geo)> oParts = [];
        using (Library oLib = new(fVoxelSizeMm))
        {
            Voxels voxBaseA = SensorNodeEnclosureModel.VoxBuild(oLib, oParams, oLayout, EnclosurePart.Base, false, fVoxelSizeMm);
            Voxels voxLidA = SensorNodeEnclosureModel.VoxBuild(oLib, oParams, oLayout, EnclosurePart.Lid, false, fVoxelSizeMm);
            SensorNodeEnclosureModel.Validate(voxBaseA, oParams, oLayout, EnclosurePart.Base, fVoxelSizeMm);
            SensorNodeEnclosureModel.Validate(voxLidA, oParams, oLayout, EnclosurePart.Lid, fVoxelSizeMm);

            // Before anything is written: does the board go in, with both halves on?
            oFit = AssemblyCheck.Check(
                oLib, voxBaseA + voxLidA, oFeatures, OPlacement(),
                fToleranceMm, fVoxelSizeMm, aAcceptUnverifiedAccess);
            if (!oFit.BPassed)
                throw new AssemblyCheckFailedException(oFit);

            foreach (EnclosurePart e in new[] { EnclosurePart.Base, EnclosurePart.Lid })
            {
                Voxels vox = SensorNodeEnclosureModel.VoxBuild(oLib, oParams, oLayout, e, true, fVoxelSizeMm);
                SensorNodeEnclosureModel.Validate(vox, oParams, oLayout, e, fVoxelSizeMm);
                oParts[e] = StlOf(vox);
            }
        }

        Dictionary<EnclosurePart, string> oHash = oParts.ToDictionary(
            kv => kv.Key, kv => ArtifactStore.StrStore(strArtifactsDir, kv.Value.Stl, ".stl"));

        using Ledger oLedger = new(strLedgerPath);
        SensorNodeEnclosurePartResult Record(EnclosurePart e)
        {
            EnclosurePart eOther = e == EnclosurePart.Base ? EnclosurePart.Lid : EnclosurePart.Base;
            Dictionary<string, object?> oSidecar = OSidecar(
                oParams, oLayout, fVoxelSizeMm, fToleranceMm, oFit, e,
                oParts[e].Geo.OArtifactBlock(oHash[e]), oHash[eOther], strCommit);
            byte[] abSidecar = CanonicalJson.Serialize(oSidecar);
            string strProv = ArtifactStore.StrStore(strArtifactsDir, abSidecar, ".provenance.json");
            long nRun = oLedger.NAppend(new RunRecord
            {
                CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Model = SensorNodeEnclosureModel.StrModelId,
                VoxelSizeMm = StrF4(fVoxelSizeMm),
                InputsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oSidecar["inputs"])),
                VersionsJson = System.Text.Encoding.ASCII.GetString(CanonicalJson.Serialize(oSidecar["versions"])),
                ArtifactSha256 = oHash[e],
                ProvenanceSha256 = strProv,
                Passed = true,
            });
            return new SensorNodeEnclosurePartResult
            {
                Part = e,
                RunId = nRun,
                ArtifactSha256 = oHash[e],
                ProvenanceSha256 = strProv,
                ArtifactPath = ArtifactStore.StrPathFor(strArtifactsDir, oHash[e], ".stl"),
                Geometry = oParts[e].Geo,
            };
        }

        return new SensorNodeEnclosureRunResult
        {
            Base = Record(EnclosurePart.Base),
            Lid = Record(EnclosurePart.Lid),
            Fit = oFit,
        };
    }

    /// <summary>The enclosure is built in the features frame, so the board lands where it is described.</summary>
    public static PartPlacement OPlacement() => PartPlacement.OLanding(
        Axis.PosX, Axis.PosY, Axis.PosZ, [0, 0, 0], Vector3.Zero,
        "identity: the enclosure is built in the board's features frame (board underside at z = 0), "
        + "assembled pose, base and lid together");

    private static (byte[], ArtifactGeometry) StlOf(Voxels vox)
    {
        Mesh msh = vox.mshAsMesh();
        if (msh.nTriangleCount() == 0)
            throw new GeometryValidationException("Meshing produced zero triangles.");
        ArtifactGeometry oGeo = ArtifactGeometry.OMeasure(msh, vox);
        string strTmp = Path.Combine(Path.GetTempPath(), $"odc-{Guid.NewGuid():N}.stl");
        try
        {
            msh.SaveToStlFile(strTmp, Mesh.EStlUnit.MM);
            return (File.ReadAllBytes(strTmp), oGeo);
        }
        finally
        {
            if (File.Exists(strTmp))
                File.Delete(strTmp);
        }
    }

    private static Dictionary<string, object?> OSidecar(
        SensorNodeEnclosureParams p, SensorNodeLayout l, float fVoxel, float fTol,
        AssemblyCheckResult oFit, EnclosurePart e, Dictionary<string, object?> oArtifact,
        string strSiblingHash, string strCommit)
    {
        const string strDesign = "design choice for this enclosure, not a measured fit";
        Dictionary<string, object?> V(float f, string strSource) =>
            new() { ["mm"] = StrF2(f), ["source"] = strSource };

        return new Dictionary<string, object?>
        {
            ["schema"] = "odc/provenance/0.2",
            ["model"] = SensorNodeEnclosureModel.StrModelId,
            ["voxel_size_mm"] = StrF4(fVoxel),
            ["inputs"] = new Dictionary<string, object?>
            {
                ["part"] = e == EnclosurePart.Base ? "base" : "lid",
                ["pose"] = e == EnclosurePart.Base
                    ? "print: floor on the bed, z shifted so the underside is z = 0"
                    : "print: lid top on the bed, rotated 180 degrees about x: (x, y, z) -> (x, -y, lid_top - z)",
                ["board"] = SensorNodeEnclosureModel.StrPartId,
                ["holes_mm"] = p.HolesMm.Select(h => (object?)$"{StrF2(h.X)},{StrF2(h.Y)}").ToList(),
                ["holes_source"] =
                    "OpenCircuitCore boards/sensor-node/board.manifest.json (sha256 "
                    + "1c708612b512a67e4d74bb576a9bfe28d16d0230ce2acb68db00ed83354e9751), KiCad frame with y negated",
                ["edge_clearance"] = V(p.EdgeClearanceMm, strDesign + "; TODO(source): a measured XY fit on the K2 Plus"),
                ["wall"] = V(p.WallMm, strDesign + ": about five 0.4 mm line widths"),
                ["thin_wall_antenna"] = V(p.ThinWallMm, strDesign
                    + ": three 0.4 mm line widths, the least plastic over the antenna that still prints solid. "
                    + "TODO(source): Espressif's ESP32-S3-WROOM-1 guidance on housing material and distance over the antenna"),
                ["floor"] = V(p.FloorMm, strDesign + ": holds the 3.2 mm nut pocket (nut 2.4 + 0.8 for the screw tip); the boss above is its roof"),
                ["standoff"] = V(p.StandoffMm, strDesign + ": clears the header pin tails under the board"),
                ["ceiling_above_board"] = V(p.CeilingAboveBoardMm, strDesign + ": clears the module can"),
                ["lid_top"] = V(p.LidTopMm, strDesign),
                ["seam_gap"] = V(p.SeamGapMm, strDesign + ": the screws clamp the board between boss and post, never the walls"),
                ["boss_diameter"] = V(p.BossDiameterMm, strDesign),
                ["clearance_hole"] = V(p.ClearanceHoleMm, "ISO 273 medium series, M3"),
                ["nut_across_flats"] = V(p.NutAcrossFlatsMm, "ISO 4032 M3, s"),
                ["nut_thickness"] = V(p.NutThicknessMm, "ISO 4032 M3, m max"),
                ["nut_pocket_clearance"] = V(p.NutPocketClearanceMm, strDesign + ", total across flats"),
                ["screw_head_diameter"] = V(p.ScrewHeadDiameterMm, "ISO 4762 M3, dk max"),
                ["screw_length"] = V(p.ScrewLengthMm, "ISO 4762 M3x16, a stock length; the stack is cut for it"),
                ["header_window_margin"] = V(p.HeaderWindowMarginMm, strDesign + ", per side around each header's outline"),
                ["vent_width"] = V(p.VentWidthMm, strDesign + ": three 0.4 mm line widths"),
                ["baffle"] = V(p.BaffleMm, strDesign),
                ["baffle_gap"] = V(p.BaffleGapMm, strDesign + ": above the board, so it never presses on traces"),
                ["derived"] = new Dictionary<string, object?>
                {
                    ["antenna_zone_y_min_mm"] = StrF2(l.AntennaZoneYMm),
                    ["baffle_y_mm"] = StrF2(SensorNodeEnclosureModel.FBaffleY(l)),
                    ["base_underside_z_mm"] = StrF2(SensorNodeEnclosureModel.FBaseBottomZ(p)),
                    ["base_wall_top_z_mm"] = StrF2(SensorNodeEnclosureModel.FBaseTopZ(p, l)),
                    ["lid_ceiling_z_mm"] = StrF2(SensorNodeEnclosureModel.FCeilingZ(p, l)),
                    ["lid_top_z_mm"] = StrF2(SensorNodeEnclosureModel.FLidTopZ(p, l)),
                    ["nut_pocket_top_z_mm"] = StrF2(SensorNodeEnclosureModel.FNutPocketTopZ(p)),
                    ["screw_tip_z_mm"] = StrF2(SensorNodeEnclosureModel.FScrewTipZ(p, l)),
                    ["frame"] = "board features frame (assembled pose), board underside at z = 0",
                },
                ["resolution_floor_mm"] = StrF4(SensorNodeEnclosureModel.FResolutionFloorMm(p)),
                ["assembly_tolerance_mm"] = StrF4(fTol),
            },
            ["assembly_check"] = oFit.OProvenanceBlock(),
            ["sibling"] = new Dictionary<string, object?>
            {
                ["part"] = e == EnclosurePart.Base ? "lid" : "base",
                ["artifact_sha256"] = strSiblingHash,
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = EnclosureRun.StrToolVersion,
                ["picogk"] = EnclosureRun.StrPicoGKVersion,
                ["shapekernel"] = EnclosureRun.StrShapeKernelTag,
            },
            ["commit"] = strCommit,
            ["artifact"] = oArtifact,
            ["caveats"] = new List<object?>
            {
                "J1, J2 and U1-ANT carry no access depth in the registry (a socket housing's length and "
                    + "Espressif's clearance figure are not recorded), so the assembly check could not prove "
                    + "the header windows or the antenna zone; they were accepted by id and are listed under "
                    + "assembly_check.access_accepted. The windows run through the full lid height over each "
                    + "header's own outline, which a deeper access volume would also pass.",
                "Antenna: the walls, floor and lid top over the antenna zone are thinned to the thin-wall "
                    + "figure and no screw, nut, boss or post reaches into it. Whether that plastic, at that "
                    + "distance, detunes the antenna is not modelled. Check RSSI with the lid on.",
                "The BME280 chamber is separated by a baffle in the lid and vented through the lid top and "
                    + "both side walls, but the board's GND pour still conducts the module's heat to the "
                    + "sensor. The baffle stops convection, not conduction.",
                "The seam between base and lid is a plain gap with no lip or seal: splash and dust get in.",
                "The nut drops into its pocket from the underside and is held by a finger while the screw "
                    + "starts; the pocket's hex stops it turning once the screw bites.",
                "The board's OpenPartsCore entry was written for this run and may be uncommitted: "
                    + "assembly_check.registry_file_sha256 pins its content, not registry_commit.",
            },
        };
    }

    private static string StrF2(double f) => f.ToString("F2", CultureInfo.InvariantCulture);
    private static string StrF4(double f) => f.ToString("F4", CultureInfo.InvariantCulture);
}
