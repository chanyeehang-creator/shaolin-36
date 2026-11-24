// Checks the game's data sheets before the game loads them.
// Usage: dotnet run --project tools/DataCheck -- [data folder, default game/data]
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var dir = args.Length > 0 ? args[0] : Path.Combine("game", "data");
var techniquesPath = Path.Combine(dir, "techniques.csv");
var rulesPath = Path.Combine(dir, "rules.csv");
var feelPath = Path.Combine(dir, "feel.csv");
int problems = 0;

foreach (var path in new[] { techniquesPath, rulesPath, feelPath })
{
    if (File.Exists(path)) continue;
    Console.Error.WriteLine($"FAIL: {path} not found");
    return 2;
}

var techniques = TechniqueCsv.Parse(File.ReadAllText(techniquesPath));
foreach (var error in techniques.Errors) Console.Error.WriteLine($"{techniquesPath}: {error}");
problems += techniques.Errors.Count;

var rules = RulesCsv.Parse(File.ReadAllText(rulesPath));
foreach (var error in rules.Errors) Console.Error.WriteLine($"{rulesPath}: {error}");
problems += rules.Errors.Count;

var feel = FeelCsv.Parse(File.ReadAllText(feelPath));
foreach (var error in feel.Errors) Console.Error.WriteLine($"{feelPath}: {error}");
problems += feel.Errors.Count;

if (problems > 0)
{
    Console.Error.WriteLine($"FAIL: {problems} problem(s) in {dir}");
    return 1;
}

Console.WriteLine($"OK: {techniques.Techniques.Count} techniques in {techniquesPath}");
Console.WriteLine($"OK: {Rules.Keys.Length} rules in {rulesPath}");
Console.WriteLine($"OK: {FeelSheet.EventNames.Count} feel events in {feelPath}");

// Tuning aid: how forgiving each string is.
var byId = techniques.Techniques.ToDictionary(t => t.Id);
var starts = techniques.Techniques.Where(t => t.HasNext && !techniques.Techniques.Any(o => o.Next == t.Id));
foreach (var t in starts)
    Console.WriteLine($"  {t.Id} string: every hit stays in reach (with step-in and knockback) if it starts {t.StringReachFrom(id => byId.GetValueOrDefault(id))} cm away or closer");
foreach (var t in techniques.Techniques.Where(t => t.HasNext))
{
    var next = byId[t.Next];
    int link = t.LinkFramesInto(next);
    var verdict = link > 0
        ? $"links if chained within the first {link} of {t.ChainWindowFrames} chain frames"
        : "never links: the defender always gets a gap";
    Console.WriteLine($"  {t.Id} -> {next.Id}: {verdict}");
}

return 0;
