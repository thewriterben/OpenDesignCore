using OpenDesignCore.Runs;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Reading the flow a part was printed at from its G-code (AdvancedStudio
/// ADR-0004). Every case that is not exactly one readable figure is a
/// refusal: a guessed flow would let the studio's check pass on a number
/// nobody printed.
/// </summary>
public sealed class GcodeFlowRatioTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-gcode-flow-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private string StrWrite(params string[] aLines)
    {
        string strPath = Path.Combine(_strTempDir, $"{Guid.NewGuid():N}.gcode");
        File.WriteAllLines(strPath, aLines);
        return strPath;
    }

    [Fact]
    public void ReadsTheFilamentFlowRatioFromTheConfigBlock()
    {
        // The shape the K2's Creality Print / Orca output actually has: the
        // settings dump at the end, with other *_flow_ratio keys around it.
        string strPath = StrWrite(
            "; HEADER_BLOCK_START", "; total layer number: 50", "; HEADER_BLOCK_END",
            "G28", "G1 X1 Y1 E0.1",
            "; CONFIG_BLOCK_START",
            "; bottom_solid_infill_flow_ratio = 1",
            "; brim_flow_ratio = 1",
            "; filament_flow_ratio = 0.95",
            "; first_layer_flow_ratio = 1.05",
            "; CONFIG_BLOCK_END");
        Assert.Equal(0.95, GcodeFlowRatio.FRead(strPath), tolerance: 1e-12);
    }

    [Fact]
    public void AMultiFilamentListOfOneValueIsThatValue()
    {
        string strPath = StrWrite("; filament_flow_ratio = 0.98,0.98,0.98");
        Assert.Equal(0.98, GcodeFlowRatio.FRead(strPath), tolerance: 1e-12);
    }

    [Fact]
    public void DifferingFlowsAreRefusedAsAmbiguous()
    {
        string strPath = StrWrite("; filament_flow_ratio = 0.95,1");
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => GcodeFlowRatio.FRead(strPath));
        Assert.Contains("ambiguous", oEx.Message);
        Assert.Contains("0.95", oEx.Message);
    }

    [Fact]
    public void AGcodeWithoutAFlowRatioFailsRatherThanOmittingIt()
    {
        // Other flow keys must not be mistaken for the filament's.
        string strPath = StrWrite(
            "G28", "; first_layer_flow_ratio = 1", "; bottom_solid_infill_flow_ratio = 1");
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => GcodeFlowRatio.FRead(strPath));
        Assert.Contains("no filament_flow_ratio", oEx.Message);
    }

    [Theory]
    [InlineData("; filament_flow_ratio = ")]
    [InlineData("; filament_flow_ratio = abc")]
    [InlineData("; filament_flow_ratio = 0")]
    [InlineData("; filament_flow_ratio = -0.9")]
    public void AnUnreadableFlowIsRefused(string strLine)
    {
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => GcodeFlowRatio.FRead(StrWrite(strLine)));
        Assert.Contains("filament_flow_ratio", oEx.Message);
    }

    [Fact]
    public void AMissingFileIsRefusedByName()
    {
        string strPath = Path.Combine(_strTempDir, "nope.gcode");
        CompensationException oEx = Assert.Throws<CompensationException>(
            () => GcodeFlowRatio.FRead(strPath));
        Assert.Contains("does not exist", oEx.Message);
    }
}
