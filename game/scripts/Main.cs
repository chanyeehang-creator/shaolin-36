using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Data;

namespace Shaolin36.Game;

/// <summary>
/// Boot scene: loads the data sheets through the core library and opens the
/// playtest <see cref="Arena"/>. Run with "-- --smoke" to load the data, run a short
/// fight with no screen, report and quit (used by CI and the headless boot check).
/// </summary>
public partial class Main : Node3D
{
    private const string TechniquesPath = "res://data/techniques.csv";
    private const string RulesPath = "res://data/rules.csv";

    public override void _Ready()
    {
        bool smoke = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;

        var result = TechniqueCsv.Parse(FileAccess.GetFileAsString(TechniquesPath) ?? string.Empty);
        foreach (var error in result.Errors) GD.PushError($"{TechniquesPath}: {error}");
        var rules = RulesCsv.Parse(FileAccess.GetFileAsString(RulesPath) ?? string.Empty);
        foreach (var error in rules.Errors) GD.PushError($"{RulesPath}: {error}");

        bool ok = result.Ok && rules.Ok;
        int problems = result.Errors.Count + rules.Errors.Count;
        var summary = ok
            ? $"Shaolin 36 · {result.Techniques.Count} techniques and {Rules.Keys.Length} rules loaded · sim {Sim.TicksPerSecond} Hz"
            : $"Data check failed: {problems} problem(s), see the Output panel";
        GD.Print(summary);

        if (!ok)
        {
            if (smoke)
            {
                GD.Print("SMOKE FAIL");
                GetTree().Quit(1);
                return;
            }
            ShowError(summary);
            return;
        }

        var arena = new Arena(rules.Rules!, result.Techniques);
        if (smoke)
        {
            // Run a short fight with no screen: the dummy against a copy of itself.
            int hits = arena.Simulate(Sim.TicksPerSecond * 20);
            GD.Print($"Playtest arena: {hits} hits in 20 simulated seconds");
            bool fought = hits > 0;
            arena.Free();
            GD.Print(fought ? "SMOKE OK" : "SMOKE FAIL");
            GetTree().Quit(fought ? 0 : 1);
            return;
        }

        AddChild(arena);
    }

    private void ShowError(string summary)
    {
        var hud = new CanvasLayer();
        hud.AddChild(new Label { Text = summary, Position = new Vector2(16, 16) });
        AddChild(hud);
    }
}
