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
    public required string WillRun { get; init; }
}

/// <summary>
/// Hands a recorded run's artifact to AdvancedStudio, honestly:
///
/// AdvancedStudio (studio-core 0.2.0, surveyed 2026-08-15) has no file upload
/// and no slicer — it manages pre-sliced G-code already on the printer, and its
/// only write seam is POST /api/propose (propose-only; a human approves in the
/// dashboard). Dimensional compensation deliberately lives in the slicer.
///
/// So a handoff is three explicit steps, each recorded:
///  1. STAGE  — copy the STL + provenance sidecar to a slicing workspace.
///  2. VERIFY — studio must answer GET /api/state, else fail loudly
///              (--offline records "staged-offline" instead; never silent).
///  3. PROPOSE — only if a sliced G-code filename is given: POST /api/propose
///              {action: "print_start", params: {filename}} and record the
///              confirmation id. Approval stays with the human.
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
        bool bOffline)
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
        string strWillRun = "";

        if (bOffline)
        {
            strStatus = "staged-offline";
            if (strGcodeFilename is not null)
                throw new HandoffException("--print requires the studio; drop --offline.");
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

            // 3. Propose the print for the already-sliced G-code, if named.
            if (strGcodeFilename is not null)
            {
                using HttpResponseMessage oResp = oHttp.PostAsJsonAsync(
                    $"{strStudioUrl}/api/propose",
                    new Dictionary<string, object?>
                    {
                        ["action"] = "print_start",
                        ["params"] = new Dictionary<string, object?> { ["filename"] = strGcodeFilename },
                    }).GetAwaiter().GetResult();

                string strBody = oResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!oResp.IsSuccessStatusCode)
                    throw new HandoffException($"Studio rejected the proposal ({(int)oResp.StatusCode}): {strBody}");

                using JsonDocument oDoc = JsonDocument.Parse(strBody);
                strProposalId = oDoc.RootElement.GetProperty("confirmation_id").GetString()
                    ?? throw new HandoffException("Studio response lacked confirmation_id.");
                strWillRun = oDoc.RootElement.TryGetProperty("will_run", out JsonElement oWill)
                    ? oWill.GetString() ?? "" : "";
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
            WillRun = strWillRun,
        };
    }
}
