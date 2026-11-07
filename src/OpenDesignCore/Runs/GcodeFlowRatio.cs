using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenDesignCore.Runs;

/// <summary>
/// The flow ratio a part was actually printed at, read from the slicer's
/// settings dump in its G-code.
///
/// Why it matters (AdvancedStudio ADR-0004): a shrinkage figure measured on a
/// part sliced before the flow calibration is partly over- or under-extrusion,
/// filed under the material's name. The studio refuses a shrinkage proposal
/// whose declared printed flow differs from the profile's calibrated flow, but
/// it can only check a number it is sent. Reading it from the G-code rather
/// than asking for it on the command line means the figure is the one the
/// printer was given, not the one someone remembers setting.
///
/// OrcaSlicer and Creality Print write it as <c>; filament_flow_ratio = 0.95</c>
/// in the config block (at the end of the file on the K2, not the header), and
/// as a comma-separated list for multi-filament jobs.
/// </summary>
public static partial class GcodeFlowRatio
{
    [GeneratedRegex(@"^;\s*filament_flow_ratio\s*=\s*(?<v>.*?)\s*$")]
    private static partial Regex OLineRegex();

    /// <summary>
    /// The single flow ratio in <paramref name="strGcodePath"/>, or a refusal
    /// that says why there is not exactly one.
    /// </summary>
    public static double FRead(string strGcodePath)
    {
        if (!File.Exists(strGcodePath))
        {
            throw new CompensationException(
                $"G-code '{strGcodePath}' does not exist. --gcode names the file the measured "
                + "part was printed from, so its flow ratio can be checked against the profile.");
        }

        SortedSet<double> oValues = [];
        bool bFound = false;
        foreach (string strLine in File.ReadLines(strGcodePath))
        {
            Match oMatch = OLineRegex().Match(strLine);
            if (!oMatch.Success)
                continue;
            bFound = true;

            string[] aParts = oMatch.Groups["v"].Value.Split(
                [',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // An empty value is unreadable, not absent: the key was written.
            foreach (string strPart in aParts.Length == 0 ? [""] : aParts)
            {
                if (!double.TryParse(strPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double f)
                    || !double.IsFinite(f) || f <= 0)
                {
                    throw new CompensationException(
                        $"G-code '{strGcodePath}' has an unreadable filament_flow_ratio "
                        + $"('{oMatch.Groups["v"].Value}'). Refusing to guess the flow the part "
                        + "was printed at.");
                }
                oValues.Add(f);
            }
        }

        if (!bFound || oValues.Count == 0)
        {
            throw new CompensationException(
                $"G-code '{strGcodePath}' has no filament_flow_ratio line, so the flow the part "
                + "was printed at is unknown. The studio checks that a shrinkage was measured at "
                + "the profile's calibrated flow; without the figure it cannot. Re-slice with a "
                + "slicer that writes its settings into the G-code (OrcaSlicer, Creality Print), "
                + "or check that this is the file the part was printed from.");
        }
        if (oValues.Count > 1)
        {
            throw new CompensationException(
                $"G-code '{strGcodePath}' declares more than one filament_flow_ratio ("
                + string.Join(", ", oValues.Select(f => f.ToString("0.###", CultureInfo.InvariantCulture)))
                + "), so which one the measured part was printed at is ambiguous. Measure a part "
                + "printed from a single filament.");
        }
        return oValues.Min;
    }
}
