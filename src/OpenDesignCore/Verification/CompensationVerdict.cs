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

    /// <summary>
    /// One axis deviates while the others are clean. That is the signature of
    /// a machine scale error — steps/mm, belt tension, a slipping pulley — and
    /// it is not compensable in the slicer at all.
    ///
    /// Material shrinkage is a property of the polymer, so it acts on every
    /// axis at once and in the same direction. A single axis out on its own
    /// is the machine, and applying a shrinkage percentage to it would
    /// distort the two good axes to disguise the bad one.
    ///
    /// Found on a real print, deliberately run before calibrating: X −0.25%,
    /// Z +0.10%, Y +0.83%. Textbook PLA on two axes and a fault on the third.
    /// </summary>
    MachineScaleError,
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

        double fAxisNoise = oReport.ScanAccuracyMm;
        double fXAbs = Math.Abs(oX.DeviationMm);
        double fYAbs = Math.Abs(oY.DeviationMm);
        bool bXQuiet = fXAbs <= fAxisNoise;
        bool bYQuiet = fYAbs <= fAxisNoise;

        // Scoped to X and Y, deliberately.
        //
        // This used to read oReport.WithinScanAccuracy, which is computed over
        // EVERY axis including Z. A part measuring dead on nominal in X and Y
        // with a 0.100 mm z-span deviation therefore failed the test — Z's
        // deviation disqualified an XY verdict, in a file whose stated rule is
        // that Z is never folded into XY. The judgement about whether an XY
        // shrinkage factor is warranted can only be about X and Y.
        //
        // Found on a real print: X 40.000/40.000, Y 60.000/60.000, z-span
        // -0.48 %. The correct answer is "nothing to compensate in XY"; what
        // came back was AxisNotSignificant.
        if (bXQuiet && bYQuiet)
        {
            return OWith(ECompensationVerdict.WithinScannerNoise,
                $"X deviated {fXAbs:F3} mm and Y {fYAbs:F3} mm, both within the declared "
                + $"instrument accuracy of {fAxisNoise:F3} mm. There is nothing here to "
                + "compensate: a setting derived from this would be compensating for the "
                + "instrument, not the print. "
                + (fZ is double fZq
                    ? $"Z deviated {fZq:F3} % and is reported separately — it has its own "
                      + "causes and no XY shrinkage factor addresses it."
                    : "")
                + " If you expected a shrinkage figure, the deviation is smaller than this "
                + "caliper can resolve; a larger block shows the same percentage as more "
                + "millimetres.");
        }

        // Exactly one axis quiet. XOR, not OR: the message below asserts that
        // the other axis "is real", and under the old OR that sentence was
        // emitted even when both axes were silent, producing the self-refuting
        // "The other axis moved 0.000 mm and is real".
        if (bXQuiet ^ bYQuiet)
        {
            string strWeak = bXQuiet ? "X" : "Y";
            double fWeak = bXQuiet ? fXAbs : fYAbs;
            double fStrong = bXQuiet ? fYAbs : fXAbs;
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

        // Machine fault before axis disagreement. Both present as an X/Y
        // spread, but only one of them has a slicer remedy, and sending
        // someone to the slicer for a belt problem wastes a print and hides
        // the fault behind a plausible number.
        if (fSpread > fMaxAxisSpreadPct)
        {
            (string strAxis, double fPct)? oOutlier = OFindLoneOutlier(
                oX.DeviationPct, oY.DeviationPct,
                100.0 * fAxisNoise / Math.Min(oX.DesignMm, oY.DesignMm));

            if (oOutlier is (string strBad, double fBadPct))
            {
                string strUp = strBad.ToUpperInvariant();
                double fCorrection = 1.0 / (1.0 + (fBadPct / 100.0));
                return OWith(ECompensationVerdict.MachineScaleError,
                    $"X deviated {oX.DeviationPct:F3}% and Y deviated {oY.DeviationPct:F3}% — "
                    + "opposite directions. No material or flow effect does that: shrinkage "
                    + "pulls both in-plane axes in, over-extrusion pushes both out. One axis "
                    + "shrinking while the other grows leaves geometry as the explanation — "
                    + $"steps/mm, belt tension, a slipping pulley on {strUp}. {strUp} is named "
                    + "because a cooling polymer cannot make a part larger than its design, so "
                    + "the axis that GREW is the one that is not behaving like material. "
                    + "This is not compensable in the slicer at all; a shrinkage percentage "
                    + "here would distort the good axis to disguise the bad one. To cancel "
                    + $"it, {strUp} would need scaling by {fCorrection:F5}. "
                    // BUG, tracked in examples/calibration-loop/MACHINE-CALIBRATION.md:
                    // this used to end by telling the reader to multiply that axis's
                    // rotation_distance. That is right on a Cartesian machine and harmful
                    // on a CoreXY, where both motors move for every move: matched
                    // rotation distances scale X and Y together, and mismatched ones
                    // produce skew rather than independent scale. So the advice would have
                    // introduced a real geometric fault into a machine whose actual
                    // problem was still unidentified.
                    //
                    // Fixing it properly needs a kinematics field on OpenBuildCore's
                    // machine schema — machine facts belong in the machine registry — and
                    // this verdict reading it. Until then the verdict states the
                    // observation and stops, because a remedy it cannot justify is worse
                    // than no remedy.
                    + "HOW to apply that depends on the kinematics and this tool does not "
                    + "know yours: on a Cartesian machine it is that axis's rotation_distance, "
                    + "but on a CoreXY X and Y cannot be scaled independently at all and "
                    + "changing one stepper produces skew instead. Rule out the measurement "
                    + "first — half a millimetre on one pair of faces is as easily a seam or "
                    + "a caliper angle as a machine fault, and an uncalibrated flow ratio "
                    + "moves dimensions too. See examples/calibration-loop/CALIBRATE-FIRST.md "
                    + "before changing anything, then print and measure again.");
            }

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

    /// <summary>
    /// Whether the in-plane axes disagree in a way no material effect explains.
    ///
    /// The first attempt at this was a statistical outlier test across all
    /// three axes: find the one that disagrees with the other two. It never
    /// fired on the print that motivated it, and the reason was better than
    /// the test — <b>X and Z are not supposed to agree</b>. In-plane shrinkage
    /// is material contraction; Z is dominated by layer-height control, which
    /// is a mechanism rather than a polymer. Pooling them was wrong physics
    /// dressed up as statistics.
    ///
    /// The real signal is a sign disagreement between X and Y. Every material
    /// and flow effect acts on both in-plane axes in the <i>same direction</i>:
    /// shrinkage pulls both in, over-extrusion pushes both out. One axis
    /// shrinking while the other grows cannot be explained by the material at
    /// all, so what is left is geometry — steps/mm, belt tension, a slipping
    /// pulley.
    ///
    /// The suspect named is the axis that <b>grew</b>, because a cooling
    /// polymer cannot make a part larger than the design. That is a physical
    /// argument rather than a statistical one, which is why it is worth
    /// asserting a specific axis instead of shrugging at both.
    ///
    /// Returns null when the signs agree: two axes shrinking by different
    /// amounts is a real disagreement, but it has several possible causes and
    /// naming one would send someone to adjust a belt that was never the
    /// problem.
    /// </summary>
    private static (string strAxis, double fPct)? OFindLoneOutlier(
        double fXPct, double fYPct, double fNoisePct)
    {
        // Both must be real deviations, not instrument noise sitting near zero
        // where a sign is meaningless.
        if (Math.Abs(fXPct) <= fNoisePct || Math.Abs(fYPct) <= fNoisePct)
            return null;

        if (Math.Sign(fXPct) == Math.Sign(fYPct))
            return null;

        return fXPct > 0 ? ("x", fXPct) : ("y", fYPct);
    }
}
