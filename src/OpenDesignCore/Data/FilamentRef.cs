namespace OpenDesignCore.Data;

/// <summary>Thrown when a filament reference is malformed. Message lists every failure.</summary>
public sealed class FilamentRefException(string strMessage) : Exception(strMessage);

/// <summary>
/// A stable external identifier for one filament variant — a specific colour
/// of a specific product line from a specific brand.
///
/// <para>
/// Why this exists: <c>--material pla</c> is a label, not an identity. Two
/// spools from two brands are both "pla", so a compensation measured on one is
/// eligible for the other, and <see cref="Runs.CompensationRun"/>'s own caveat
/// already says the quiet part — "shrinkage varies by spool". Nothing in the
/// pipeline could name a spool until now.
/// </para>
///
/// <para>
/// <b>This is an identity, never a property.</b> No number reached through a
/// reference may enter a model run. The Open Filament Database catalogues
/// brands, product lines, colours, spool sizes and stores; it is not a source
/// for shrinkage, tolerance, or any other engineering value, and citing it as
/// one would be exactly the invented-material-property failure the project
/// rules forbid. A value still comes from <c>data/</c> with a citation to a
/// vendor TDS or a measurement (ADR-0013).
/// </para>
///
/// <para>
/// Canonical text form, which is what gets recorded:
/// <c>open-filament-database:dataset-v2026.07.10:brands/{b}/materials/{M}/filaments/{f}/variants/{v}</c>
/// with an optional <c>#uuid</c> suffix.
/// </para>
/// </summary>
public sealed record FilamentRef
{
    /// <summary>The only catalogue understood today (ADR-0013).</summary>
    public const string StrOpenFilamentDatabase = "open-filament-database";

    /// <summary>Which catalogue the path addresses. Not the value's citation — see <see cref="SourceCitation"/>.</summary>
    public required string Catalog { get; init; }

    /// <summary>
    /// The catalogue release this path was read from, e.g. <c>dataset-v2026.07.10</c>.
    /// Required: an unpinned reference into a catalogue that renames entries is
    /// not provenance, it is a lookup that used to work.
    /// </summary>
    public required string DatasetVersion { get; init; }

    /// <summary>Path within the catalogue: <c>brands/{b}/materials/{M}/filaments/{f}/variants/{v}</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Opaque catalogue id, if the entry has one. Optional; the path is the addressing scheme.</summary>
    public string? Uuid { get; init; }

    /// <summary>
    /// Parse the canonical text form, or refuse with every problem listed.
    /// Strict on purpose: a mistyped reference that parses is worse than one
    /// that fails, because it records a spool nobody printed.
    /// </summary>
    public static FilamentRef OParse(string strText)
    {
        if (string.IsNullOrWhiteSpace(strText))
            throw new FilamentRefException("Empty filament reference.");

        string strRest = strText.Trim();
        string? strUuid = null;

        int nHash = strRest.IndexOf('#', StringComparison.Ordinal);
        if (nHash >= 0)
        {
            strUuid = strRest[(nHash + 1)..];
            strRest = strRest[..nHash];
        }

        // Split into exactly three fields; the path itself carries no colons.
        string[] aFields = strRest.Split(':', 3);
        if (aFields.Length != 3)
        {
            throw new FilamentRefException(
                $"Malformed filament reference '{strText}'. Expected "
                + "<catalog>:<dataset-version>:<path>[#uuid], e.g. "
                + $"{StrOpenFilamentDatabase}:dataset-v2026.07.10:"
                + "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black");
        }

        FilamentRef oRef = new()
        {
            Catalog = aFields[0].Trim(),
            DatasetVersion = aFields[1].Trim(),
            Path = aFields[2].Trim(),
            Uuid = string.IsNullOrWhiteSpace(strUuid) ? null : strUuid.Trim(),
        };

        IReadOnlyList<string> aErrors = oRef.AValidate();
        if (aErrors.Count > 0)
        {
            throw new FilamentRefException(
                $"Invalid filament reference '{strText}':\n  " + string.Join("\n  ", aErrors));
        }
        return oRef;
    }

    /// <summary>
    /// Every problem with this reference, or an empty list. Returned rather
    /// than thrown so the data loader can report all of a file's failures at
    /// once, the way it already does for everything else.
    /// </summary>
    public IReadOnlyList<string> AValidate()
    {
        List<string> aErrors = [];

        if (Catalog != StrOpenFilamentDatabase)
        {
            aErrors.Add(
                $"unknown catalog '{Catalog}'. The only catalogue understood is "
                + $"'{StrOpenFilamentDatabase}' (ADR-0013). An unrecognised one is refused "
                + "rather than stored uninterpreted, because a reference nothing can resolve "
                + "is decoration.");
        }

        if (string.IsNullOrWhiteSpace(DatasetVersion))
        {
            aErrors.Add(
                "dataset_version is required. The catalogue is a moving target — entries are "
                + "renamed and retired — so a reference without the release it was read from "
                + "cannot be checked later.");
        }
        else if (DatasetVersion is "latest" or "main" or "HEAD")
        {
            aErrors.Add(
                $"dataset_version '{DatasetVersion}' is a moving pointer, not a release. It "
                + "will mean something different tomorrow, which defeats the point of "
                + "recording it. Name the release, e.g. dataset-v2026.07.10.");
        }

        // brands/{b}/materials/{M}/filaments/{f}/variants/{v}
        string[] aSegments = Path.Split('/', StringSplitOptions.None);
        if (aSegments.Length != 8 || aSegments.Any(string.IsNullOrWhiteSpace))
        {
            aErrors.Add(
                $"path '{Path}' is not a variant path. Expected eight non-empty segments: "
                + "brands/{brand}/materials/{MATERIAL}/filaments/{filament}/variants/{variant}.");
        }
        else
        {
            string[] aExpectedKeys = ["brands", "materials", "filaments", "variants"];
            for (int i = 0; i < aExpectedKeys.Length; i++)
            {
                if (aSegments[i * 2] != aExpectedKeys[i])
                {
                    aErrors.Add(
                        $"path '{Path}' has '{aSegments[i * 2]}' where '{aExpectedKeys[i]}' was "
                        + "expected. A brand- or material-level path is not specific enough: "
                        + "shrinkage varies between colours of one product line, so the "
                        + "variant is the unit that matters.");
                }
            }
        }

        if (Uuid is { } strUuid && !Guid.TryParse(strUuid, out Guid _))
        {
            aErrors.Add(
                $"uuid '{strUuid}' is not a UUID. Omit it rather than recording something "
                + "that cannot be looked up.");
        }

        return aErrors;
    }

    /// <summary>
    /// The canonical text form. Round-trips through <see cref="OParse"/>, and
    /// is what lands in comparison and compensation records — one spelling per
    /// reference, so two records naming the same spool are byte-identical there.
    /// </summary>
    public override string ToString()
        => Uuid is { } strUuid
            ? $"{Catalog}:{DatasetVersion}:{Path}#{strUuid}"
            : $"{Catalog}:{DatasetVersion}:{Path}";
}
