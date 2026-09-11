using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenDesignCore.Mcp;

// stdio MCP server exposing the OpenDesignCore engine to peers (ADR-0009).
// Working root comes from ODC_ROOT, else the current directory; data/,
// artifacts/, and ledger.db are resolved beneath it.
//
// Logs go to stderr: stdout is the protocol channel.
//
// So does everything else that would have gone to Console.Out. PicoGK's
// Library.Dispose writes "Disposing Library" / "Done Disposing Library" to the
// console — cosmetic in the CLI, fatal here: it landed between two JSON-RPC
// frames during run_enclosure and a strict client (Oh-Ben-Claw, 2026-09-06)
// reported "unparseable MCP frame" and lost the stream for good. The SDK's
// stdio transport writes to the raw standard-output stream, not Console.Out,
// so redirecting Console.Out to stderr moves the noise without touching the
// protocol. Verified by list_parts and run_enclosure through the redirected
// process.
Console.SetOut(Console.Error);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// The SDK's own categories are quiet no longer: Oh-Ben-Claw forwards this
// process's stderr into the agent log (obc #139, 2026-09-11), and every request
// handler start and finish is an information-level line, so a single tool call
// costs four. ODC's own lines were the ones getting lost.
LogLevel eSdkLevel = McpLogging.ELevelFrom(
    Environment.GetEnvironmentVariable(McpLogging.StrEnvLevel), out string? strLogNote);
builder.Logging.AddFilter(McpLogging.StrCategoryPrefix, eSdkLevel);
if (strLogNote is not null)
    Console.Error.WriteLine(strLogNote);

IMcpServerBuilder mcp = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OdcTools>();

// verify_artifact (ADR-0018) exists only when the operator has pinned its
// tolerances and Blender is present. Not offered is an honest absence; a tool
// that is listed and always refuses would be noise in every model's context.
if (VerifyConfig.OFromEnvironment(out string strVerifyReason) is VerifyConfig oVerify)
{
    mcp.WithTools<OdcVerifyTools>();
    Console.Error.WriteLine(
        $"verify_artifact offered: blender={oVerify.BlenderExe} volume_tol_pct={oVerify.VolumeTolPct} "
        + $"bbox_tol_mm={(oVerify.BboxTolMm is double f ? f.ToString(System.Globalization.CultureInfo.InvariantCulture) : "2 x voxel (derived per run)")}");
}
else
{
    Console.Error.WriteLine("verify_artifact not offered: " + strVerifyReason);
}

await builder.Build().RunAsync();

namespace OpenDesignCore.Mcp
{
    /// <summary>
    /// How loudly the MCP SDK itself logs. Warning by default: its per-request
    /// bookkeeping is noise in a forwarded agent log, while its warnings and
    /// errors are not.
    ///
    /// Overridable, because those same lines are the evidence when the stdio
    /// stream breaks — which has happened twice here (a PicoGK console write
    /// between two frames, and a server killed mid-call). A level that cannot
    /// be turned back up is one that is missing exactly when it is wanted.
    /// </summary>
    public static class McpLogging
    {
        public const string StrEnvLevel = "ODC_MCP_SDK_LOG_LEVEL";

        /// <summary>Category prefix; filter rules match by longest prefix.</summary>
        public const string StrCategoryPrefix = "ModelContextProtocol";

        /// <summary>
        /// Unset means Warning. A value that is not a level name is reported
        /// rather than swallowed: an operator who asked for Trace and silently
        /// got Warning would read the absence of lines as the absence of calls.
        /// </summary>
        public static LogLevel ELevelFrom(string? strLevel, out string? strNote)
        {
            strNote = null;
            if (string.IsNullOrWhiteSpace(strLevel))
                return LogLevel.Warning;
            if (Enum.TryParse(strLevel, ignoreCase: true, out LogLevel eLevel) && Enum.IsDefined(eLevel))
                return eLevel;
            strNote = $"{StrEnvLevel} is '{strLevel}', not a log level name; the MCP SDK stays at Warning.";
            return LogLevel.Warning;
        }
    }
}
