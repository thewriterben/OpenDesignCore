using Microsoft.Extensions.Logging;
using OpenDesignCore.Mcp;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// The MCP SDK's own log level. Quiet by default so a forwarded agent log
/// carries ODC's lines rather than four per tool call, and turnable back up
/// because those lines are the evidence when the stdio stream breaks.
/// </summary>
public sealed class McpLoggingTests
{
    [Fact]
    public void Unset_IsWarning()
    {
        Assert.Equal(LogLevel.Warning, McpLogging.ELevelFrom(null, out string? strNote));
        Assert.Null(strNote);
        Assert.Equal(LogLevel.Warning, McpLogging.ELevelFrom("   ", out _));
    }

    [Fact]
    public void ALevelName_IsHonoured_CaseInsensitively()
    {
        Assert.Equal(LogLevel.Information, McpLogging.ELevelFrom("Information", out string? strNote));
        Assert.Null(strNote);
        Assert.Equal(LogLevel.Trace, McpLogging.ELevelFrom("trace", out _));
        Assert.Equal(LogLevel.None, McpLogging.ELevelFrom("NONE", out _));
    }

    [Fact]
    public void Nonsense_FallsBackToWarningAndSaysSo()
    {
        // Silence here would read as "no calls happened" to whoever set it.
        Assert.Equal(LogLevel.Warning, McpLogging.ELevelFrom("verbose", out string? strNote));
        Assert.NotNull(strNote);
        Assert.Contains(McpLogging.StrEnvLevel, strNote);
        Assert.Contains("verbose", strNote);

        // Numeric values outside the enum are nonsense too, not level 99.
        Assert.Equal(LogLevel.Warning, McpLogging.ELevelFrom("99", out string? strNote99));
        Assert.NotNull(strNote99);
    }

    [Fact]
    public void ThePrefixMatchesTheCategoriesThatWereNoisy()
    {
        // The observed lines came from ModelContextProtocol.Server.McpServer and
        // .StdioServerTransport; a filter rule matches by longest category prefix.
        Assert.StartsWith(McpLogging.StrCategoryPrefix, "ModelContextProtocol.Server.McpServer", StringComparison.Ordinal);
        Assert.StartsWith(McpLogging.StrCategoryPrefix, "ModelContextProtocol.Server.StdioServerTransport", StringComparison.Ordinal);
        Assert.StartsWith(McpLogging.StrCategoryPrefix, typeof(ModelContextProtocol.Server.McpServerTool).FullName!, StringComparison.Ordinal);
    }
}
