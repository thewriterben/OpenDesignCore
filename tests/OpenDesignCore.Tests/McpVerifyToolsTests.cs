using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenDesignCore.Mcp;
using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// verify_artifact over MCP (ADR-0018): offered only when the operator pinned
/// its tolerances, and the agent can never pass a tolerance of its own.
/// </summary>
[Collection(ProcessEnvironmentCollection.StrName)]
public sealed class McpVerifyToolsTests : IDisposable
{
    private readonly Dictionary<string, string?> _oPrev = new[]
    {
        "ODC_ROOT", VerifyConfig.StrEnvBlender, VerifyConfig.StrEnvVolumeTolPct, VerifyConfig.StrEnvBboxTolMm,
    }.ToDictionary(s => s, Environment.GetEnvironmentVariable);

    public void Dispose()
    {
        foreach ((string k, string? v) in _oPrev)
            Environment.SetEnvironmentVariable(k, v);
    }

    private static string StrAnyExistingFile() => typeof(McpVerifyToolsTests).Assembly.Location;

    [Fact]
    public void Config_RefusesWithoutBlenderOrWithoutAVolumeTolerance()
    {
        Assert.Null(VerifyConfig.OFrom(null, "1.0", null, out string r1));
        Assert.Contains(VerifyConfig.StrEnvBlender, r1);

        Assert.Null(VerifyConfig.OFrom(StrAnyExistingFile(), null, null, out string r2));
        Assert.Contains("no default", r2);

        Assert.Null(VerifyConfig.OFrom(StrAnyExistingFile(), "0", null, out _));
        Assert.Null(VerifyConfig.OFrom(StrAnyExistingFile(), "1.0", "-1", out string r3));
        Assert.Contains(VerifyConfig.StrEnvBboxTolMm, r3);
    }

    [Fact]
    public void Config_ReadsInvariantCultureNumbers()
    {
        VerifyConfig? o = VerifyConfig.OFrom(StrAnyExistingFile(), "1.5", "0.4", out _);
        Assert.NotNull(o);
        Assert.Equal(1.5, o.VolumeTolPct);
        Assert.Equal(0.4, o.BboxTolMm);
        Assert.Null(VerifyConfig.OFrom(StrAnyExistingFile(), "1.5", null, out _)!.BboxTolMm);
    }

    [Fact]
    public void TheToolTakesNoToleranceParameters()
    {
        // The whole point of ADR-0018. If someone adds `volumeTolPct` to the
        // signature, this is the alarm.
        System.Reflection.MethodInfo o = typeof(OdcVerifyTools).GetMethod(nameof(OdcVerifyTools.VerifyArtifact))!;
        string[] aParams = [.. o.GetParameters().Select(p => p.Name!)];
        Assert.Equal(["runId"], aParams);
    }

    [Fact]
    public void Registration_IsConditional()
    {
        // Same registration path as Program.cs: OdcTools always, OdcVerifyTools
        // only with a config. Seven tools without, eight with.
        static List<string> ANames(bool bVerify)
        {
            ServiceCollection oServices = new();
            IMcpServerBuilder o = oServices.AddMcpServer().WithTools<OdcTools>();
            if (bVerify) o.WithTools<OdcVerifyTools>();
            using ServiceProvider oProvider = oServices.BuildServiceProvider();
            return [.. oProvider.GetServices<ModelContextProtocol.Server.McpServerTool>().Select(t => t.ProtocolTool.Name)];
        }

        Assert.DoesNotContain("verify_artifact", ANames(false));
        List<string> aWith = ANames(true);
        Assert.Equal(8, aWith.Count);
        Assert.Contains("verify_artifact", aWith);
    }

    [Fact]
    public void UnconfiguredCall_RefusesAndSaysWhy()
    {
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvBlender, null);
        McpGuardException ex = Assert.Throws<McpGuardException>(() => OdcVerifyTools.VerifyArtifact(50));
        Assert.Contains("not configured", ex.Message);
    }

    [Fact]
    public void UnknownRun_IsRefusedBeforeBlenderIsSpawned()
    {
        // A file that exists but is not Blender is enough: the run lookup comes first.
        Environment.SetEnvironmentVariable("ODC_ROOT", TestRegistry.StrRepoRoot());
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvBlender, StrAnyExistingFile());
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvVolumeTolPct, "1.0");
        McpGuardException ex = Assert.Throws<McpGuardException>(() => OdcVerifyTools.VerifyArtifact(long.MaxValue));
        Assert.Contains("not found", ex.Message);
    }

    /// <summary>
    /// Through the tool, against the real Blender and run 50: the record says
    /// the operator declared the tolerance. Returns early without Blender.
    /// </summary>
    [Fact]
    public void RealBlender_RecordNamesTheOperatorAsTheToleranceSource()
    {
        string? strBlender = Environment.GetEnvironmentVariable(VerifyConfig.StrEnvBlender);
        if (string.IsNullOrEmpty(strBlender) || !File.Exists(strBlender)) return;
        string strRoot = TestRegistry.StrRepoRoot();
        if (!File.Exists(Path.Combine(strRoot, "artifacts", "06",
            "06ac53848bc575284a835b83be4f033c7aef433ecd75be8ec974b3548aadf824.stl"))) return;

        Environment.SetEnvironmentVariable("ODC_ROOT", strRoot);
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvVolumeTolPct, "1.0");
        Environment.SetEnvironmentVariable(VerifyConfig.StrEnvBboxTolMm, null);

        using JsonDocument oDoc = JsonDocument.Parse(OdcVerifyTools.VerifyArtifact(50));
        JsonElement r = oDoc.RootElement;
        Assert.True(r.GetProperty("passed").GetBoolean());
        Assert.Equal(BlenderCrossCheck.StrDeclaredByOperator, r.GetProperty("tolerance_source").GetString());

        string strRecord = Path.Combine(strRoot, "artifacts", r.GetProperty("record_sha256").GetString()![..2],
            r.GetProperty("record_sha256").GetString() + ".verification.json");
        using JsonDocument oRec = JsonDocument.Parse(File.ReadAllText(strRecord));
        JsonElement oIn = oRec.RootElement.GetProperty("inputs");
        Assert.Equal(BlenderCrossCheck.StrDeclaredByOperator, oIn.GetProperty("volume_tolerance_source").GetString());
        Assert.Equal("2 x voxel_size_mm (0.300)", oIn.GetProperty("bbox_tolerance_source").GetString());
    }
}
