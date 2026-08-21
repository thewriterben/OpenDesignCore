using OpenDesignCore.Data;
using Xunit;

namespace OpenDesignCore.Tests;

public sealed class DataStoreTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-data-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    /// <summary>Walk up from the test binary to the repository root.</summary>
    private static string StrRepoRoot()
    {
        DirectoryInfo? oDir = new(AppContext.BaseDirectory);
        while (oDir is not null && !File.Exists(Path.Combine(oDir.FullName, "OpenDesignCore.sln")))
            oDir = oDir.Parent;
        Assert.NotNull(oDir);
        return oDir.FullName;
    }

    private string StrWriteEntry(string strNamespace, string strFileName, string strJson)
    {
        string strDir = Path.Combine(_strTempDir, strNamespace);
        Directory.CreateDirectory(strDir);
        File.WriteAllText(Path.Combine(strDir, strFileName), strJson);
        return _strTempDir;
    }

    [Fact]
    public void ShippedData_LoadsAndIsFullyCited()
    {
        DataSet oData = DataStore.LoadAll(Path.Combine(StrRepoRoot(), "data"));

        Assert.NotEmpty(oData.Parts);
        Assert.NotEmpty(oData.Materials);
        Assert.All(oData.Parts, oPart => Assert.False(string.IsNullOrWhiteSpace(oPart.Source.Citation)));
        Assert.All(oData.Materials, oMat => Assert.False(string.IsNullOrWhiteSpace(oMat.Source.Citation)));
    }

    [Fact]
    public void ShippedData_Esp32S3Wroom1_EnvelopeMatchesDatasheet()
    {
        DataSet oData = DataStore.LoadAll(Path.Combine(StrRepoRoot(), "data"));
        PartEntry oPart = Assert.Single(oData.Parts, o => o.Id == "parts/esp32-s3-wroom-1");

        // Espressif ESP32-S3-WROOM-1/1U Datasheet, Table 1-1: 18.0 x 25.5 x 3.1 mm.
        Assert.Equal(18.0, oPart.EnvelopeMm.X);
        Assert.Equal(25.5, oPart.EnvelopeMm.Y);
        Assert.Equal(3.1, oPart.EnvelopeMm.Z);
    }

    [Fact]
    public void UncitedEntry_IsRejected()
    {
        string strDir = StrWriteEntry("parts", "bad.json", """
            {
              "id": "parts/bad",
              "name": "Uncited part",
              "envelope_mm": { "x": 1, "y": 1, "z": 1 },
              "source": { "citation": "   " }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir));
        Assert.Contains(oEx.Errors, s => s.Contains("citation"));
    }

    [Fact]
    public void UnknownField_IsRejected()
    {
        string strDir = StrWriteEntry("parts", "extra.json", """
            {
              "id": "parts/extra",
              "name": "Part with unknown field",
              "envelope_mm": { "x": 1, "y": 1, "z": 1 },
              "source": { "citation": "test" },
              "surprise_field": 42
            }
            """);

        Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir));
    }

    [Fact]
    public void NonPositiveEnvelope_IsRejected()
    {
        string strDir = StrWriteEntry("parts", "flat.json", """
            {
              "id": "parts/flat",
              "name": "Zero-thickness part",
              "envelope_mm": { "x": 10, "y": 10, "z": 0 },
              "source": { "citation": "test" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir));
        Assert.Contains(oEx.Errors, s => s.Contains("positive"));
    }

    [Fact]
    public void MalformedShrinkageRange_IsRejected()
    {
        string strDir = StrWriteEntry("materials", "bad-range.json", """
            {
              "id": "materials/bad-range",
              "name": "Backwards range",
              "shrinkage_pct_range": [0.5, 0.2],
              "source": { "citation": "test" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir));
        Assert.Contains(oEx.Errors, s => s.Contains("shrinkage_pct_range"));
    }

    [Fact]
    public void MaterialWithValidFilamentRef_Loads()
    {
        string strDir = StrWriteEntry("materials", "spooled.json", """
            {
              "id": "materials/prusament-pla-galaxy-black",
              "name": "Prusament PLA Galaxy Black",
              "filament_ref": {
                "catalog": "open-filament-database",
                "dataset_version": "dataset-v2026.07.10",
                "path": "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black"
              },
              "source": { "citation": "test" }
            }
            """);

        DataSet oData = DataStore.LoadAll(strDir);
        MaterialEntry oMat = Assert.Single(oData.Materials);
        Assert.NotNull(oMat.FilamentRef);
        Assert.Equal("dataset-v2026.07.10", oMat.FilamentRef!.DatasetVersion);
    }

    [Fact]
    public void MaterialWithUnpinnedFilamentRef_IsRejectedNamingTheEntry()
    {
        // The loader reports every failure with the entry that owns it, and a
        // filament reference is no exception — "invalid ref" without the id is
        // useless in a store of many materials.
        string strDir = StrWriteEntry("materials", "unpinned.json", """
            {
              "id": "materials/unpinned",
              "name": "Reference to a moving catalogue",
              "filament_ref": {
                "catalog": "open-filament-database",
                "dataset_version": "latest",
                "path": "brands/prusament/materials/PLA/filaments/prusament-pla/variants/galaxy-black"
              },
              "source": { "citation": "test" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir));
        Assert.Contains(oEx.Errors, s => s.Contains("materials/unpinned") && s.Contains("filament_ref"));
    }
}
