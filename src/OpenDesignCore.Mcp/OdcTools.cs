using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OpenDesignCore.Data;
using OpenDesignCore.Import;
using OpenDesignCore.Models;
using OpenDesignCore.Provenance;
using OpenDesignCore.Runs;

namespace OpenDesignCore.Mcp;

/// <summary>
/// The OpenDesignCore MCP surface (ADR-0009).
///
/// Reads and deterministic model runs execute — their only effects are
/// content-addressed artifacts and append-only ledger rows, reproducible from
/// the recorded inputs. Anything reaching outside those stores stops at a
/// proposal: `handoff` stages and proposes, and this server exposes no way to
/// approve anything. Approval belongs to the human, in the fabricator's own
/// interface.
/// </summary>
[McpServerToolType]
public sealed class OdcTools
{
    private OdcTools() { } // tools are static; the type exists for discovery

    private static readonly JsonSerializerOptions s_oJson = new() { WriteIndented = false };

    private static string StrRoot => Environment.GetEnvironmentVariable("ODC_ROOT") ?? Environment.CurrentDirectory;
    private static string StrDataDir => Path.Combine(StrRoot, "data");
    private static string StrArtifactsDir => Path.Combine(StrRoot, "artifacts");
    private static string StrLedgerPath => Path.Combine(StrRoot, "ledger.db");

    private static string StrJson(object oValue) => JsonSerializer.Serialize(oValue, s_oJson);

    [McpServerTool(Name = "list_models")]
    [Description("List the geometry models this engine can run, with their parameters and resolution-floor rule.")]
    public static string ListModels() => StrJson(new[]
    {
        new
        {
            id = EnclosureShellModel.StrModelId,
            summary = "Open-top tray around a part envelope from the cited parts registry.",
            parameters = new[] { "part_id", "voxel_mm", "clearance_mm", "wall_mm" },
            resolution_floor = "wall_mm / 2",
        },
        new
        {
            id = CradleModel.StrModelId,
            summary = "Foam-insert-style cradle carved to fit an imported scan.",
            parameters = new[] { "stl_path", "units", "voxel_mm", "clearance_mm", "wall_mm", "split_fraction" },
            resolution_floor = "min(wall_mm / 2, clearance_mm)",
        },
    });

    [McpServerTool(Name = "list_parts")]
    [Description("List parts in the cited reference data store, with envelopes in mm and their source citations.")]
    public static string ListParts()
    {
        DataSet oData = DataStore.LoadAll(StrDataDir);
        return StrJson(oData.Parts.Select(o => new
        {
            id = o.Id,
            name = o.Name,
            envelope_mm = new { x = o.EnvelopeMm.X, y = o.EnvelopeMm.Y, z = o.EnvelopeMm.Z },
            citation = o.Source.Citation,
        }));
    }

    [McpServerTool(Name = "list_runs")]
    [Description("List recorded model runs from the provenance ledger, newest last.")]
    public static string ListRuns([Description("Maximum rows to return (default 20).")] int limit = 20)
    {
        using Ledger oLedger = new(StrLedgerPath);
        return StrJson(oLedger.ARuns().TakeLast(Math.Clamp(limit, 1, 200)).Select(o => new
        {
            id = o.Id,
            created_utc = o.CreatedUtc,
            model = o.Model,
            voxel_size_mm = o.VoxelSizeMm,
            artifact_sha256 = o.ArtifactSha256,
            provenance_sha256 = o.ProvenanceSha256,
            passed = o.Passed,
        }));
    }

    [McpServerTool(Name = "get_provenance")]
    [Description("Return the full provenance sidecar for a recorded run: inputs, voxel size, pinned versions, commit, artifact hash.")]
    public static string GetProvenance([Description("Ledger run id.")] long runId)
    {
        using Ledger oLedger = new(StrLedgerPath);
        RunRecord oRun = oLedger.ORunById(runId)
            ?? throw new McpGuardException($"Run {runId} not found.");
        string strPath = ArtifactStore.StrPathFor(StrArtifactsDir, oRun.ProvenanceSha256, ".provenance.json");
        if (!File.Exists(strPath))
            throw new McpGuardException($"Provenance sidecar missing from the artifact store for run {runId}.");
        return File.ReadAllText(strPath);
    }

    [McpServerTool(Name = "run_enclosure")]
    [Description("Run the enclosure model around a part from the reference data store. Deterministic: writes a content-addressed STL, a provenance sidecar, and a ledger row. Voxel size is required and never defaulted.")]
    public static string RunEnclosure(
        [Description("Part id, e.g. 'parts/esp32-s3-wroom-1'.")] string partId,
        [Description("Voxel size in mm. Required; no default.")] double voxelMm,
        [Description("Clearance per side in mm (default 0.30).")] double clearanceMm = 0.30,
        [Description("Wall and floor thickness in mm (default 2.40).")] double wallMm = 2.40)
    {
        McpGuard.CheckVoxelSize((float)voxelMm);

        DataSet oData = DataStore.LoadAll(StrDataDir);
        PartEntry oPart = oData.Parts.FirstOrDefault(o => o.Id == partId)
            ?? throw new McpGuardException($"Part '{partId}' not found. Call list_parts.");
        McpGuard.CheckVolume(
            oPart.EnvelopeMm.X + 2 * clearanceMm + 2 * wallMm,
            oPart.EnvelopeMm.Y + 2 * clearanceMm + 2 * wallMm,
            oPart.EnvelopeMm.Z + clearanceMm + wallMm,
            (float)voxelMm);

        EnclosureRunResult oResult = EnclosureRun.Execute(
            StrDataDir, partId, (float)voxelMm, (float)clearanceMm, (float)wallMm,
            StrArtifactsDir, StrLedgerPath, strCommit: "mcp");

        return StrJson(new
        {
            run_id = oResult.RunId,
            artifact_sha256 = oResult.ArtifactSha256,
            provenance_sha256 = oResult.ProvenanceSha256,
            artifact_path = oResult.ArtifactPath,
        });
    }

    [McpServerTool(Name = "run_cradle")]
    [Description("Import a scanned mesh and carve a cradle that fits it. Units must be declared explicitly (mm|cm|m|in|ft) — they are never inferred from the file.")]
    public static string RunCradle(
        [Description("Path to the scan STL, relative to the working root.")] string stlPath,
        [Description("Units the scan is in: mm, cm, m, in, or ft. AUTO is refused.")] string units,
        [Description("Voxel size in mm. Required; no default.")] double voxelMm,
        [Description("Clearance around the scan per side in mm (default 0.40).")] double clearanceMm = 0.40,
        [Description("Wall thickness in mm (default 2.40).")] double wallMm = 2.40,
        [Description("Fraction of the scan height the cradle rises to, 0..1 (default 0.45).")] double splitFraction = 0.45)
    {
        McpGuard.CheckVoxelSize((float)voxelMm);
        if (!Enum.TryParse(units, ignoreCase: true, out PicoGK.Mesh.EStlUnit eUnits)
            || eUnits == PicoGK.Mesh.EStlUnit.AUTO)
        {
            throw new McpGuardException(
                "Units must be one of mm, cm, m, in, ft. AUTO is refused: an STL carries no " +
                "reliable unit information and a silently mis-scaled scan is a real bug.");
        }

        string strStl = McpGuard.StrResolveInsideRoot(StrRoot, stlPath);
        CradleRunResult oResult = CradleRun.Execute(
            strStl, eUnits, 1.0f, (float)voxelMm, (float)clearanceMm, (float)wallMm,
            (float)splitFraction, StrArtifactsDir, StrLedgerPath, strCommit: "mcp");

        return StrJson(new
        {
            run_id = oResult.RunId,
            scan_sha256 = oResult.ScanSha256,
            artifact_sha256 = oResult.ArtifactSha256,
            provenance_sha256 = oResult.ProvenanceSha256,
            artifact_path = oResult.ArtifactPath,
        });
    }

    [McpServerTool(Name = "handoff_to_studio")]
    [Description("Stage a run's artifact for fabrication and, if sliced G-code is named, PROPOSE the print to AdvancedStudio. This never starts a print: a human approves in the studio dashboard. This server has no approval tool.")]
    public static string HandoffToStudio(
        [Description("Ledger run id to hand off.")] long runId,
        [Description("Staging directory for the STL, relative to the working root.")] string stageDir,
        [Description("Studio base URL (default http://localhost:8770).")] string studioUrl = "http://localhost:8770",
        [Description("Optional: filename of already-sliced G-code on the printer. Omit to stage only.")] string? gcodeFilename = null)
    {
        string strStage = McpGuard.StrResolveInsideRoot(StrRoot, stageDir);
        HandoffResult oResult = StudioHandoff.Execute(
            StrArtifactsDir, StrLedgerPath, runId, strStage, studioUrl.TrimEnd('/'),
            gcodeFilename, bOffline: false);

        return StrJson(new
        {
            handoff_id = oResult.HandoffId,
            status = oResult.Status,
            staged_path = oResult.StagedStlPath,
            proposal_id = oResult.ProposalId,
            will_run = oResult.WillRun,
            note = oResult.ProposalId.Length > 0
                ? "Proposed only. A human must approve it in the AdvancedStudio dashboard."
                : "Staged only. Slice it, then call again with gcode_filename to propose the print.",
        });
    }
}
