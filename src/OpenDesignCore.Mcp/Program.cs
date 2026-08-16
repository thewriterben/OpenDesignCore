using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenDesignCore.Mcp;

// stdio MCP server exposing the OpenDesignCore engine to peers (ADR-0009).
// Working root comes from ODC_ROOT, else the current directory; data/,
// artifacts/, and ledger.db are resolved beneath it.
//
// Logs go to stderr: stdout is the protocol channel.

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OdcTools>();

await builder.Build().RunAsync();
