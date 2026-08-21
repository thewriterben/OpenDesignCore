using System.Globalization;
using System.Text.Json;

namespace OpenDesignCore.Verification;

public sealed class MachineCalibrationException(string strMessage) : Exception(strMessage);

/// <summary>
/// How thoroughly a machine's axes have been verified against a known length.
/// </summary>
public enum MachineCalibrationState
{
    /// <summary>Nobody has measured this machine. Not "bad" — unknown.</summary>
    Unknown,

    /// <summary>Some axes verified, others not. A part is measured on all three.</summary>
    Partial,

    /// <summary>X, Y and Z each verified, each with a recorded residual and method.</summary>
    Verified,
}

/// <summary>
/// A machine's axis-calibration record, read from an OpenBuildCore machine
/// registry.
///
/// <para>Why OpenDesignCore consults this at all: a printed part's dimensions
/// are the product of the design, the material, and the machine's idea of how
/// far a millimetre is. A measurement cannot tell those apart. If the machine
/// is out, the error lands in whatever the measurement is filed under — and
/// the thing it gets filed under is a material shrinkage figure that will
/// shape every future print in that material.</para>
///
/// <para>This was not hypothetical. The first real print through this loop
/// showed X at -0.25 % and Y at +0.83 %, which no material does; the Y axis
/// was mechanically short. Writing "PLA shrinks 0.29 %" from that average
/// would have baked a machine fault into a material record, permanently, with
/// a provenance hash making it look sourced.</para>
///
/// <para>OpenBuildCore owns machines; this type only reads its registry, the
/// mirror of OpenBuildCore reading this engine's provenance sidecars.</para>
/// </summary>
public sealed record MachineCalibration
{
    public required string MachineId { get; init; }
    public required MachineCalibrationState State { get; init; }

    /// <summary>Axes with a complete calibration record, sorted.</summary>
    public required IReadOnlyList<string> VerifiedAxes { get; init; }

    /// <summary>
    /// The largest absolute residual across verified axes, or null when
    /// nothing is verified. Null is not zero and must never be read as zero.
    /// </summary>
    public required double? WorstResidualPct { get; init; }

    /// <summary>Human-readable statement of the state, for refusals and logs.</summary>
    public required string Reason { get; init; }

    /// <summary>The axes any part is measured on, and so the axes that must be verified.</summary>
    private static readonly string[] s_aRequiredAxes = ["x", "y", "z"];

    /// <summary>
    /// Read one machine's calibration from an OpenBuildCore machines.json.
    ///
    /// A missing file or an unknown id is an error, not an Unknown state:
    /// those mean the caller pointed at the wrong thing, and answering
    /// "uncalibrated" would disguise a typo as a finding.
    /// </summary>
    public static MachineCalibration ORead(string strMachinesPath, string strMachineId)
    {
        if (!File.Exists(strMachinesPath))
        {
            throw new MachineCalibrationException(
                $"No machine registry at {strMachinesPath}. Point --machines at an "
                + "OpenBuildCore machines.json; the calibration state of the machine that "
                + "printed the part is not something this tool can assume.");
        }

        using JsonDocument oDoc = JsonDocument.Parse(File.ReadAllBytes(strMachinesPath));
        if (!oDoc.RootElement.TryGetProperty("machines", out JsonElement oMachines))
        {
            throw new MachineCalibrationException(
                $"{strMachinesPath} has no 'machines' array — this does not look like an "
                + "OpenBuildCore machine registry.");
        }

        foreach (JsonElement oMachine in oMachines.EnumerateArray())
        {
            if (oMachine.TryGetProperty("machine_id", out JsonElement oId)
                && oId.GetString() == strMachineId)
            {
                return OFrom(strMachineId, oMachine);
            }
        }

        List<string> aKnown = [];
        foreach (JsonElement oMachine in oMachines.EnumerateArray())
        {
            if (oMachine.TryGetProperty("machine_id", out JsonElement oId))
                aKnown.Add(oId.GetString() ?? "");
        }

        throw new MachineCalibrationException(
            $"No machine '{strMachineId}' in {strMachinesPath}. Known: "
            + (aKnown.Count > 0 ? string.Join(", ", aKnown) : "none"));
    }

    private static MachineCalibration OFrom(string strMachineId, JsonElement oMachine)
    {
        // Absent and explicit null mean the same thing and both mean unknown.
        // OpenBuildCore's schema allows null precisely so a machine can carry
        // a note about *why* it is uncalibrated without claiming it is.
        if (!oMachine.TryGetProperty("axis_calibration", out JsonElement oCal)
            || oCal.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new MachineCalibration
            {
                MachineId = strMachineId,
                State = MachineCalibrationState.Unknown,
                VerifiedAxes = [],
                WorstResidualPct = null,
                Reason = $"machine '{strMachineId}' has no recorded axis calibration",
            };
        }

        List<string> aVerified = [];
        double fWorst = 0;
        foreach (JsonProperty oAxis in oCal.EnumerateObject())
        {
            aVerified.Add(oAxis.Name.ToLowerInvariant());
            // OpenBuildCore's validator has already refused a half-made claim
            // — a date without a residual, a residual without a method — so a
            // record that is present here is a record that is complete.
            double fResidual = Math.Abs(FResidual(oAxis.Value, strMachineId, oAxis.Name));
            if (fResidual > fWorst) fWorst = fResidual;
        }
        aVerified.Sort(StringComparer.Ordinal);

        List<string> aMissing = [.. s_aRequiredAxes.Where(a => !aVerified.Contains(a))];
        if (aMissing.Count > 0)
        {
            return new MachineCalibration
            {
                MachineId = strMachineId,
                State = MachineCalibrationState.Partial,
                VerifiedAxes = aVerified,
                WorstResidualPct = aVerified.Count > 0 ? fWorst : null,
                Reason = $"machine '{strMachineId}' has {string.Join(", ", aVerified)} verified "
                         + $"but not {string.Join(", ", aMissing)}",
            };
        }

        return new MachineCalibration
        {
            MachineId = strMachineId,
            State = MachineCalibrationState.Verified,
            VerifiedAxes = aVerified,
            WorstResidualPct = fWorst,
            Reason = $"machine '{strMachineId}' has x, y and z verified, worst residual "
                     + fWorst.ToString("F3", CultureInfo.InvariantCulture) + " %",
        };
    }

    private static double FResidual(JsonElement oAxis, string strMachineId, string strAxisName)
    {
        if (!oAxis.TryGetProperty("residual_pct", out JsonElement oResidual)
            || !oResidual.TryGetDouble(out double fResidual))
        {
            throw new MachineCalibrationException(
                $"Machine '{strMachineId}' axis '{strAxisName}' claims a calibration with no "
                + "readable residual_pct. Claiming a calibration is claiming a measurement "
                + "happened, and a measurement has a result. Run OpenBuildCore's validator "
                + "over this registry.");
        }
        return fResidual;
    }
}
