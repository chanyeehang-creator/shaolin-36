// Checks the game's data sheets before the game loads them.
// Usage: dotnet run --project tools/DataCheck -- [path/to/techniques.csv]
using Shaolin36.Core.Data;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var path = args.Length > 0 ? args[0] : Path.Combine("game", "data", "techniques.csv");
if (!File.Exists(path))
{
    Console.Error.WriteLine($"FAIL: {path} not found");
    return 2;
}

var result = TechniqueCsv.Parse(File.ReadAllText(path));
foreach (var error in result.Errors) Console.Error.WriteLine($"{path}: {error}");

if (!result.Ok)
{
    Console.Error.WriteLine($"FAIL: {result.Errors.Count} problem(s) in {path}");
    return 1;
}

Console.WriteLine($"OK: {result.Techniques.Count} techniques in {path}");
return 0;
