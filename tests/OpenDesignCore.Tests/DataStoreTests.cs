using OpenDesignCore.Data;
using Xunit;

namespace OpenDesignCore.Tests;

[Collection(ProcessEnvironmentCollection.StrName)]
public sealed class DataStoreTests : IDisposable
{
    private readonly string _strTempDir =
        Directory.CreateTempSubdirectory("odc-data-tests").FullName;

    public void Dispose() => Directory.Delete(_strTempDir, recursive: true);

    private static string StrRepoRoot() => TestRegistry.StrRepoRoot();

    /// <summary>This repo's own data/ (materials) plus a fixture registry, both in the temp dir.</summary>
    private (string strData, string strRegistry) Dirs()
    {
        string strData = Path.Combine(_strTempDir, "data");
        Directory.CreateDirectory(strData);
        string strRegistry = TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore"));
        return (strData, strRegistry);
    }

    private string StrWriteEntry(string strNamespace, string strFileName, string strJson)
    {
        string strDir = Path.Combine(_strTempDir, "data", strNamespace);
        Directory.CreateDirectory(strDir);
        File.WriteAllText(Path.Combine(strDir, strFileName), strJson);
        return Path.Combine(_strTempDir, "data");
    }

    [Fact]
    public void ShippedData_LoadsAndIsFullyCited()
    {
        // Materials are this repo's; parts come from the sibling registry
        // (ADR-0016), so the shipped check needs it present.
        string? strRegistry = TestRegistry.StrSiblingRegistryOrNull();
        if (strRegistry is null) return;

        DataSet oData = DataStore.LoadAll(Path.Combine(StrRepoRoot(), "data"), strRegistry);

        Assert.NotEmpty(oData.Parts);
        Assert.NotEmpty(oData.UnofferedParts);
        Assert.NotEmpty(oData.Materials);
        Assert.All(oData.Parts, oPart => Assert.False(string.IsNullOrWhiteSpace(oPart.Source.Citation)));
        Assert.All(oData.Parts, oPart => Assert.Equal(64, oPart.FileSha256.Length));
        Assert.All(oData.Materials, oMat => Assert.False(string.IsNullOrWhiteSpace(oMat.Source.Citation)));
        Assert.Matches("^[0-9a-f]{40}$", oData.PartsRegistryCommit);
    }

    [Fact]
    public void ShippedRegistry_Esp32S3Wroom1_EnvelopeMatchesDatasheet()
    {
        string? strRegistry = TestRegistry.StrSiblingRegistryOrNull();
        if (strRegistry is null) return;

        DataSet oData = DataStore.LoadAll(Path.Combine(StrRepoRoot(), "data"), strRegistry);
        PartEntry oPart = Assert.Single(oData.Parts, o => o.Id == "electronic/esp32-s3-wroom-1");

        // Espressif ESP32-S3-WROOM-1/1U Datasheet, Table 1-1: 18.0 x 25.5 x 3.1 mm.
        Assert.Equal(18.0, oPart.EnvelopeMm.X);
        Assert.Equal(25.5, oPart.EnvelopeMm.Y);
        Assert.Equal(3.1, oPart.EnvelopeMm.Z);
        Assert.Equal(0.2, oPart.ToleranceMm);
        Assert.Contains("Table 1-1", oPart.Source.Citation);

        // The generic multi-bridge board can never carry an envelope, and is
        // listed as not offered rather than dropped.
        Assert.Contains(oData.UnofferedParts, o => o.Id == "boards/esp32-s3");
    }

    [Fact]
    public void Registry_EntryWithoutEnvelope_IsListedNotOffered()
    {
        (string strData, string strRegistry) = Dirs();
        DataSet oData = DataStore.LoadAll(strData, strRegistry);

        Assert.Single(oData.Parts, o => o.Id == TestRegistry.StrFixtureId);
        UnofferedPart oUnoffered = Assert.Single(oData.UnofferedParts);
        Assert.Equal(TestRegistry.StrUnofferedId, oUnoffered.Id);
        Assert.Contains("no envelope_mm", oUnoffered.Reason);
        Assert.Equal("unresolved", oData.PartsRegistryCommit); // fixture has no .git
    }

    [Fact]
    public void Registry_UnknownUpstreamFieldOutsideTheEnvelope_IsTolerated()
    {
        // The fixture carries `some_new_upstream_field` in attributes. The
        // registry's schema is validated by its own CI; this engine is strict
        // only about the object it consumes.
        (string strData, string strRegistry) = Dirs();
        DataSet oData = DataStore.LoadAll(strData, strRegistry);
        Assert.Contains(oData.Parts, o => o.Id == TestRegistry.StrFixtureId);
    }

    [Fact]
    public void Registry_UncitedEnvelope_IsRejected()
    {
        (string strData, string strRegistry) = Dirs();
        TestRegistry.StrWriteEntry(strRegistry, "electronic", "uncited.json", """
            {
              "id": "electronic/uncited",
              "name": "uncited",
              "source": { "citation": "the entry is cited" },
              "envelope_mm": { "x": 1, "y": 1, "z": 1, "source": { "citation": "   " } }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strData, strRegistry));
        Assert.Contains(oEx.Errors, s => s.Contains("electronic/uncited") && s.Contains("citation of its own"));
    }

    [Fact]
    public void Registry_EnvelopeWithUnknownKey_IsRejected()
    {
        (string strData, string strRegistry) = Dirs();
        TestRegistry.StrWriteEntry(strRegistry, "electronic", "extra.json", """
            {
              "id": "electronic/extra",
              "name": "extra",
              "source": { "citation": "test" },
              "envelope_mm": { "x": 1, "y": 1, "z": 1, "w": 1, "source": { "citation": "test" } }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strData, strRegistry));
        Assert.Contains(oEx.Errors, s => s.Contains("electronic/extra") && s.Contains("unknown key 'w'"));
    }

    [Fact]
    public void Registry_NonPositiveOrMissingAxis_IsRejected()
    {
        (string strData, string strRegistry) = Dirs();
        TestRegistry.StrWriteEntry(strRegistry, "electronic", "flat.json", """
            {
              "id": "electronic/flat",
              "name": "flat",
              "source": { "citation": "test" },
              "envelope_mm": { "x": 10, "y": 10, "z": 0, "source": { "citation": "test" } }
            }
            """);
        TestRegistry.StrWriteEntry(strRegistry, "electronic", "twod.json", """
            {
              "id": "electronic/twod",
              "name": "twod",
              "source": { "citation": "test" },
              "envelope_mm": { "x": 10, "y": 10, "source": { "citation": "test" } }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strData, strRegistry));
        Assert.Contains(oEx.Errors, s => s.Contains("electronic/flat") && s.Contains("positive"));
        Assert.Contains(oEx.Errors, s => s.Contains("electronic/twod") && s.Contains("envelope_mm.z missing"));
    }

    [Fact]
    public void Registry_Missing_FailsNamingTheEnvVar()
    {
        (string strData, _) = Dirs();
        DataValidationException oEx = Assert.Throws<DataValidationException>(
            () => DataStore.LoadAll(strData, Path.Combine(_strTempDir, "nowhere")));
        Assert.Contains(oEx.Errors, s => s.Contains(PartsRegistry.StrEnvVar));
    }

    [Fact]
    public void PrivatePartsDirectory_IsRefusedAsAFork()
    {
        (string strData, string strRegistry) = Dirs();
        Directory.CreateDirectory(Path.Combine(strData, "parts"));
        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strData, strRegistry));
        Assert.Contains(oEx.Errors, s => s.Contains("fork"));
    }

    [Fact]
    public void UnknownField_InOwnData_IsRejected()
    {
        (string strData, string strRegistry) = Dirs();
        StrWriteEntry("materials", "extra.json", """
            {
              "id": "materials/extra",
              "name": "Material with unknown field",
              "source": { "citation": "doi:10.1234/fixture" },
              "surprise_field": 42
            }
            """);

        Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strData, strRegistry));
    }

    [Theory]
    [InlineData("siyuan://blocks/20240101120000-abc1234", "siyuan URL")]
    [InlineData("see block 20240101120000-abc1234", "SiYuan block id")]
    public void MaterialCitingSiYuan_IsRejected(string strCitation, string strLabel)
    {
        string strDir = StrWriteEntry("materials", "siyuan.json", $$"""
            {
              "id": "materials/siyuan",
              "name": "SiYuan cited",
              "source": { "citation": "{{strCitation}}" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore"))));
        Assert.Contains(oEx.Errors, s => s.Contains("materials/siyuan") && s.Contains($"references SiYuan ({strLabel})"));
    }

    [Fact]
    public void MaterialWithVaultCitation_Loads()
    {
        string strDir = StrWriteEntry("materials", "vaulted.json", $$"""
            {
              "id": "materials/vaulted",
              "name": "Vault cited",
              "source": { "citation": "vault:sha256:{{new string('a', 64)}}" }
            }
            """);

        DataSet oData = DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));
        Assert.Contains(oData.Materials, o => o.Id == "materials/vaulted");
    }

    [Fact]
    public void MalformedShrinkageRange_IsRejected()
    {
        string strDir = StrWriteEntry("materials", "bad-range.json", """
            {
              "id": "materials/bad-range",
              "name": "Backwards range",
              "shrinkage_pct_range": [0.5, 0.2],
              "source": { "citation": "doi:10.1234/fixture" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore"))));
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
              "source": { "citation": "doi:10.1234/fixture" }
            }
            """);

        DataSet oData = DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));
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
              "source": { "citation": "doi:10.1234/fixture" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore"))));
        Assert.Contains(oEx.Errors, s => s.Contains("materials/unpinned") && s.Contains("filament_ref"));
    }

    [Theory]
    [InlineData("vault:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa#page=2")]
    [InlineData("repo:thewriterben/OpenPartsCore@e32d954:data/material/creality-hyper-pla.json")]
    [InlineData("ledger:jobs:cc639d8a24ae")]
    [InlineData("standard:ISO 286-1:2010")]
    public void MaterialWithStrictCitationForm_Loads(string strCitation)
    {
        string strDir = StrWriteEntry("materials", "strict.json", $$"""
            {
              "id": "materials/strict",
              "name": "Strictly cited",
              "source": { "citation": "{{strCitation}}" }
            }
            """);

        DataSet oData = DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));
        Assert.Contains(oData.Materials, o => o.Id == "materials/strict");
    }

    [Theory]
    [InlineData("Research-Report.md (2026-07-12): PLA shrinkage 0.2-0.3%")]
    [InlineData("vault:sha256:abc")]
    [InlineData("see vault:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void MaterialWithFreeTextCitation_IsRejectedNamingTheEntry(string strCitation)
    {
        string strDir = StrWriteEntry("materials", "prose.json", $$"""
            {
              "id": "materials/prose",
              "name": "Prose cited",
              "source": { "citation": "{{strCitation}}" }
            }
            """);

        DataValidationException oEx =
            Assert.Throws<DataValidationException>(() => DataStore.LoadAll(strDir, TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore"))));
        Assert.Contains(oEx.Errors, s => s.Contains("materials/prose") && s.Contains("free text"));
    }

    [Fact]
    public void ShippedMaterials_TakeStrictCitationForms()
    {
        // Runs without the sibling registry: the fixture registry stands in for parts.
        DataSet oData = DataStore.LoadAll(Path.Combine(StrRepoRoot(), "data"), TestRegistry.StrWrite(Path.Combine(_strTempDir, "OpenPartsCore")));
        Assert.NotEmpty(oData.Materials);
        Assert.All(oData.Materials, oMat => Assert.NotNull(DataStore.StrCitationForm(oMat.Source.Citation)));
    }
}
