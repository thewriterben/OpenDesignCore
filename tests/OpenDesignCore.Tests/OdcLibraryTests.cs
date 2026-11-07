using System.Text.RegularExpressions;
using Xunit;

namespace OpenDesignCore.Tests;

/// <summary>
/// <see cref="OdcLibrary"/> stops PicoGK 2.2.0's memory-monitor timer, whose
/// native call took the parallel test host down. The crash needs a tick to
/// land at the wrong moment (it fires every 10 s), so these tests check the
/// conditions instead: the timer is stopped once the library exists, the field
/// the workaround reaches is still there, Dispose still works, and nothing in
/// the repository constructs a plain Library.
/// </summary>
public sealed class OdcLibraryTests
{
    [Fact]
    public void TheTimerFieldTheWorkaroundStopsStillExists()
    {
        Assert.Equal(typeof(Timer), OdcLibrary.OTimerField.FieldType);
    }

    [Fact]
    public void TheMemoryMonitorIsStoppedOnceTheLibraryExists()
    {
        using OdcLibrary oLib = new(0.5f);
        Timer oTimer = (Timer)OdcLibrary.OTimerField.GetValue(oLib)!;
        Assert.False(oTimer.Change(1000, 1000), "Change re-armed the timer, so it was never disposed.");
    }

    [Fact]
    public void DisposingTwiceIsSafe()
    {
        OdcLibrary oLib = new(0.5f);
        oLib.Dispose();
        oLib.Dispose();
    }

    [Fact]
    public void NothingConstructsAPlainLibrary()
    {
        string strRoot = TestRegistry.StrRepoRoot();
        // "Library x = new(" or "new Library(", but not "OdcLibrary x = new(".
        Regex oPlain = new(@"(?<![A-Za-z])Library\s+\w+\s*=\s*new\s*\(|new\s+Library\s*\(");
        List<string> aFound = [];
        foreach (string strDir in new[] { "src", "tests" })
        {
            foreach (string strFile in Directory.EnumerateFiles(Path.Combine(strRoot, strDir), "*.cs", SearchOption.AllDirectories))
            {
                if (strFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || strFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;
                string[] aLines = File.ReadAllLines(strFile);
                for (int i = 0; i < aLines.Length; i++)
                {
                    string strLine = aLines[i].TrimStart();
                    if (strLine.StartsWith("//", StringComparison.Ordinal))
                        continue;
                    if (oPlain.IsMatch(strLine))
                        aFound.Add($"{Path.GetRelativePath(strRoot, strFile)}:{i + 1}");
                }
            }
        }
        Assert.True(aFound.Count == 0,
            "Open the library as OdcLibrary, not Library (see OdcLibrary's remarks): " + string.Join(", ", aFound));
    }
}
