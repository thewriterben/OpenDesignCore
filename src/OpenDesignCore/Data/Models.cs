namespace OpenDesignCore.Data;

/// <summary>
/// Where every value in an entry comes from. An entry without a non-empty
/// citation is invalid — the loader rejects it (ADR-0006 grounding rule).
/// </summary>
public sealed record SourceCitation
{
    public required string Citation { get; init; }
    public string? Url { get; init; }
    public string? Retrieved { get; init; }
}

/// <summary>Axis-aligned bounding envelope. Millimetres (ADR-0004).</summary>
public sealed record EnvelopeMm
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
}

/// <summary>
/// A physical part whose envelope a model may design around. Read from the
/// OpenPartsCore registry (ADR-0016); only entries carrying a cited
/// <c>envelope_mm</c> become one of these — the rest are listed in
/// <see cref="DataSet.UnofferedParts"/> with the reason.
/// </summary>
public sealed record PartEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required EnvelopeMm EnvelopeMm { get; init; }
    /// <summary>Dimensional tolerance of the envelope, in mm, if the source states one.</summary>
    public double? ToleranceMm { get; init; }
    /// <summary>
    /// The envelope's own citation (OpenPartsCore ADR-0006), not the entry's:
    /// the entry-level source of an ingested board is a registry with no
    /// dimensions, so it is the wrong thing to record against a number.
    /// </summary>
    public required SourceCitation Source { get; init; }
    /// <summary>SHA-256 of the registry file the entry was read from. Pins the content when the commit cannot.</summary>
    public required string FileSha256 { get; init; }
}

/// <summary>A registry entry that exists but cannot be designed around, and why.</summary>
public sealed record UnofferedPart
{
    public required string Id { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Process constraints for a fabrication material.</summary>
public sealed record MaterialEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>[min, max] shrinkage in percent, if known.</summary>
    public double[]? ShrinkagePctRange { get; init; }
    /// <summary>Achievable dimensional tolerance for FDM, in mm, if known.</summary>
    public double? FdmToleranceMm { get; init; }
    /// <summary>
    /// Which catalogued filament variant this entry describes, if it describes
    /// one. Optional: plenty of real filament is not catalogued anywhere, and
    /// requiring a reference would block work without making anything safer.
    /// Identity only — no value in this entry may be sourced from the
    /// catalogue (ADR-0013); that is what <see cref="Source"/> is for.
    /// </summary>
    public FilamentRef? FilamentRef { get; init; }
    public required SourceCitation Source { get; init; }
}

/// <summary>Everything loaded from data/ and from the OpenPartsCore registry, validated.</summary>
public sealed record DataSet
{
    /// <summary>Registry entries with a cited envelope — the ones a model can be run around.</summary>
    public required IReadOnlyList<PartEntry> Parts { get; init; }
    /// <summary>Registry entries without one. Absent means unknown, never zero; they are named, not hidden.</summary>
    public required IReadOnlyList<UnofferedPart> UnofferedParts { get; init; }
    public required IReadOnlyList<MaterialEntry> Materials { get; init; }
    /// <summary>Directory the parts were read from (the OpenPartsCore checkout).</summary>
    public required string PartsRegistryDir { get; init; }
    /// <summary>
    /// Commit of the OpenPartsCore checkout, read from its .git, or
    /// <c>"unresolved"</c> when there is no .git to read. Recorded in provenance
    /// either way — an unresolved commit is a fact about the run, not a gap to
    /// paper over — and <see cref="PartEntry.FileSha256"/> pins the content regardless.
    /// </summary>
    public required string PartsRegistryCommit { get; init; }
}
