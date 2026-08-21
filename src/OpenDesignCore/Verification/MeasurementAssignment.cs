using System.Globalization;

namespace OpenDesignCore.Verification;

/// <summary>One caliper reading and the design dimension it is claimed to be.</summary>
public readonly record struct Reading(string StrLabel, double FNominalMm, double FMeasuredMm);

/// <summary>
/// Refuses a set of readings whose labels cannot be the labels they were given.
///
/// <para>The calibration block's axes are deliberately unequal — 40 × 60, with
/// a shelf at 4 mm under a face at 25 mm — and the model refuses to generate
/// one with equal X and Y. The stated reason was that a transposed reading
/// would then be *detectable*. Nothing detected it. A swapped pair went
/// straight through as a 50 % deviation, got compared, recorded, and offered
/// as a compensation.</para>
///
/// <para>The Z pair is the dangerous one in practice. X and Y are read off two
/// obviously different faces; "bed to the shelf" and "bed to the tall face"
/// are the same gesture twice, and the arguments are adjacent on the command
/// line.</para>
///
/// <para>The test needs no tolerance, which is the point. It asks whether each
/// reading is nearer its own nominal than any other nominal — a question with
/// an exact answer. A real deviation is fractions of a percent; a transposition
/// is tens of percent. Nothing legitimate lands in between, so nothing has to
/// be declared about where the line sits.</para>
///
/// <para>If a design ever does have two nominals close enough for this to fire
/// on an honest reading, that is not a false positive: it means the readings
/// genuinely cannot be told apart, and recording one as though it could is the
/// error. Refusing is still right.</para>
/// </summary>
public static class MeasurementAssignment
{
    /// <summary>
    /// Throw if any reading is closer to a different axis's nominal than to
    /// its own. Names both ends of the swap; a message that only said
    /// "implausible" would leave the reader to work out which two.
    /// </summary>
    public static void Assert(IReadOnlyList<Reading> aReadings)
    {
        foreach (Reading oReading in aReadings)
        {
            double fOwn = Math.Abs(oReading.FMeasuredMm - oReading.FNominalMm);

            // The NEAREST other nominal, not merely the first one that beats
            // its own. An earlier version returned on the first hit and told a
            // reader who had swapped Z-low with Z-high to "check Z-low against
            // X" — true, in that 25.02 mm is nearer the 40 mm nominal than the
            // 4 mm one, and useless, because it is nearer the 25 mm nominal
            // than either. A refusal that misdirects is barely better than no
            // refusal; someone would go and re-measure the wrong face.
            Reading? oNearest = null;
            double fNearest = double.MaxValue;

            foreach (Reading oOther in aReadings)
            {
                if (oOther.StrLabel == oReading.StrLabel)
                    continue;

                double fOther = Math.Abs(oReading.FMeasuredMm - oOther.FNominalMm);
                if (fOther < fNearest)
                {
                    fNearest = fOther;
                    oNearest = oOther;
                }
            }

            // Strictly greater: an exact tie is refused too. A reading
            // equidistant between two nominals is the most ambiguous value
            // there is, and letting it through on a technicality would
            // contradict the rule above about ambiguity. Found by a test whose
            // own comment claimed 14.5 was "nearer 25" when it is exactly
            // halfway between 4 and 25 — the fixture was wrong and the check
            // was too.
            //
            // This cannot fire on a reading that is on its own nominal: that
            // gives fOwn = 0, and no other distance can be smaller.
            if (oNearest is not Reading oOtherEnd || fNearest > fOwn)
                continue;

            throw new ArgumentException(
                $"The {oReading.StrLabel} reading of {StrMm(oReading.FMeasuredMm)} is nearer "
                + $"the {oOtherEnd.StrLabel} nominal ({StrMm(oOtherEnd.FNominalMm)}) than its own "
                + $"({StrMm(oReading.FNominalMm)}), so it cannot be a {oReading.StrLabel} "
                + "measurement of this part. Almost certainly two readings are the wrong way "
                + $"round — check {oReading.StrLabel} against {oOtherEnd.StrLabel}. "
                + "Order is X, Y, Z-low, Z-high, where Z-low is bed to the top of the SHELF "
                + "and Z-high is bed to the top of the TALL face. "
                + "Nothing is recorded; re-run with the readings in that order.");
        }
    }

    private static string StrMm(double fValue)
        => fValue.ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
