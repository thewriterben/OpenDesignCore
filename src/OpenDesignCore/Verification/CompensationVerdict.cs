namespace OpenDesignCore.Verification;

/// <summary>Why a measured deviation may or may not become a slicer setting.</summary>
public enum ECompensationVerdict
{
    /// <summary>A single XY factor is defensible from this measurement.</summary>
    Proposed,

    /// <summary>
    /// The deviation is inside what the scanner can resolve. There is nothing
    /// to compensate; a setting derived from it would be compensating for
    /// instrument noise.
    /// </summary>
    WithinScannerNoise,

    /// <summary>
    /// No scanner accuracy was declared, so signal cannot be separated from
    /// instrument. Unknown, never treated as "small enough to ignore".
    /// </summary>
    AccuracyUndeclared,

    /// <summary>
    /// X and Y disagree by more than the caller's declared threshold. Orca's
    /// Shrinkage (XY) is a single number and cannot express that; emitting a
    /// mean would be silently wrong on both axes.
    /// </summary>
    AxesDisagree,

    /// <summary>
    /// One of X or Y deviated by no more than the instrument's accuracy, so
    /// that axis has no measured deviation at all — only the other one does.
    ///
    /// Averaging a real reading with a non-reading manufactures a number that
    /// describes neither. Found on the first real print: a 0.02 mm caliper
    /// reported X off by 0.050 mm (2.5× the accuracy, real) and Y off by
    /// 0.020 mm (1.0×, indistinguishable from zero), and the tool cheerfully
    /// averaged them.
    /// </summary>
    AxisNotSignificant,
}

/// <summary>
/// The bridge between a measurement and a slicer setting — and, mostly, the
/// decision not to cross it.
///
/// The arithmetic that turns a nominal/measured pair into an OrcaSlicer
/// shrinkage percentage lives in AdvancedStudio (`calibration/calculators.py`),
/// where it is already calibrated against the process research and where
/// slicer semantics belong. Duplicating it here would give the platform two
/// implementations of one formula, free to drift.
///
/// What belongs *here* is the question only the measurement can answer:
/// whether a single compensation factor is a defensible reading of it. That is
/// a property of the data, not of the slicer.
///
/// Three refusals, each a real failure mode rather than defensive padding:
///
///  * <b>Within scanner noise.</b> A 30 mm part measured 0.003 mm small on a
///    scanner good to 0.05 mm has not shrunk; the scanner has wobbled.
///  * <b>Accuracy undeclared.</b> Without a stated instrument accuracy there is
///    no basis to call a deviation real. Unknown is recorded as unknown — the
///    same rule that governs units and voxel size.
///  * <b>Axes disagree.</b> The whole reason the comparison reports a spread.
///    A part that shrank 0.2% in X and 1.4% in Y has no single correct XY
///    factor, and the mean is wrong on both axes at once.
///
/// Z is never folded into the XY figure. Orca's Shrinkage (XY) applies to X
/// and Y only; Z shrinkage is a different setting with different causes (layer
/// squish, first-layer offset), and averaging three axes into one number would
/// be silently wrong in a way nobody would notice.
/// </summary>
public sealed record CompensationProposal
{
    public required ECompensationVerdict Verdict { get; init; }
    public required string Reason { get; init; }

    /// <summary>Design (nominal) X and Y, mm — the pair a calculator needs.</summary>
    public required double NominalXMm { get; init; }
    public required double NominalYMm { get; init; }
    public required double MeasuredXMm { get; init; }
    public required double MeasuredYMm { get; init; }

    /// <summary>
    /// Mean of the X and Y nominals and measurements, mm. This is the pair to
    /// hand a shrinkage calculator, and it is only meaningful when the verdict
    /// is <see cref="ECompensationVerdict.Proposed"/> — which is exactly when
    /// the two axes were found to agree.
    /// </summary>
    public double NominalXyMm => (NominalXMm + NominalYMm) / 2.0;
    public double MeasuredXyMm => (MeasuredXMm + MeasuredYMm) / 2.0;

    /// <summary>Observed disagreement between X and Y, percentage points.</summary>
    public required double AxisSpreadPct { get; init; }

    /// <summary>The caller's declared limit. Never defaulted in code.</summary>
    public required double MaxAxisSpreadPct { get; init; }

    /// <summary>
    /// Z deviation, reported alongside and never merged into the XY figure.
    /// Null when the comparison carried no z axis.
    /// </summary>
    public required double? ZDeviationPct { get; init; }

    public bool Actionable => Verdict == ECompensationVerdict.Proposed;

    public static CompensationProposal OJudge(
        DimensionalReport oReport, double fMaxAxisSpreadPct)
    {
        if (fMaxAxisSpreadPct <= 0)
        {
            throw new ArgumentException(
                "Max axis spread must be declared and positive. There is no defensible "
                + "default: how much X/Y disagreement still permits one factor is a "
                + "process judgement, not a constant (see the tolerance rule in CLAUDE.md).");
        }

        AxisDeviation oX = oReport.Axes.Single(a => a.Axis == "x");
        AxisDeviation oY = oReport.Axes.Single(a => a.Axis == "y");
        // "z-span" when the caliper measured between two printed top faces, so
        // the first-layer offset has cancelled; plain "z" when it is a single
        // bed-referenced height and therefore still contains it.
        double? fZ = oReport.Axes
            .SingleOrDefault(a => a.Axis is "z" or "z-span")?.DeviationPct;
        double fSpread = Math.Abs(oX.DeviationPct - oY.DeviationPct);

        CompensationProposal OWith(ECompensationVerdict eVerdict, string strReason) => new()
        {
            Verdict = eVerdict,
            Reason = strReason,
            NominalXMm = oX.DesignMm,
            NominalYMm = oY.DesignMm,
            MeasuredXMm = oX.ScanMm,
            MeasuredYMm = oY.ScanMm,
            AxisSpreadPct = fSpread,
            MaxAxisSpreadPct = fMaxAxisSpreadPct,
            ZDeviationPct = fZ,
        };

        if (!oReport.AccuracyDeclared)
        {
            return OWith(ECompensationVerdict.AccuracyUndeclared,
                "No scanner accuracy was declared for this comparison, so a deviation "
                + "cannot be distinguished from instrument error. Rerun `compare` with "
                + "--scan-accuracy-mm. Refusing rather than assuming the measurement is "
                + "good enough to act on.");
        }

        if (oReport.WithinScanAccuracy == true)
        {
            return OWith(ECompensationVerdict.WithinScannerNoise,
                $"Largest deviation {oReport.MaxAbsDeviationMm:F3} mm is within the declared "
                + $"scanner accuracy of {oReport.ScanAccuracyMm:F3} mm. There is nothing here "
                + "to compensate: a setting derived from this would be compensating for the "
                + "instrument, not the print.");
        }

        // Per-axis significance, checked before the axes are compared to each
        // other. WithinScanAccuracy above tests only the LARGEST deviation, so
        // it passes as soon as *one* axis is real — which is exactly how a
        // real reading got averaged with a non-reading on the first print.
        double fAxisNoise = oReport.ScanAccuracyMm;
        double fXAbs = Math.Abs(oX.DeviationMm);
        double fYAbs = Math.Abs(oY.DeviationMm);
        if (fXAbs <= fAxisNoise || fYAbs <= fAxisNoise)
        {
            string strWeak = fXAbs <= fAxisNoise ? "X" : "Y";
            double fWeak = fXAbs <= fAxisNoise ? fXAbs : fYAbs;
            double fStrong = fXAbs <= fAxisNoise ? fYAbs : fXAbs;
            return OWith(ECompensationVerdict.AxisNotSignificant,
                $"{strWeak} deviated {fWeak:F3} mm, which is within the instrument's "
                + $"{fAxisNoise:F3} mm accuracy — that axis has no measured deviation at all. "
                + $"The other axis moved {fStrong:F3} mm and is real. Averaging a reading with "
                + "a non-reading produces a number describing neither. Either the part is only "
                + "deviating on one axis (which is not material shrinkage and a single XY "
                + "factor cannot fix), or the deviation is too small for this instrument to "
                + "resolve — print a larger block so the same shrinkage shows as more "
                + "millimetres.");
        }

        // The spread is a difference of two percentages, each carrying the
        // instrument's error divided by a DIFFERENT edge length. On a short
        // edge that error is a larger fraction, so the spread of a perfectly
        // isotropic part is not zero — it has a noise floor, and comparing a
        // declared threshold against a spread without knowing that floor
        // invites a confident answer to an unanswerable question.
        double fSpreadNoisePct =
            100.0 * fAxisNoise / oX.DesignMm + 100.0 * fAxisNoise / oY.DesignMm;

        if (fSpread > fMaxAxisSpreadPct)
        {
            return OWith(ECompensationVerdict.AxesDisagree,
                $"X deviated {oX.DeviationPct:F3}% and Y deviated {oY.DeviationPct:F3}%, a spread "
                + $"of {fSpread:F3} points against the declared limit of {fMaxAxisSpreadPct:F3}. "
                + "A single XY shrinkage factor cannot express that, and their mean would be "
                + "wrong on both axes. "
                + (fSpread <= fSpreadNoisePct
                    ? $"Note that {fSpreadNoisePct:F3} points of that spread is attributable to "
                      + "the instrument alone, because the same absolute error is a larger "
                      + "fraction of the shorter edge — so this disagreement may not be real. "
                      + "A larger block would settle it."
                    : "Compensate per-axis in the slicer, or find out why the axes differ — a "
                      + $"spread this size (against a {fSpreadNoisePct:F3}-point instrument "
                      + "floor) is usually a mechanical or orientation problem rather than "
                      + "material shrinkage."));
        }

        return OWith(ECompensationVerdict.Proposed,
            $"X and Y agree within {fSpread:F3} points (limit {fMaxAxisSpreadPct:F3}) and the "
            + $"deviation exceeds the declared scanner accuracy of {oReport.ScanAccuracyMm:F3} mm, "
            + "so one XY factor is a defensible reading of this measurement.");
    }
}
