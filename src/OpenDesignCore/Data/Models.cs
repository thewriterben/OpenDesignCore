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

/// <summary>A physical part whose envelope a model may design around.</summary>
public sealed record PartEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required EnvelopeMm EnvelopeMm { get; init; }
    /// <summary>Dimensional tolerance of the envelope, in mm, if the source states one.</summary>
    public double? ToleranceMm { get; init; }
    public required SourceCitation Source { get; init; }
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
    public required SourceCitation Source { get; init; }
}

/// <summary>Everything loaded from data/, validated.</summary>
public sealed record DataSet
{
    public required IReadOnlyList<PartEntry> Parts { get; init; }
    public required IReadOnlyList<MaterialEntry> Materials { get; init; }
}
