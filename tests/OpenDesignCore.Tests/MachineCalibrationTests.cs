using System.Text.Json;
using OpenDesignCore.Verification;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Machine registries on disk, in the shape OpenBuildCore writes them.
///
/// Built as JSON text and read back through the real parser rather than
/// constructed directly, because the thing under test is agreement with
/// another repo's file format. A hand-built record would pass whatever this
/// code happens to expect.
/// </summary>
public static class TestMachines
{
    public static string StrWrite(string strDir, string strJson)
    {
        Directory.CreateDirectory(strDir);
        string strPath = Path.Combine(strDir, "machines.json");
        File.WriteAllText(strPath, strJson);
        return strPath;
    }

    private static string StrAxis(double fResidualPct)
        => $$"""
        {"verified_on":"2026-08-17","residual_pct":{{fResidualPct}},
         "how_measured":"calibration-block/0.2, caliper 0.02 mm"}
        """;

    private const string c_strHead =
        "{\"schema\":\"obc/machines/0.1\",\"machines\":[{\"machine_id\":\"";
    private const string c_strBody =
        "\",\"make\":\"m\",\"model\":\"n\",\"process\":\"fff\","
        + "\"envelope_mm\":{\"x\":220,\"y\":220,\"z\":250},\"materials\":[\"pla\"],"
        + "\"axis_calibration\":";

    private static string StrRegistry(string strId, string strCalibration)
        => c_strHead + strId + c_strBody + strCalibration + "}]}";

    /// <summary>All three axes verified — the only state that permits a proposal.</summary>
    public static MachineCalibration OCalibrated(string strDir, double fWorstPct = 0.04)
        => MachineCalibration.ORead(StrWrite(strDir, StrRegistry("bench",
            "{\"x\":" + StrAxis(fWorstPct)
            + ",\"y\":" + StrAxis(0.01)
            + ",\"z\":" + StrAxis(0.02) + "}")), "bench");

    /// <summary>Nobody has measured this machine. Benji's K2, as of the first real print.</summary>
    public static MachineCalibration OUncalibrated(string strDir)
        => MachineCalibration.ORead(StrWrite(strDir, """
        {"schema":"obc/machines/0.1","machines":[
          {"machine_id":"k2","make":"Creality","model":"K2 Plus","process":"fff",
           "envelope_mm":{"x":350,"y":350,"z":350},"materials":["pla"],
           "axis_calibration":null}]}
        """), "k2");

    /// <summary>X and Z checked, Y skipped — which is where the fault actually was.</summary>
    public static MachineCalibration OPartial(string strDir)
        => MachineCalibration.ORead(StrWrite(strDir, StrRegistry("half",
            "{\"x\":" + StrAxis(0.03) + ",\"z\":" + StrAxis(0.02) + "}")), "half");
}

public sealed class MachineCalibrationTests : IDisposable
{
    private readonly string m_strDir = Path.Combine(
        Path.GetTempPath(), "odc-machinecal-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(m_strDir)) Directory.Delete(m_strDir, recursive: true);
    }

    [Fact]
    public void AnExplicitNullMeansUnknownRatherThanZero()
    {
        MachineCalibration oCal = TestMachines.OUncalibrated(m_strDir);
        Assert.Equal(MachineCalibrationState.Unknown, oCal.State);
        // Null, emphatically not 0.0. A zero residual is a claim that the
        // machine was measured and found perfect; this machine was never
        // measured at all, and collapsing the two is the whole failure mode
        // this type exists to prevent.
        Assert.Null(oCal.WorstResidualPct);
        Assert.Empty(oCal.VerifiedAxes);
    }

    [Fact]
    public void AnAbsentFieldAndAnExplicitNullAgree()
    {
        string strPath = TestMachines.StrWrite(m_strDir, """
        {"schema":"obc/machines/0.1","machines":[
          {"machine_id":"quiet","make":"m","model":"n","process":"fff",
           "envelope_mm":{"x":1,"y":1,"z":1},"materials":["pla"]}]}
        """);
        Assert.Equal(MachineCalibrationState.Unknown,
            MachineCalibration.ORead(strPath, "quiet").State);
    }

    [Fact]
    public void TwoAxesOutOfThreeIsPartialNotVerified()
    {
        // Tempting to call this "mostly calibrated" and let it through. A
        // part is measured on all three axes, and the untested one is exactly
        // where a fault would hide — as it did.
        MachineCalibration oCal = TestMachines.OPartial(m_strDir);
        Assert.Equal(MachineCalibrationState.Partial, oCal.State);
        Assert.Equal(["x", "z"], oCal.VerifiedAxes);
        Assert.Contains("not y", oCal.Reason);
    }

    [Fact]
    public void AllThreeAxesVerifiedReportsTheWorstResidual()
    {
        MachineCalibration oCal = TestMachines.OCalibrated(m_strDir, fWorstPct: 0.06);
        Assert.Equal(MachineCalibrationState.Verified, oCal.State);
        Assert.Equal(["x", "y", "z"], oCal.VerifiedAxes);
        // Worst, not mean: a good X does not excuse a bad Y.
        Assert.Equal(0.06, oCal.WorstResidualPct!.Value, tolerance: 1e-9);
    }

    [Fact]
    public void ANegativeResidualCountsByMagnitude()
    {
        string strPath = TestMachines.StrWrite(m_strDir, """
        {"schema":"obc/machines/0.1","machines":[
          {"machine_id":"neg","make":"m","model":"n","process":"fff",
           "envelope_mm":{"x":1,"y":1,"z":1},"materials":["pla"],
           "axis_calibration":{
             "x":{"verified_on":"2026-08-17","residual_pct":-0.4,"how_measured":"caliper"},
             "y":{"verified_on":"2026-08-17","residual_pct":0.1,"how_measured":"caliper"},
             "z":{"verified_on":"2026-08-17","residual_pct":0.1,"how_measured":"caliper"}}}]}
        """);
        // An axis 0.4 % short is as wrong as one 0.4 % long. Signed
        // comparison would have called this the best axis of the three.
        Assert.Equal(0.4, MachineCalibration.ORead(strPath, "neg").WorstResidualPct!.Value,
            tolerance: 1e-9);
    }

    [Fact]
    public void AMissingRegistryIsAnErrorNotAnUnknownMachine()
    {
        // Answering "uncalibrated" to a wrong path would disguise a typo as a
        // finding, and the finding would be the one that blocks the user.
        MachineCalibrationException oEx = Assert.Throws<MachineCalibrationException>(
            () => MachineCalibration.ORead(Path.Combine(m_strDir, "nope.json"), "k2"));
        Assert.Contains("No machine registry", oEx.Message);
    }

    [Fact]
    public void AnUnknownMachineIdListsTheOnesThatExist()
    {
        TestMachines.OUncalibrated(m_strDir);
        MachineCalibrationException oEx = Assert.Throws<MachineCalibrationException>(
            () => MachineCalibration.ORead(Path.Combine(m_strDir, "machines.json"), "k3"));
        Assert.Contains("Known: k2", oEx.Message);
    }

    [Fact]
    public void ACalibrationClaimWithNoReadableResidualIsRefused()
    {
        // OpenBuildCore's validator refuses this shape, so it should never
        // arrive. If it does, the registry was hand-edited past the
        // validator, and guessing would be worse than stopping.
        string strPath = TestMachines.StrWrite(m_strDir, """
        {"schema":"obc/machines/0.1","machines":[
          {"machine_id":"bad","make":"m","model":"n","process":"fff",
           "envelope_mm":{"x":1,"y":1,"z":1},"materials":["pla"],
           "axis_calibration":{"x":{"verified_on":"2026-08-17","how_measured":"caliper"}}}]}
        """);
        MachineCalibrationException oEx = Assert.Throws<MachineCalibrationException>(
            () => MachineCalibration.ORead(strPath, "bad"));
        Assert.Contains("no readable residual_pct", oEx.Message);
    }

    [Fact]
    public void TheShippedOpenBuildCoreRegistryStillParses()
    {
        // Cross-repo agreement is the only thing this type is for, so it is
        // read against the real neighbouring file rather than fixtures alone.
        // Skipped rather than failed when the sibling checkout is absent:
        // CI for this repo does not clone it.
        string strPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "OpenBuildCore", "example", "machines.json");
        if (!File.Exists(strPath)) return;

        using JsonDocument oDoc = JsonDocument.Parse(File.ReadAllBytes(strPath));
        foreach (JsonElement oMachine in oDoc.RootElement.GetProperty("machines").EnumerateArray())
        {
            string strId = oMachine.GetProperty("machine_id").GetString()!;
            MachineCalibration oCal = MachineCalibration.ORead(strPath, strId);
            Assert.Equal(strId, oCal.MachineId);
            Assert.NotEmpty(oCal.Reason);
        }
    }
}
