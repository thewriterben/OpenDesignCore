using System.Reflection;

// Thin-thread skeleton: prove the pinned stack resolves and compiles.
// No geometry runs here yet — model runs arrive with the ledger and
// provenance machinery (ROADMAP "Now"). Referencing PicoGK.Library is
// deliberate: it fails the build loudly if the package pin is broken.

string strTool = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
string strPicoGK = typeof(PicoGK.Library).Assembly.GetName().Version?.ToString() ?? "unknown";

Console.WriteLine($"OpenDesignCore {strTool}");
Console.WriteLine($"PicoGK assembly {strPicoGK} (pinned package 2.2.0, ADR-0008)");
Console.WriteLine($"ShapeKernel: compiled from submodule tag ShapeKernel-v2.1.0");
