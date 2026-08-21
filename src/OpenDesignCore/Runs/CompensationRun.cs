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

    /// <summary>The material the measured part was printed in, from the comparison.</summary>
    public required string Material { get; init; }

    /// <summary>
    /// The catalogued filament variant, if the comparison named one, else
    /// "undeclared". Optional where <see cref="Material"/> is required: a
    /// missing material makes the compensation meaningless, a missing spool
    /// only makes it less precise.
    /// </summary>
    public required string FilamentRef { get; init; }
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
        string strMaterial = StrParseMaterial(abComparison);
        string strFilamentRef = StrParseFilamentRef(abComparison);

        Dictionary<string, object?> oRecord = new()
        {
            ["schema"] = "odc/compensation/0.2",
            ["model"] = StrModelId,
            ["inputs"] = new Dictionary<string, object?>
            {
                // Hash-chained: this proposal is about one specific measurement.
                ["comparison_sha256"] = strComparisonSha256,
                ["max_axis_spread_pct"] = StrF3(fMaxAxisSpreadPct),
                ["material"] = strMaterial,
                ["filament_ref"] = strFilamentRef,
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
            Material = strMaterial,
            FilamentRef = strFilamentRef,
        };
    }

    /// <summary>
    /// The catalogued filament variant from the comparison, or "undeclared".
    ///
    /// Absent is allowed here where an absent material is refused, and the
    /// asymmetry is deliberate: without a material the compensation cannot be
    /// filed at all, whereas without a spool it can — it is simply a
    /// compensation for "some PLA" rather than for one identified spool, which
    /// is what every compensation was before ADR-0013. Records written under
    /// schema odc/comparison/0.1 predate the field and read as "undeclared"
    /// rather than failing.
    /// </summary>
    private static string StrParseFilamentRef(byte[] abComparison)
    {
        using JsonDocument oDoc = JsonDocument.Parse(abComparison);
        return oDoc.RootElement.GetProperty("inputs")
            .TryGetProperty("filament_ref", out JsonElement oRef)
            && oRef.GetString() is { Length: > 0 } strRef
                ? strRef
                : "undeclared";
    }

    /// <summary>
    /// The material from the comparison, or a refusal.
    ///
    /// Older records predate the field and say nothing. Those are refused
    /// rather than defaulted, because the whole point is that an unlabelled
    /// measurement must not silently become a labelled compensation.
    /// </summary>
    private static string StrParseMaterial(byte[] abComparison)
    {
        using JsonDocument oDoc = JsonDocument.Parse(abComparison);
        string strMaterial =
            oDoc.RootElement.GetProperty("inputs").TryGetProperty("material", out JsonElement oMat)
                ? oMat.GetString() ?? "" : "";

        if (strMaterial.Length == 0 || strMaterial == "undeclared")
        {
            throw new CompensationException(
                "The comparison does not record which material the part was printed in, so "
                + "this compensation cannot be tied to one. Re-run `compare` with --material. "
                + "A shrinkage figure describes a single material, and an unlabelled one is "
                + "exactly how a PLA measurement ends up in a PETG profile.");
        }
        return strMaterial;
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
    ///
    /// The machine's calibration is a required argument, not an optional
    /// check. See <see cref="MachineCalibration"/> for why measuring an
    /// unverified machine is fine but writing the result into a profile is
    /// not — and note that ADR-0009 draws that exact line elsewhere too:
    /// recording into our own store executes, anything that shapes work
    /// beyond it proposes. Here the line falls one step earlier, because a
    /// material profile shapes every future print and a human approving a
    /// number in a dashboard has no way to see the machine underneath it.
    /// </summary>
    public static string StrPropose(
        CompensationRunResult oResult,
        string strStudioUrl,
        string strProfileKey,
        MachineCalibration oMachine)
    {
        if (!oResult.Proposal.Actionable)
        {
            throw new CompensationException(
                $"Refusing to propose: the verdict was {oResult.Proposal.Verdict}. "
                + oResult.Proposal.Reason);
        }

        // The gate Benji's first real print argued for. That machine's Y axis
        // was 0.83 % short while X was 0.25 % long; averaged into one XY
        // shrinkage figure it would have read as "PLA shrinks 0.29 %" and
        // been believed by every subsequent print in PLA.
        if (oMachine.State != MachineCalibrationState.Verified)
        {
            throw new CompensationException(
                $"Refusing to propose: {oMachine.Reason}. The measurement stands and is "
                + "recorded — but a shrinkage figure written into a profile shapes every "
                + "future print in that material, and on an unverified machine part of that "
                + "figure is the machine, filed under the material's name. Calibrate the "
                + "axes against a known length, record the result in the machine registry, "
                + "then re-measure. (Recording the comparison never required this; only "
                + "proposing it does.)");
        }

        // The gate that would have caught the real mistake. The walkthrough's
        // example command said `--propose-to-profile petg` while the block was
        // printed in PLA; nothing in the pipeline knew or cared, and a PLA
        // measurement would have landed in the PETG profile carrying a
        // provenance hash that made it look sourced.
        //
        // A substring match rather than equality, because a profile key may be
        // more specific than a material name — "petg" measured, "petg-cf" or
        // "esun-petg" as the profile — and refusing that would be pedantry.
        // "pla" against "petg" is what this is for.
        string strKey = strProfileKey.Trim().ToLowerInvariant();
        if (!strKey.Contains(oResult.Material) && !oResult.Material.Contains(strKey))
        {
            throw new CompensationException(
                $"Refusing to propose: the part was measured in '{oResult.Material}' but the "
                + $"target profile is '{strProfileKey}'. Shrinkage is a property of one "
                + "material, so this would file a measurement under a material it does not "
                + "describe — and it would arrive carrying a provenance hash that made it "
                + "look sourced. Propose to the profile for the material you actually "
                + "printed.");
        }

        using HttpClient oHttp = StudioClient.OCreate();
        Dictionary<string, object?> oBody = new()
        {
            ["action"] = "profile_update",
            ["params"] = new Dictionary<string, object?>
            {
                ["key"] = strProfileKey,
                ["nominal_xy_mm"] = oResult.Proposal.NominalXyMm,
                ["measured_xy_mm"] = oResult.Proposal.MeasuredXyMm,
                // The material rides in the origin string as well as being
                // gated on, so the stored value is self-describing even to a
                // reader who never fetches the comparison record.
                // The machine rides along too. A reader six months from now
                // asking "was the printer any good when this was measured?"
                // should not have to take it on trust.
                // The spool rides along when one was named. A profile keyed
                // "pla" that was measured on one specific spool should say so:
                // the next person to wonder whether the number transfers to a
                // different brand can see that it might not. The prefix is
                // unchanged — AdvancedStudio matches on `odc-comparison:<sha>`.
                ["origin"] = $"odc-comparison:{oResult.ComparisonSha256} ({oResult.Material}"
                             + (oResult.FilamentRef == "undeclared"
                                 ? ", spool undeclared"
                                 : $", spool {oResult.FilamentRef}")
                             + $", machine {oMachine.MachineId} worst axis residual "
                             + $"{oMachine.WorstResidualPct?.ToString("F3", CultureInfo.InvariantCulture)} %)",
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
            // Optional by design: a hand measurement records this as
            // "not-measurable-by-hand", because a caliper cannot give a
            // volume. The compensation judgement is entirely about extents
            // and axis spread, so an unreadable volume must not stop the
            // record loading — it would refuse the whole caliper path over a
            // field it never consults.
            ScanVolumeCubicMm = FParseOptional(oSummary, "scan_volume_cubic_mm"),
            VoxelSizeMm = FParse(oRoot, "voxel_size_mm"),
            ScanAccuracyMm = fAccuracy,
        };
    }

    private static double FParse(JsonElement oParent, string strKey)
        => double.Parse(oParent.GetProperty(strKey).GetString()!, CultureInfo.InvariantCulture);

    /// <summary>
    /// A number, or zero when the record says the value was not measurable.
    ///
    /// Only for fields the compensation does not consult. Applying this to a
    /// figure the verdict depends on would turn a missing measurement into a
    /// silent zero, which is the failure this codebase spends most of its
    /// effort avoiding.
    /// </summary>
    private static double FParseOptional(JsonElement oParent, string strKey)
        => oParent.TryGetProperty(strKey, out JsonElement oValue)
           && double.TryParse(oValue.GetString(), CultureInfo.InvariantCulture, out double f)
            ? f : 0;

    private static string StrF3(double fValue) => fValue.ToString("F3", CultureInfo.InvariantCulture);
}
