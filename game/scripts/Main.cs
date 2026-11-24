using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Data;

namespace Shaolin36.Game;

/// <summary>
/// Stage 0 boot scene: loads the technique sheet through the core library and
/// shows an arena with two capsule fighters. Run with "-- --smoke" to load,
/// report and quit (used by CI and the headless boot check).
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

        if (smoke)
        {
            GD.Print(ok ? "SMOKE OK" : "SMOKE FAIL");
            GetTree().Quit(ok ? 0 : 1);
            return;
        }

        BuildArena(summary);
    }

    private void BuildArena(string summary)
    {
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -30, 0),
            ShadowEnabled = true,
        });

        AddChild(new Camera3D
        {
            Position = new Vector3(0, 3.2f, 7f),
            RotationDegrees = new Vector3(-18, 0, 0),
        });

        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(16, 16) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.38f, 0.30f) },
        });

        AddChild(Capsule("Player", new Vector3(-1.5f, 1f, 0), new Color(0.95f, 0.65f, 0.15f)));
        AddChild(Capsule("Opponent", new Vector3(1.5f, 1f, 0), new Color(0.55f, 0.15f, 0.15f)));

        var hud = new CanvasLayer();
        hud.AddChild(new Label { Text = summary, Position = new Vector2(16, 16) });
        AddChild(hud);
    }

    private static MeshInstance3D Capsule(string name, Vector3 position, Color color) => new()
    {
        Name = name,
        Position = position,
        Mesh = new CapsuleMesh { Radius = 0.4f, Height = 2f },
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
    };
}
