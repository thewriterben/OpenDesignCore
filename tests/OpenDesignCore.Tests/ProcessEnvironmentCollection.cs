using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// Every test class that reads or sets <c>ODC_ROOT</c> / <c>ODC_OPENPARTSCORE</c>
/// (directly, or through <see cref="TestRegistry.StrSiblingRegistryOrNull"/>,
/// which resolves through the env var like the engine does) belongs to this
/// collection, so xunit runs them one class at a time.
///
/// Found 2026-09-06: <c>ShippedData_LoadsAndIsFullyCited</c> failed once, then
/// <c>ShippedRegistry_Esp32S3Wroom1_EnvelopeMatchesDatasheet</c> reported the
/// fixture registry's single part in place of the shipped one — the MCP tool
/// tests had pointed the process-global variable at the fixture while a
/// DataStore test was reading the real sibling. A process environment is a
/// process environment; parallel classes cannot each have their own.
/// </summary>
[CollectionDefinition(StrName, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string StrName = "process environment";
}
