using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Poses;
using Shaolin36.Core.Runs;

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
    private const string FeelPath = "res://data/feel.csv";
    private const string BrainsPath = "res://data/brains.csv";
    private const string RunPath = "res://data/run.csv";
    private const string PosesPath = "res://data/poses.csv";

    public override void _Ready()
    {
        bool smoke = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--smoke") >= 0;

        var result = TechniqueCsv.Parse(FileAccess.GetFileAsString(TechniquesPath) ?? string.Empty);
        foreach (var error in result.Errors) GD.PushError($"{TechniquesPath}: {error}");
        var rules = RulesCsv.Parse(FileAccess.GetFileAsString(RulesPath) ?? string.Empty);
        foreach (var error in rules.Errors) GD.PushError($"{RulesPath}: {error}");
        var feel = FeelCsv.Parse(FileAccess.GetFileAsString(FeelPath) ?? string.Empty);
        foreach (var error in feel.Errors) GD.PushError($"{FeelPath}: {error}");
        var brains = BrainCsv.Parse(FileAccess.GetFileAsString(BrainsPath) ?? string.Empty);
        foreach (var error in brains.Errors) GD.PushError($"{BrainsPath}: {error}");
        var run = RunCsv.Parse(FileAccess.GetFileAsString(RunPath) ?? string.Empty);
        foreach (var error in run.Errors) GD.PushError($"{RunPath}: {error}");
        var runErrors = run.Ok && result.Ok && brains.Ok
            ? run.Sheet!.Check(new TechniqueBook(result.Techniques), brains.Sheet!)
            : new System.Collections.Generic.List<string>();
        foreach (var error in runErrors) GD.PushError($"{RunPath}: {error}");
        var poses = PoseCsv.Parse(FileAccess.GetFileAsString(PosesPath) ?? string.Empty);
        foreach (var error in poses.Errors) GD.PushError($"{PosesPath}: {error}");
        var poseErrors = poses.Ok && result.Ok ? poses.Sheet!.Check(result.Techniques) : new System.Collections.Generic.List<string>();
        foreach (var error in poseErrors) GD.PushError($"{PosesPath}: {error}");

        bool ok = result.Ok && rules.Ok && feel.Ok && brains.Ok && run.Ok && runErrors.Count == 0 && poses.Ok && poseErrors.Count == 0;
        int problems = result.Errors.Count + rules.Errors.Count + feel.Errors.Count + brains.Errors.Count + run.Errors.Count + runErrors.Count + poses.Errors.Count + poseErrors.Count;
        var summary = ok
            ? $"Shaolin 36 · {result.Techniques.Count} techniques, {Rules.Keys.Length} rules, {FeelSheet.EventNames.Count} feel events, {brains.Sheet?.Styles.Count} opponent brains and a {run.Sheet?.Fights}-fight run and {poses.Sheet?.Count} poses loaded · sim {Sim.TicksPerSecond} Hz"
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

        // A fresh run each launch: the seed is rolled here, and every choice in the run follows from it.
        var arena = new Arena(rules.Rules!, result.Techniques, feel.Sheet!, brains.Sheet!, run.Sheet!, poses.Sheet!, (int)(GD.Randi() & 0x7fffffff));
        if (smoke)
        {
            // Run a short fight with no screen: a training dummy's presses against the opponent brain.
            int hits = arena.Simulate(Sim.TicksPerSecond * 20, out int frozen);
            GD.Print($"Playtest arena: {hits} hits in 20 simulated seconds, {frozen} hit-stop ticks");
            int runHits = arena.SimulateRun(Sim.TicksPerSecond * 20, out var runSummary);
            GD.Print($"Demo run: {runHits} hits in 20 simulated seconds, {runSummary}");
            bool fought = hits > 0 && runHits > 0;
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
