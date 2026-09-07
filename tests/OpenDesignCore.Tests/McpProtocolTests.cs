using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenDesignCore.Mcp;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Through the wire, not just the method: what a client actually receives.
/// The unit tests see <see cref="McpGuardException"/>; a client sees only what
/// the SDK puts in the error result, and until 2026-09-07 that was
/// "An error occurred invoking '…'" for every refusal this surface made.
/// </summary>
[Collection(ProcessEnvironmentCollection.StrName)]
public sealed class McpProtocolTests : IDisposable
{
    private readonly string? _strPrevRoot = Environment.GetEnvironmentVariable("ODC_ROOT");
    private readonly string? _strPrevRegistry = Environment.GetEnvironmentVariable(OpenDesignCore.Data.PartsRegistry.StrEnvVar);
    private readonly string? _strPrevBlender = Environment.GetEnvironmentVariable(VerifyConfig.StrEnvBlender);
    private readonly string? _strPrevVolTol = Environment.GetEnvironmentVariable(VerifyConfig.StrEnvVolumeTolPct);
    private readonly string _strTempDir = Directory.CreateTempSubdirectory("odc-mcp-proto").FullName;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strPrevRoot);
        Environment.SetEnvironmentVariable(OpenDesignCore.Data.PartsRegistry.StrEnvVar, _strPrevRegistry);
        Directory.Delete(_strTempDir, recursive: true);
    }

    /// <summary>Server on one end of two anonymous pipes, client on the other, same process.</summary>
    private static async Task<(McpClient Client, Task ServerTask, CancellationTokenSource Cts)> OConnectAsync(bool bVerify)
    {
        AnonymousPipeServerStream oToServer = new(PipeDirection.Out, HandleInheritability.None);
        AnonymousPipeClientStream oServerIn = new(PipeDirection.In, oToServer.ClientSafePipeHandle);
        AnonymousPipeServerStream oFromServer = new(PipeDirection.In, HandleInheritability.None);
        AnonymousPipeClientStream oServerOut = new(PipeDirection.Out, oFromServer.ClientSafePipeHandle);

        ServiceCollection oServices = new();
        IMcpServerBuilder oBuilder = oServices.AddMcpServer()
            .WithStreamServerTransport(oServerIn, oServerOut)
            .WithTools<OdcTools>();
        if (bVerify) oBuilder.WithTools<OdcVerifyTools>();
        ServiceProvider oProvider = oServices.BuildServiceProvider();
        McpServer oServer = oProvider.GetRequiredService<McpServer>();
        CancellationTokenSource oCts = new();
        Task oServerTask = oServer.RunAsync(oCts.Token);

        McpClient oClient = await McpClient.CreateAsync(new StreamClientTransport(oToServer, oFromServer));
        return (oClient, oServerTask, oCts);
    }

    [Fact]
    public async Task ARefusalsReasonReachesTheClient()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", TestRegistry.StrRepoRoot());
        Environment.SetEnvironmentVariable(OpenDesignCore.Data.PartsRegistry.StrEnvVar,
            TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));

        (McpClient oClient, Task oServerTask, CancellationTokenSource oCts) = await OConnectAsync(bVerify: false);
        try
        {
            CallToolResult oResult = await oClient.CallToolAsync("run_enclosure",
                new Dictionary<string, object?> { ["partId"] = "boards/not-in-any-registry", ["voxelMm"] = 0.3 });

            Assert.True(oResult.IsError);
            string strText = string.Join("\n", oResult.Content.OfType<TextContentBlock>().Select(c => c.Text));
            // The SDK prefixes "An error occurred invoking 'run_enclosure':" —
            // fine; what matters is that our sentence follows it.
            Assert.Contains("not found", strText);
            Assert.Contains("list_parts", strText);
        }
        finally
        {
            await oClient.DisposeAsync();
            oCts.Cancel();
            try { await oServerTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task VerifyArtifact_IsAbsentUnconfigured_AndRefusesAnUnknownRunWithItsReason()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", TestRegistry.StrRepoRoot());
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvBlender, typeof(McpProtocolTests).Assembly.Location);
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvVolumeTolPct, "1.0");
        try
        {
            (McpClient oOff, Task tOff, CancellationTokenSource cOff) = await OConnectAsync(bVerify: false);
            try
            {
                Assert.DoesNotContain((await oOff.ListToolsAsync()).Select(t => t.Name), s => s == "verify_artifact");
            }
            finally { await oOff.DisposeAsync(); cOff.Cancel(); try { await tOff; } catch (OperationCanceledException) { } }

            (McpClient oOn, Task tOn, CancellationTokenSource cOn) = await OConnectAsync(bVerify: true);
            try
            {
                Assert.Contains((await oOn.ListToolsAsync()).Select(t => t.Name), s => s == "verify_artifact");
                CallToolResult oResult = await oOn.CallToolAsync("verify_artifact",
                    new Dictionary<string, object?> { ["runId"] = long.MaxValue });
                Assert.True(oResult.IsError);
                string strText = string.Join("\n", oResult.Content.OfType<TextContentBlock>().Select(c => c.Text));
                Assert.Contains("not found", strText);
            }
            finally { await oOn.DisposeAsync(); cOn.Cancel(); try { await tOn; } catch (OperationCanceledException) { } }
        }
        finally
        {
            Environment.SetEnvironmentVariable(VerifyConfig.StrEnvBlender, _strPrevBlender);
            Environment.SetEnvironmentVariable(VerifyConfig.StrEnvVolumeTolPct, _strPrevVolTol);
        }
    }
}
