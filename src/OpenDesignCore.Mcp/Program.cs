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
