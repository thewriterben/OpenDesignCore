using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using OpenDesignCore.Verification;

namespace OpenDesignCore.Mcp;

/// <summary>
/// What the operator pinned for <c>verify_artifact</c> (ADR-0018). Read once at
/// start-up from the server's environment. The agent sees the tool only when
/// this exists; it never sees the numbers as parameters.
/// </summary>
public sealed record VerifyConfig(string BlenderExe, double VolumeTolPct, double? BboxTolMm)
{
    public const string StrEnvBlender = "ODC_BLENDER";
    public const string StrEnvVolumeTolPct = "ODC_VERIFY_VOLUME_TOL_PCT";
    public const string StrEnvBboxTolMm = "ODC_VERIFY_BBOX_TOL_MM";

    /// <summary>
    /// Null, with the reason, when the tool must not be offered: no Blender, or
    /// no operator-declared volume tolerance. A default tolerance here would be
    /// the number ADR-0017 refuses to invent.
    /// </summary>
    public static VerifyConfig? OFromEnvironment(out string strReason)
        => OFrom(
            Environment.GetEnvironmentVariable(StrEnvBlender),
            Environment.GetEnvironmentVariable(StrEnvVolumeTolPct),
            Environment.GetEnvironmentVariable(StrEnvBboxTolMm),
            out strReason);

    public static VerifyConfig? OFrom(string? strBlender, string? strVolumeTol, string? strBboxTol, out string strReason)
    {
        if (string.IsNullOrWhiteSpace(strBlender) || !File.Exists(strBlender))
        {
            strReason = $"{StrEnvBlender} is '{strBlender ?? "(unset)"}'; no Blender there, so verify_artifact is not offered.";
            return null;
        }
        if (!double.TryParse(strVolumeTol, NumberStyles.Float, CultureInfo.InvariantCulture, out double fVol) || fVol <= 0)
        {
            strReason = $"{StrEnvVolumeTolPct} is '{strVolumeTol ?? "(unset)"}'; the operator has not declared a volume tolerance, so verify_artifact is not offered (ADR-0017: no default).";
            return null;
        }
        double? fBbox = null;
        if (!string.IsNullOrWhiteSpace(strBboxTol))
        {
            if (!double.TryParse(strBboxTol, NumberStyles.Float, CultureInfo.InvariantCulture, out double fB) || fB <= 0)
            {
                strReason = $"{StrEnvBboxTolMm} is '{strBboxTol}', not a positive number; verify_artifact is not offered.";
                return null;
            }
            fBbox = fB;
        }
        strReason = "";
        return new VerifyConfig(strBlender, fVol, fBbox);
    }
}

/// <summary>
/// The verification tool, registered only when <see cref="VerifyConfig"/> could
/// be read (see Program.cs). Separate from <see cref="OdcTools"/> so the
/// registration can be conditional: an agent is never shown a tool it cannot
/// call, and a skipped verification is an absent tool rather than a pass.
/// </summary>
[McpServerToolType]
public sealed class OdcVerifyTools
{
    private OdcVerifyTools() { }

    [McpServerTool(Name = "verify_artifact")]
    [Description("Ask a second, unrelated mesh kernel (Blender, headless) to measure a run's STL and lay its extents, volume and manifoldness beside the provenance sidecar's claims. Tolerances are pinned by the operator on the server and are NOT parameters: you cannot choose how lenient the check is. Writes a content-addressed odc/verification/0.1 record and a ledger row. `passed` means the sidecar describes the artifact within the operator's tolerances and the mesh is watertight — never that the part is good.")]
    public static string VerifyArtifact(
        [Description("Ledger run id of an STL-producing run (run_enclosure or run_cradle).")] long runId)
    {
        VerifyConfig oCfg = VerifyConfig.OFromEnvironment(out string strReason)
            ?? throw new McpGuardException("verify_artifact is not configured on this server: " + strReason);

        CrossCheckResult oResult;
        try
        {
            oResult = BlenderCrossCheck.Execute(
                OdcTools.StrArtifactsDir, OdcTools.StrLedgerPath, runId,
                oCfg.BlenderExe, oCfg.VolumeTolPct, oCfg.BboxTolMm, strCommit: "mcp",
                strTolSource: BlenderCrossCheck.StrDeclaredByOperator);
        }
        catch (CrossCheckException ex)
        {
            throw new McpGuardException(ex.Message);
        }

        return OdcTools.StrJson(new
        {
            verification_id = oResult.VerificationRunId,
            run_id = runId,
            record_sha256 = oResult.RecordSha256,
            passed = oResult.Passed,
            watertight = oResult.Manifold,
            claims = oResult.Claims.Select(c => new
            {
                claim = c.Claim,
                sidecar = c.Sidecar,
                blender = c.Blender,
                delta = c.Delta,
                tolerance = c.Tolerance,
                unit = c.ToleranceKind == "pct" ? "%" : "mm",
                agrees = c.Agrees,
            }),
            tolerance_source = BlenderCrossCheck.StrDeclaredByOperator,
            blender_version = oResult.Measurement.BlenderVersion,
            note = oResult.Passed
                ? "The sidecar describes the artifact within the operator's tolerances. This is not a statement that the part is correct for its purpose."
                : "Disagreement is recorded, not repaired. Report it; do not re-run hoping for a different answer — the inputs are the same.",
        });
    }
}
