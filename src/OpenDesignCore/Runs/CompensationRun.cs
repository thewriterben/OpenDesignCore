using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OpenDesignCore.Provenance;
using OpenDesignCore.Verification;

namespace OpenDesignCore.Runs;

public sealed class CompensationException(string strMessage) : Exception(strMessage);

public sealed record CompensationRunResult
{
    public required long RunId { get; init; }
    public required string ComparisonSha256 { get; init; }
    public required string RecordSha256 { get; init; }
    public required CompensationProposal Proposal { get; init; }
}

/// <summary>
/// Turns a recorded comparison into a compensation proposal, or into a
/// specific refusal to make one.
///
/// This is the far end of the verification loop: design → print → scan →
/// measured deviation → a slicer setting that makes the next print closer.
/// Until now the loop stopped one step short. `compare` produced a number and
/// a human retyped it into a slicer, if they remembered which print it came
/// from.
///
/// Two things it deliberately does not do:
///
///  * <b>It does not compute the slicer setting.</b> The nominal/measured pair
///    goes to AdvancedStudio, whose calibration calculators already turn it
///    into an OrcaSlicer shrinkage percentage and are already calibrated
///    against the process research. Two implementations of one formula across
///    two repos would drift, and the studio owns slicer semantics.
///  * <b>It does not apply anything.</b> A profile change reaches beyond this
///    engine's own stores, so under ADR-0009 it can only be proposed. A human
///    approves it where the machine is.
///
/// Input is the comparison record by hash, not a re-run of the geometry: the
/// record is the measurement, and re-deriving it would risk answering about
/// different bytes than the ones that were recorded.
/// </summary>
public static class CompensationRun
{
    public const string StrModelId = "compensation-proposal/0.1";

    public static CompensationRunResult Execute(
        string strArtifactsDir,
        string strLedgerPath,
        string strComparisonSha256,
        double fMaxAxisSpreadPct,
        string strCommit)
    {
        string strPath = ArtifactStore.StrPathFor(
            strArtifactsDir, strComparisonSha256, ".comparison.json");
        if (!File.Exists(strPath))
        {
            throw new CompensationException(
                $"No comparison record {strComparisonSha256[..Math.Min(12, strComparisonSha256.Length)]} "
                + $"in {strArtifactsDir}. Run `compare` first; this reads a recorded "
                + "measurement rather than making a new one.");
        }

        byte[] abComparison = File.ReadAllBytes(strPath);
        DimensionalReport oReport = OParseReport(abComparison, strPath);
        CompensationProposal oProposal = CompensationProposal.OJudge(oReport, fMaxAxisSpreadPct);

        Dictionary<string, object?> oRecord = new()
        {
            ["schema"] = "odc/compensation/0.1",
            ["model"] = StrModelId,
            ["inputs"] = new Dictionary<string, object?>
            {
                // Hash-chained: this proposal is about one specific measurement.
                ["comparison_sha256"] = strComparisonSha256,
                ["max_axis_spread_pct"] = StrF3(fMaxAxisSpreadPct),
            },
            ["verdict"] = oProposal.Verdict.ToString(),
            ["actionable"] = oProposal.Actionable,
            ["reason"] = oProposal.Reason,
            ["measurement"] = new Dictionary<string, object?>
            {
                ["nominal_x_mm"] = StrF3(oProposal.NominalXMm),
                ["nominal_y_mm"] = StrF3(oProposal.NominalYMm),
                ["measured_x_mm"] = StrF3(oProposal.MeasuredXMm),
                ["measured_y_mm"] = StrF3(oProposal.MeasuredYMm),
                ["nominal_xy_mm"] = StrF3(oProposal.NominalXyMm),
                ["measured_xy_mm"] = StrF3(oProposal.MeasuredXyMm),
                ["axis_spread_pct"] = StrF3(oProposal.AxisSpreadPct),
                ["z_deviation_pct"] = oProposal.ZDeviationPct is double fZ
                    ? StrF3(fZ) : "absent",
            },
            ["caveats"] = new List<object?>
            {
                "The nominal/measured XY pair is for a shrinkage calculator; this engine "
                    + "deliberately does not compute the slicer setting itself. "
                    + "AdvancedStudio's calibration calculators own that arithmetic.",
                "Z is reported separately and is never folded into the XY figure. "
                    + "OrcaSlicer's Shrinkage (XY) applies to X and Y only, and Z deviation "
                    + "has different causes (layer squish, first-layer offset).",
                "A compensation derived from one part corrects that part's process, not a "
                    + "material in general. Shrinkage varies by spool, geometry and cooling.",
            },
            ["versions"] = new Dictionary<string, object?>
            {
                ["tool"] = EnclosureRun.StrToolVersion,
            },
            ["commit"] = strCommit,
        };

        byte[] abRecord = CanonicalJson.Serialize(oRecord);
        string strRecordHash = ArtifactStore.StrStore(strArtifactsDir, abRecord, ".compensation.json");

        using Ledger oLedger = new(strLedgerPath);
        long nRunId = oLedger.NAppend(new RunRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Model = StrModelId,
            VoxelSizeMm = "n/a",
            InputsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oRecord["inputs"])),
            VersionsJson = System.Text.Encoding.ASCII.GetString(
                CanonicalJson.Serialize(oRecord["versions"])),
            ArtifactSha256 = strRecordHash,
            ProvenanceSha256 = strRecordHash,
            // "Passed" means a proposal was produced, not that the print was
            // good. A refusal is a correct outcome and an honest one, but it
            // is not a proposal, and the ledger should not read as though a
            // compensation exists when none does.
            Passed = oProposal.Actionable,
        });

        return new CompensationRunResult
        {
            RunId = nRunId,
            ComparisonSha256 = strComparisonSha256,
            RecordSha256 = strRecordHash,
            Proposal = oProposal,
        };
    }

    /// <summary>
    /// Offer an actionable proposal to AdvancedStudio's profile store.
    ///
    /// Sends the measured pair, never a computed setting: the studio's
    /// calibration calculators own the shrinkage arithmetic, and the origin
    /// travels with it so the stored value can be traced back to the scan.
    ///
    /// Propose-only (ADR-0009). This returns a confirmation id; a human
    /// approves it in the studio, where the machine is.
    /// </summary>
    public static string StrPropose(
        CompensationRunResult oResult, string strStudioUrl, string strProfileKey)
    {
        if (!oResult.Proposal.Actionable)
        {
            throw new CompensationException(
                $"Refusing to propose: the verdict was {oResult.Proposal.Verdict}. "
                + oResult.Proposal.Reason);
        }

        using HttpClient oHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
        Dictionary<string, object?> oBody = new()
        {
            ["action"] = "profile_update",
            ["params"] = new Dictionary<string, object?>
            {
                ["key"] = strProfileKey,
                ["nominal_xy_mm"] = oResult.Proposal.NominalXyMm,
                ["measured_xy_mm"] = oResult.Proposal.MeasuredXyMm,
                ["origin"] = $"odc-comparison:{oResult.ComparisonSha256}",
            },
        };

        HttpResponseMessage oResp;
        try
        {
            oResp = oHttp.PostAsJsonAsync($"{strStudioUrl}/api/propose", oBody)
                .GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new CompensationException(
                $"AdvancedStudio unreachable at {strStudioUrl} ({e.Message}). The "
                + "compensation record is written; only the proposal did not reach the studio.");
        }

        string strBody = oResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!oResp.IsSuccessStatusCode)
            throw new CompensationException($"Studio rejected the proposal ({(int)oResp.StatusCode}): {strBody}");

        using JsonDocument oDoc = JsonDocument.Parse(strBody);
        return oDoc.RootElement.GetProperty("confirmation_id").GetString()
            ?? throw new CompensationException("Studio response lacked confirmation_id.");
    }

    /// <summary>
    /// Rebuild the report from the recorded comparison. Lengths in the record
    /// are unit-keyed strings, never floats, so they are parsed back
    /// explicitly rather than trusted to a serializer's round trip.
    /// </summary>
    private static DimensionalReport OParseReport(byte[] abComparison, string strPath)
    {
        using JsonDocument oDoc = JsonDocument.Parse(abComparison);
        JsonElement oRoot = oDoc.RootElement;

        string strSchema = oRoot.TryGetProperty("schema", out JsonElement oSchema)
            ? oSchema.GetString() ?? "" : "";
        if (!strSchema.StartsWith("odc/comparison/", StringComparison.Ordinal))
        {
            throw new CompensationException(
                $"{strPath} is not a comparison record (schema '{strSchema}'). "
                + "A compensation is a reading of a measurement; it needs the measurement.");
        }

        List<AxisDeviation> aAxes = [];
        foreach (JsonElement oAxis in oRoot.GetProperty("axes").EnumerateArray())
        {
            aAxes.Add(new AxisDeviation
            {
                Axis = oAxis.GetProperty("axis").GetString()!,
                DesignMm = FParse(oAxis, "design_mm"),
                ScanMm = FParse(oAxis, "scan_mm"),
            });
        }

        JsonElement oInputs = oRoot.GetProperty("inputs");
        string strAccuracy = oInputs.GetProperty("declared_scan_accuracy_mm").GetString() ?? "undeclared";
        double fAccuracy = strAccuracy == "undeclared"
            ? 0
            : double.Parse(strAccuracy, CultureInfo.InvariantCulture);

        JsonElement oSummary = oRoot.GetProperty("summary");
        return new DimensionalReport
        {
            Axes = aAxes,
            DesignVolumeCubicMm = FParse(oSummary, "design_volume_cubic_mm"),
            ScanVolumeCubicMm = FParse(oSummary, "scan_volume_cubic_mm"),
            VoxelSizeMm = FParse(oRoot, "voxel_size_mm"),
            ScanAccuracyMm = fAccuracy,
        };
    }

    private static double FParse(JsonElement oParent, string strKey)
        => double.Parse(oParent.GetProperty(strKey).GetString()!, CultureInfo.InvariantCulture);

    private static string StrF3(double fValue) => fValue.ToString("F3", CultureInfo.InvariantCulture);
}
