using OpenDesignCore.Data;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// A filament reference is an identifier that will be read back years later by
/// someone asking which spool a compensation came from. These tests are about
/// refusal: a reference that parses but points nowhere is worse than one that
/// fails at the command line.
/// </summary>
public sealed class FilamentRefTests
{
    private const string StrValid =
        "open-filament-database:dataset-v2026.07.10:"
        + "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black";

    [Fact]
    public void ValidRef_ParsesIntoItsParts()
    {
        FilamentRef oRef = FilamentRef.OParse(StrValid);

        Assert.Equal("open-filament-database", oRef.Catalog);
        Assert.Equal("dataset-v2026.07.10", oRef.DatasetVersion);
        Assert.Equal(
            "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black",
            oRef.Path);
        Assert.Null(oRef.Uuid);
    }

    [Fact]
    public void CanonicalForm_RoundTrips()
    {
        // The recorded string is the canonical form, so two records naming the
        // same spool must be byte-identical there. If ToString and OParse ever
        // disagree, records stop comparing equal for no visible reason.
        Assert.Equal(StrValid, FilamentRef.OParse(StrValid).ToString());

        string strWithUuid = StrValid + "#f81d4fae-7dec-11d0-a765-00a0c91e6bf6";
        Assert.Equal(strWithUuid, FilamentRef.OParse(strWithUuid).ToString());
    }

    [Fact]
    public void Uuid_IsParsedAndValidated()
    {
        FilamentRef oRef = FilamentRef.OParse(StrValid + "#f81d4fae-7dec-11d0-a765-00a0c91e6bf6");
        Assert.Equal("f81d4fae-7dec-11d0-a765-00a0c91e6bf6", oRef.Uuid);

        FilamentRefException oEx =
            Assert.Throws<FilamentRefException>(() => FilamentRef.OParse(StrValid + "#not-a-uuid"));
        Assert.Contains("uuid", oEx.Message);
    }

    [Fact]
    public void UnknownCatalog_IsRefused()
    {
        // Refused rather than stored uninterpreted: a reference into a
        // catalogue nothing can resolve looks like provenance and is not.
        FilamentRefException oEx = Assert.Throws<FilamentRefException>(
            () => FilamentRef.OParse("some-other-db:v1:brands/b/materials/M/filaments/f/variants/v"));
        Assert.Contains("catalog", oEx.Message);
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("main")]
    [InlineData("HEAD")]
    public void MovingDatasetPointer_IsRefused(string strVersion)
    {
        // The whole reason the version is recorded is that the catalogue moves.
        // A pointer that follows it records nothing.
        FilamentRefException oEx = Assert.Throws<FilamentRefException>(
            () => FilamentRef.OParse(
                $"open-filament-database:{strVersion}:"
                + "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black"));
        Assert.Contains("moving pointer", oEx.Message);
    }

    [Fact]
    public void BrandLevelPath_IsRefused()
    {
        // Shrinkage varies between colours of one product line, so a path that
        // stops at the brand is not an identity for anything measurable.
        FilamentRefException oEx = Assert.Throws<FilamentRefException>(
            () => FilamentRef.OParse("open-filament-database:dataset-v2026.07.10:brands/prusament"));
        Assert.Contains("variant path", oEx.Message);
    }

    [Fact]
    public void PathWithWrongKeys_IsRefused()
    {
        // Right shape, wrong scheme — eight segments but not the catalogue's
        // addressing. Caught, because this is what a hand-edited path looks like.
        FilamentRefException oEx = Assert.Throws<FilamentRefException>(
            () => FilamentRef.OParse(
                "open-filament-database:dataset-v2026.07.10:"
                + "vendors/prusament/types/PLA/products/prusament-pla/colours/galaxy-black"));
        Assert.Contains("brands", oEx.Message);
    }

    [Fact]
    public void MalformedText_IsRefusedWithAnExample()
    {
        // The error has to teach the format; nobody remembers a three-field
        // colon-separated grammar from a "malformed" message alone.
        FilamentRefException oEx =
            Assert.Throws<FilamentRefException>(() => FilamentRef.OParse("prusament-galaxy-black"));
        Assert.Contains("open-filament-database:dataset-v2026.07.10:", oEx.Message);
    }

    [Fact]
    public void EmptyText_IsRefused()
        => Assert.Throws<FilamentRefException>(() => FilamentRef.OParse("   "));
}
