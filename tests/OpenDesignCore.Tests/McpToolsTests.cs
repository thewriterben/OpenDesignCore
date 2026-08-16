using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenDesignCore.Mcp;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The tool methods and their guards. Transport is the SDK's business; what is
/// ours is what the tools accept, refuse, and return.
/// </summary>
public sealed class McpToolsTests : IDisposable
{
    private readonly string _strPrevRoot = Environment.GetEnvironmentVariable("ODC_ROOT") ?? "";
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-mcp-tests").FullName;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strPrevRoot.Length > 0 ? _strPrevRoot : null);
        Directory.Delete(_strTempDir, recursive: true);
    }

    private static string StrRepoRoot()
    {
        DirectoryInfo? oDir = new(AppContext.BaseDirectory);
        while (oDir is not null && !File.Exists(Path.Combine(oDir.FullName, "OpenDesignCore.sln")))
            oDir = oDir.Parent;
        Assert.NotNull(oDir);
        return oDir.FullName;
    }

    [Fact]
    public void ListModels_DeclaresBothModelsAndTheirFloors()
    {
        using JsonDocument oDoc = JsonDocument.Parse(OdcTools.ListModels());
        List<string> aIds = [.. oDoc.RootElement.EnumerateArray().Select(o => o.GetProperty("id").GetString()!)];

        Assert.Contains("enclosure-shell/0.1", aIds);
        Assert.Contains("scan-cradle/0.1", aIds);
        Assert.All(oDoc.RootElement.EnumerateArray(),
            o => Assert.False(string.IsNullOrWhiteSpace(o.GetProperty("resolution_floor").GetString())));
    }

    [Fact]
    public void ListParts_ReadsTheCitedStoreAndCarriesCitations()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", StrRepoRoot());

        using JsonDocument oDoc = JsonDocument.Parse(OdcTools.ListParts());
        JsonElement oPart = Assert.Single(
            oDoc.RootElement.EnumerateArray(),
            o => o.GetProperty("id").GetString() == "parts/esp32-s3-wroom-1");

        Assert.Equal(18.0, oPart.GetProperty("envelope_mm").GetProperty("x").GetDouble());
        Assert.False(string.IsNullOrWhiteSpace(oPart.GetProperty("citation").GetString()));
    }

    [Fact]
    public void RunEnclosure_RefusesVoxelSizeFinerThanTheMcpLimit()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", StrRepoRoot());
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunEnclosure("parts/esp32-s3-wroom-1", voxelMm: 0.001));
        Assert.Contains("finer than the MCP limit", oEx.Message);
    }

    [Fact]
    public void RunCradle_RefusesAutoUnits()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strTempDir);
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunCradle("scan.stl", units: "auto", voxelMm: 0.4));
        Assert.Contains("AUTO is refused", oEx.Message);
    }

    [Fact]
    public void PathsEscapingTheRoot_AreRefused()
    {
        Assert.Throws<McpGuardException>(
            () => McpGuard.StrResolveInsideRoot(_strTempDir, Path.Combine("..", "..", "secrets.stl")));
        Assert.Throws<McpGuardException>(
            () => McpGuard.StrResolveInsideRoot(_strTempDir, Path.Combine(Path.GetTempPath(), "abs.stl")));
    }

    [Fact]
    public void VolumeBudget_RefusesImplausiblyLargeRequests()
    {
        // 200 mm cube at 0.05 mm voxels is ~6.4e10 voxels.
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => McpGuard.CheckVolume(200, 200, 200, 0.05f));
        Assert.Contains("over the MCP budget", oEx.Message);
    }

    [Fact]
    public void ServerRegistration_DiscoversEveryTool()
    {
        // Proves WithTools<OdcTools>() actually exposes the surface: same
        // registration path Program.cs uses, inspected through DI.
        Microsoft.Extensions.DependencyInjection.ServiceCollection oServices = new();
        oServices.AddMcpServer().WithTools<OdcTools>();

        using ServiceProvider oProvider = oServices.BuildServiceProvider();
        List<string> aNames = [.. oProvider
            .GetServices<ModelContextProtocol.Server.McpServerTool>()
            .Select(o => o.ProtocolTool.Name)];

        Assert.Equal(7, aNames.Count);
        foreach (string strExpected in new[]
        {
            "list_models", "list_parts", "list_runs", "get_provenance",
            "run_enclosure", "run_cradle", "handoff_to_studio",
        })
        {
            Assert.Contains(strExpected, aNames);
        }
    }

    [Fact]
    public void NoApprovalToolIsExposed()
    {
        // ADR-0009: this server can propose, never approve. If someone adds an
        // approval tool, this test is the alarm.
        IEnumerable<string> aToolNames = typeof(OdcTools)
            .GetMethods()
            .SelectMany(o => o.GetCustomAttributes(
                typeof(ModelContextProtocol.Server.McpServerToolAttribute), inherit: false))
            .Cast<ModelContextProtocol.Server.McpServerToolAttribute>()
            .Select(o => o.Name ?? "");

        Assert.DoesNotContain(aToolNames, s =>
            s.Contains("approve", StringComparison.OrdinalIgnoreCase)
            || s.Contains("confirm", StringComparison.OrdinalIgnoreCase));
    }
}
