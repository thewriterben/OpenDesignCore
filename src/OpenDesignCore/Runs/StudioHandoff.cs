using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OpenDesignCore.Provenance;

namespace OpenDesignCore.Runs;

public sealed class HandoffException(string strMessage) : Exception(strMessage);

public sealed record HandoffResult
{
    public required long HandoffId { get; init; }
    public required string Status { get; init; }
    public required string StagedStlPath { get; init; }
    public required string ProposalId { get; init; }
    public required string UploadProposalId { get; init; }
    public required string WillRun { get; init; }
}

/// <summary>
/// Hands a recorded run's artifact to AdvancedStudio, honestly:
///
/// AdvancedStudio still has no slicer, and that is not this repo's problem to
/// solve: a human slices the staged STL in whatever slicer they trust. What
/// the studio now has is an *upload* seam, so the file need not be moved by
/// hand. Its only write path remains POST /api/propose — propose-only, a human
/// approves in the dashboard. Dimensional compensation deliberately lives in
/// the slicer (see ADR-0011 for how a measurement gets there).
///
/// A handoff is explicit steps, each recorded:
///  1. STAGE   — copy the STL + provenance sidecar to a slicing workspace,
///               hash-named so the slicer's output can carry its origin.
///  2. VERIFY  — studio must answer GET /api/state, else fail loudly
///               (--offline records "staged-offline" instead; never silent).
///  3. UPLOAD  — with --upload &lt;name&gt;: propose putting that sliced job on the
///               printer. Guarded, because the file lands on a networked
///               device and outlives the proposal (studio ADR-0002).
///  4. PRINT   — with --print &lt;name&gt;: propose starting it.
///
/// Upload and print are two proposals, not one. They are separate effects, and
/// bundling them would hide the second behind approval of the first. Both
/// carry the design's artifact hash, so the human approving sees which design
/// the job claims to be rather than only a filename.
/// </summary>
public static class StudioHandoff
{
    public static HandoffResult Execute(
        string strArtifactsDir,
        string strLedgerPath,
        long nRunId,
        string strStageDir,
        string strStudioUrl,
        string? strGcodeFilename,
        bool bOffline,
        string? strUploadFilename = null)
    {
        using Ledger oLedger = new(strLedgerPath);
        RunRecord oRun = oLedger.ORunById(nRunId)
            ?? throw new HandoffException($"Run {nRunId} not found in {strLedgerPath}.");

        string strStlSrc = ArtifactStore.StrPathFor(strArtifactsDir, oRun.ArtifactSha256, ".stl");
        string strSidecarSrc = ArtifactStore.StrPathFor(strArtifactsDir, oRun.ProvenanceSha256, ".provenance.json");
        if (!File.Exists(strStlSrc))
            throw new HandoffException($"Artifact missing from store: {strStlSrc}");
        if (!File.Exists(strSidecarSrc))
            throw new HandoffException($"Provenance sidecar missing from store: {strSidecarSrc}");

        // 1. Stage, names carrying the content hash so the slicer output is traceable.
        Directory.CreateDirectory(strStageDir);
        string strStlDst = Path.Combine(strStageDir, $"{oRun.Model.Replace('/', '-')}-{oRun.ArtifactSha256[..12]}.stl");
        string strSidecarDst = strStlDst[..^4] + ".provenance.json";
        File.Copy(strStlSrc, strStlDst, overwrite: true);
        File.Copy(strSidecarSrc, strSidecarDst, overwrite: true);

        string strStatus;
        string strProposalId = "";
        string strUploadProposalId = "";
        string strWillRun = "";

        if (bOffline)
        {
            strStatus = "staged-offline";
            if (strGcodeFilename is not null || strUploadFilename is not null)
                throw new HandoffException("--print and --upload require the studio; drop --offline.");
        }
        else
        {
            using HttpClient oHttp = new() { Timeout = TimeSpan.FromSeconds(5) };

            // 2. Verify the studio answers.
            try
            {
                using HttpResponseMessage oState =
                    oHttp.GetAsync($"{strStudioUrl}/api/state").GetAwaiter().GetResult();
                oState.EnsureSuccessStatusCode();
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new HandoffException(
                    $"AdvancedStudio unreachable at {strStudioUrl} ({e.Message}). " +
                    "Start studio-core, or rerun with --offline to record a staged-offline handoff.");
            }
            strStatus = "staged";

            // 3. Propose the upload, if a sliced job was named. Two proposals
            //    rather than one: putting a file on the printer and starting a
            //    print are separate effects, and bundling them would hide the
            //    second behind approval of the first.
            if (strUploadFilename is not null)
            {
                (strUploadProposalId, strWillRun) = OPropose(
                    oHttp, strStudioUrl, "gcode_upload", new Dictionary<string, object?>
                    {
                        ["filename"] = strUploadFilename,
                        ["design_artifact_sha256"] = oRun.ArtifactSha256,
                    });
                strStatus = "upload-proposed";
            }

            // 4. Propose the print for an already-sliced G-code, if named. The
            //    design hash travels with it so the human approving sees which
            //    design the job claims to be, not just a filename.
            if (strGcodeFilename is not null)
            {
                (strProposalId, strWillRun) = OPropose(
                    oHttp, strStudioUrl, "print_start", new Dictionary<string, object?>
                    {
                        ["filename"] = strGcodeFilename,
                        ["design_artifact_sha256"] = oRun.ArtifactSha256,
                    });
                strStatus = "proposed";
            }
        }

        long nHandoffId = oLedger.NAppendHandoff(new HandoffRecord
        {
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            RunId = nRunId,
            ArtifactSha256 = oRun.ArtifactSha256,
            Destination = bOffline ? $"offline:{strStageDir}" : strStudioUrl,
            Status = strStatus,
            StagedPath = strStlDst,
            ProposalId = strProposalId,
        });

        return new HandoffResult
        {
            HandoffId = nHandoffId,
            Status = strStatus,
            StagedStlPath = strStlDst,
            ProposalId = strProposalId,
            UploadProposalId = strUploadProposalId,
            WillRun = strWillRun,
        };
    }

    /// <summary>
    /// Register one proposal and return its confirmation id and prompt.
    ///
    /// Nothing here approves anything: the studio holds the pending action and
    /// a human releases it where the machine is (ADR-0009). This engine only
    /// ever learns the id it was given.
    /// </summary>
    private static (string strId, string strWillRun) OPropose(
        HttpClient oHttp, string strStudioUrl, string strAction,
        Dictionary<string, object?> oParams)
    {
        using HttpResponseMessage oResp = oHttp.PostAsJsonAsync(
            $"{strStudioUrl}/api/propose",
            new Dictionary<string, object?>
            {
                ["action"] = strAction,
                ["params"] = oParams,
            }).GetAwaiter().GetResult();

        string strBody = oResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!oResp.IsSuccessStatusCode)
        {
            throw new HandoffException(
                $"Studio rejected the {strAction} proposal ({(int)oResp.StatusCode}): {strBody}");
        }

        using JsonDocument oDoc = JsonDocument.Parse(strBody);
        string strId = oDoc.RootElement.GetProperty("confirmation_id").GetString()
            ?? throw new HandoffException($"Studio response to {strAction} lacked confirmation_id.");
        string strWillRun = oDoc.RootElement.TryGetProperty("will_run", out JsonElement oWill)
            ? oWill.GetString() ?? "" : "";
        return (strId, strWillRun);
    }
}
