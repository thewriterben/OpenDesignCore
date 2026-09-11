using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenDesignCore.Mcp;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The tool methods and their guards. Transport is the SDK's business; what is
/// ours is what the tools accept, refuse, and return.
/// </summary>
[Collection(ProcessEnvironmentCollection.StrName)]
public sealed class McpToolsTests : IDisposable
{
    private readonly string _strPrevRoot = Environment.GetEnvironmentVariable("ODC_ROOT") ?? "";
    private readonly string _strPrevRegistry =
        Environment.GetEnvironmentVariable(OpenDesignCore.Data.PartsRegistry.StrEnvVar) ?? "";
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-mcp-tests").FullName;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strPrevRoot.Length > 0 ? _strPrevRoot : null);
        Environment.SetEnvironmentVariable(
            OpenDesignCore.Data.PartsRegistry.StrEnvVar, _strPrevRegistry.Length > 0 ? _strPrevRegistry : null);
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

    /// <summary>Point the tools at a fixture registry (ADR-0016) for the duration of a test.</summary>
    private void UseFixtureRegistry()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", StrRepoRoot());
        Environment.SetEnvironmentVariable(
            OpenDesignCore.Data.PartsRegistry.StrEnvVar,
            TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));
    }

    [Fact]
    public void ListParts_SeparatesOfferedFromNotOfferedAndNamesTheRegistry()
    {
        UseFixtureRegistry();

        using JsonDocument oDoc = JsonDocument.Parse(OdcTools.ListParts());
        JsonElement oRoot = oDoc.RootElement;

        Assert.Equal("OpenPartsCore", oRoot.GetProperty("registry").GetProperty("repo").GetString());

        JsonElement oPart = Assert.Single(
            oRoot.GetProperty("offered").EnumerateArray(),
            o => o.GetProperty("id").GetString() == TestRegistry.StrFixtureId);
        Assert.Equal(TestRegistry.FX, oPart.GetProperty("envelope_mm").GetProperty("x").GetDouble());
        Assert.False(string.IsNullOrWhiteSpace(oPart.GetProperty("envelope_citation").GetString()));

        // Compact by default: ids only, with the count and the one shared reason.
        Assert.Equal(1, oRoot.GetProperty("not_offered_count").GetInt32());
        JsonElement oUnoffered = Assert.Single(oRoot.GetProperty("not_offered").EnumerateArray());
        Assert.Equal(TestRegistry.StrUnofferedId, oUnoffered.GetString());
        Assert.Contains("includeReasons", oRoot.GetProperty("not_offered_reason").GetString());
    }

    /// <summary>
    /// The verbose shape is opt-in. The first shape of list_parts was ~4.9k tokens
    /// for the shipped registry and pushed an 8k-context agent past its window.
    /// </summary>
    [Fact]
    public void ListParts_ReasonsAndFullCitationsAreOptIn()
    {
        UseFixtureRegistry();

        using JsonDocument oDoc = JsonDocument.Parse(OdcTools.ListParts(includeReasons: true, fullCitations: true));
        JsonElement oRoot = oDoc.RootElement;
        JsonElement oUnoffered = Assert.Single(oRoot.GetProperty("not_offered").EnumerateArray());
        Assert.Equal(TestRegistry.StrUnofferedId, oUnoffered.GetProperty("id").GetString());
        Assert.Contains("no envelope_mm", oUnoffered.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, oRoot.GetProperty("not_offered_reason").ValueKind);
    }

    [Fact]
    public void RunEnclosure_RefusesAnUnofferedPartWithItsReason()
    {
        UseFixtureRegistry();
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunEnclosure(TestRegistry.StrUnofferedId, voxelMm: 0.5));
        Assert.Contains("not offered", oEx.Message);
        Assert.Contains("list_parts", oEx.Message);
    }

    [Fact]
    public void RunEnclosure_RefusesVoxelSizeFinerThanTheMcpLimit()
    {
        UseFixtureRegistry();
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunEnclosure(TestRegistry.StrFixtureId, voxelMm: 0.001));
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
    public void RunCradle_RefusesAnUndeclaredScanOrigin()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strTempDir);

        // Units are fine here; what is missing is the origin. The caller must
        // read why, not just that it failed (ADR-0019, and the refusal-
        // readability fix of 2026-09-07).
        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunCradle("scan.stl", units: "mm", voxelMm: 0.4));

        Assert.Contains("cad-export|metrology-scan|photogrammetry", oEx.Message);
        Assert.Contains("takes no default", oEx.Message);
    }

    [Fact]
    public void RunCradle_RefusesPhotogrammetryWithoutAScaleReference()
    {
        Environment.SetEnvironmentVariable("ODC_ROOT", _strTempDir);

        McpGuardException oEx = Assert.Throws<McpGuardException>(
            () => OdcTools.RunCradle(
                "scan.stl", units: "mm", voxelMm: 0.4, scanOrigin: "photogrammetry"));

        Assert.Contains("scale-free", oEx.Message);
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
