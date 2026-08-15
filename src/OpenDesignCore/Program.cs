using System.Reflection;
using OpenDesignCore.Data;

// Commands:
//   (none)              print tool + pinned-stack versions
//   validate-data [dir] load and validate the reference data store (default: ./data)

if (args is ["validate-data", .. string[] aRest])
{
    string strDataDir = aRest is [string strDir, ..] ? strDir : "data";
    try
    {
        DataSet oData = DataStore.LoadAll(strDataDir);
        Console.WriteLine($"OK: {oData.Parts.Count} part(s), {oData.Materials.Count} material(s), all cited.");
        return 0;
    }
    catch (DataValidationException e)
    {
        Console.Error.WriteLine(e.Message);
        return 1;
    }
}

string strTool = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
string strPicoGK = typeof(PicoGK.Library).Assembly.GetName().Version?.ToString() ?? "unknown";

Console.WriteLine($"OpenDesignCore {strTool}");
Console.WriteLine($"PicoGK assembly {strPicoGK} (pinned package 2.2.0, ADR-0008)");
Console.WriteLine($"ShapeKernel: compiled from submodule tag ShapeKernel-v2.1.0");
Console.WriteLine();
Console.WriteLine("usage: OpenDesignCore validate-data [dir]");
return 0;
