namespace OpenDesignCore.Import;

/// <summary>
/// How a mesh came to exist, declared by the caller and never inferred (ADR-0019).
///
/// This exists because units are not the same question as scale. `--units mm`
/// says how to read the numbers in the file; it says nothing about whether those
/// numbers were ever tied to a physical size. For a CAD export they always were.
/// For a photogrammetric reconstruction they were not: structure-from-motion
/// recovers shape only up to an unknown similarity transform, so absolute size
/// enters solely from a reference in the scene.
/// </summary>
public enum EScanOrigin
{
    /// <summary>
    /// Exported from a CAD/EDA system whose units are authoritative
    /// (e.g. `kicad-cli pcb export stl`). Scale is not in question.
    /// </summary>
    CadExport,

    /// <summary>
    /// Produced by an instrument that establishes scale itself — structured
    /// light, laser, CT. A scale reference may be recorded but is not required.
    /// </summary>
    MetrologyScan,

    /// <summary>
    /// Photogrammetric reconstruction. Scale-free by construction, so a scale
    /// reference is REQUIRED: without one the mesh has no absolute size and a
    /// declared unit means less than it looks like.
    /// </summary>
    Photogrammetry,
}

/// <summary>
/// What tied a scale-free reconstruction to physical size: a description of the
/// reference and the length it was known or measured to be.
/// </summary>
public sealed record ScaleReference
{
    /// <summary>
    /// What was used and how it was read — e.g. "10 mm gauge block across the
    /// turntable, digital caliper". Free text on purpose; see the note in
    /// <see cref="Validate"/> on why this is not length-checked.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>The reference's known or measured length, mm. Must be positive.</summary>
    public required double LengthMm { get; init; }

    /// <summary>
    /// What that same reference spans in the mesh file's own coordinates,
    /// before units and any scaling are applied. Required for photogrammetry
    /// (ADR-0020), where it and <see cref="LengthMm"/> together *determine* the
    /// scale rather than merely describing it. Null where the scale came from
    /// somewhere else and this reference is corroboration.
    /// </summary>
    public double? SpanFileUnits { get; init; }

    /// <summary>
    /// The total file→mm scale this reference implies. Not a check against a
    /// separately declared scale — the declared scale is gone (ADR-0020), and
    /// this is the only source.
    /// </summary>
    public double FTotalScale =>
        SpanFileUnits is { } fSpan && fSpan > 0
            ? LengthMm / fSpan
            : throw new ImportValidationException(
                "This scale reference carries no measured span, so it implies no scale.");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Description))
            throw new ImportValidationException(
                "A scale reference must say what was measured; its description is empty.");

        // Deliberately NOT a minimum length or a banned-word list. A threshold
        // would be a guess, and a required field that can be satisfied by any
        // filler string is the failure ClawBot's ADR-0026 names for a
        // `how_determined` that states nothing. The real gate is below: a
        // positive measured length cannot be produced by typing a word.
        if (!(LengthMm > 0))
            throw new ImportValidationException(
                $"A scale reference must carry a positive measured length; got {LengthMm} mm.");
        if (double.IsNaN(LengthMm) || double.IsInfinity(LengthMm))
            throw new ImportValidationException("Scale reference length must be finite.");

        if (SpanFileUnits is { } fSpan)
        {
            if (!(fSpan > 0))
                throw new ImportValidationException(
                    $"A scale reference's measured span must be positive; got {fSpan} file units.");
            if (double.IsNaN(fSpan) || double.IsInfinity(fSpan))
                throw new ImportValidationException("Scale reference span must be finite.");
        }
    }

    /// <summary>
    /// Parse the CLI/MCP surface form `&lt;length-mm&gt;:&lt;description&gt;`,
    /// e.g. `10.0:gauge block across the turntable, digital caliper`. The
    /// measured span arrives separately (`--scale-ref-span`), because it is
    /// required in one case and meaningless in another.
    /// </summary>
    public static ScaleReference OParse(string strValue, double? fSpanFileUnits = null)
    {
        int nSplit = strValue.IndexOf(':');
        if (nSplit <= 0)
            throw new ImportValidationException(
                "A scale reference is written '<length-mm>:<what was measured, and how>', " +
                $"e.g. '10.0:gauge block across the turntable, digital caliper'. Got '{strValue}'.");

        if (!double.TryParse(
                strValue[..nSplit],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double fLengthMm))
        {
            throw new ImportValidationException(
                $"'{strValue[..nSplit]}' is not a length in mm.");
        }

        ScaleReference oRef = new()
        {
            Description = strValue[(nSplit + 1)..].Trim(),
            LengthMm = fLengthMm,
            SpanFileUnits = fSpanFileUnits,
        };
        oRef.Validate();
        return oRef;
    }
}

public static class ScanProvenance
{
    /// <summary>
    /// The rule, in one place so the CLI and the MCP surface cannot diverge:
    /// photogrammetry requires a scale reference; a CAD export refuses one.
    /// </summary>
    public static void Validate(EScanOrigin eOrigin, ScaleReference? oScaleRef)
    {
        oScaleRef?.Validate();

        switch (eOrigin)
        {
            case EScanOrigin.Photogrammetry when oScaleRef is null:
                throw new ImportValidationException(
                    "Photogrammetry is scale-free by construction: structure-from-motion recovers " +
                    "shape only up to an unknown similarity transform, so declared units alone do " +
                    "not give this mesh an absolute size. Pass a scale reference " +
                    "('<length-mm>:<what was measured, and how>'), or declare a different origin " +
                    "if the scale did not come from photogrammetry. Absence is UNKNOWN, not 1:1.");

            case EScanOrigin.Photogrammetry when oScaleRef.SpanFileUnits is null:
                throw new ImportValidationException(
                    "A photogrammetry scale reference must also say what it spans in the mesh " +
                    "file's own coordinates (--scale-ref-span), because that span and the known " +
                    "length are what *determine* this mesh's scale — they are not a description " +
                    "of it (ADR-0020). Without the span the reference asserts a size nothing in " +
                    "the pipeline ever uses, which reads as rigour and is not.");

            case not EScanOrigin.Photogrammetry when oScaleRef?.SpanFileUnits is not null:
                throw new ImportValidationException(
                    $"A measured span is only meaningful for photogrammetry, where it sets the " +
                    $"scale. For {StrOrigin(eOrigin)} the scale came from elsewhere, so a span " +
                    "here would name a second source for one quantity. Drop it, or declare " +
                    "photogrammetry if that is what this mesh is.");

            case EScanOrigin.CadExport when oScaleRef is not null:
                throw new ImportValidationException(
                    "A CAD export's units are authoritative, so a scale reference has nothing to " +
                    "establish and recording one would assert a measurement that was never made. " +
                    "Drop it, or declare the origin the mesh actually has.");
        }
    }

    public static bool BTryParseOrigin(string? strValue, out EScanOrigin eOrigin)
    {
        eOrigin = default;
        return strValue switch
        {
            "cad-export" => Set(EScanOrigin.CadExport, ref eOrigin),
            "metrology-scan" => Set(EScanOrigin.MetrologyScan, ref eOrigin),
            "photogrammetry" => Set(EScanOrigin.Photogrammetry, ref eOrigin),
            _ => false,
        };

        static bool Set(EScanOrigin eValue, ref EScanOrigin eTarget)
        {
            eTarget = eValue;
            return true;
        }
    }

    public static string StrOrigin(EScanOrigin eOrigin) => eOrigin switch
    {
        EScanOrigin.CadExport => "cad-export",
        EScanOrigin.MetrologyScan => "metrology-scan",
        EScanOrigin.Photogrammetry => "photogrammetry",
        _ => throw new ImportValidationException($"Unhandled scan origin {eOrigin}."),
    };

    public const string StrAccepted = "cad-export|metrology-scan|photogrammetry";
}
