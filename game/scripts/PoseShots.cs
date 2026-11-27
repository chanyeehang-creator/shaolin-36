using System.Collections.Generic;
using System.Linq;
using Godot;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Poses;

namespace Shaolin36.Game;

/// <summary>
/// Pose pictures: renders poses from the pose sheet to PNG files so they can be checked without playing
/// (scripts\pose-shots.bat). Each picture shows one pose three ways, side by side: as you see an opponent in a fight
/// (from in front, a little above and to one side), from the fighter's right (facing right) and from its left
/// (facing left). A red line on the floor points where the fighter faces (at its opponent).
/// Name a technique instead of a pose (huxing_quan_1) for a strip: the whole string played as a fight plays it (each next
/// hit chained on its first chain frame), one picture per frame from the fighter's right, labelled with the hit, the part
/// of the move and how far the body has moved. "huxing_quan_1:1" stops after 1 hit (to see its recovery), ":2" after 2.
/// Run with "-- --pose-shots &lt;folder&gt; [pose or technique names...]"; with no names it renders every pose.
/// </summary>
public partial class PoseShots : Node3D
{
    private const int Size = 400;
    private const int StripSize = 220;
    private const int StripColumns = 10;
    private static readonly Vector3 Aim = new(0, 1.0f, 0);

    // Where each view's camera stands. The mannequin faces -Z.
    private static readonly Vector3[] Views =
    {
        new(-1.6f, 2.6f, -5.2f), // as you see an opponent: in front, above, a little to one side
        new(5.6f, 1.0f, 0f),     // from its right: it faces to the right of the picture
        new(-5.6f, 1.0f, 0f),    // from its left: it faces to the left of the picture
    };

    private readonly PoseSheet _sheet = null!;
    private readonly Rules _rules = null!;
    private readonly List<Technique> _techniques = new();
    private readonly string _folder = "";
    private readonly List<string> _names = new();
    private readonly Mannequin _body = new();
    private readonly Camera3D _camera = new() { Fov = 28, Current = true };
    private readonly Label3D _label = new()
    {
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FontSize = 56,
        PixelSize = 0.004f,
        OutlineSize = 0,
        Modulate = Colors.Black,
        Position = new Vector3(0, 2.0f, 0),
    };

    public PoseShots() { }

    public PoseShots(PoseSheet sheet, Rules rules, IEnumerable<Technique> techniques, string folder, IEnumerable<string> names)
    {
        _sheet = sheet;
        _rules = rules;
        _techniques.AddRange(techniques);
        _folder = folder;
        _names.AddRange(names);
        if (_names.Count == 0) _names.AddRange(sheet.Names);
    }

    public override async void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Environment
            {
                BackgroundMode = Environment.BGMode.Color,
                BackgroundColor = new Color(0.82f, 0.82f, 0.80f),
                AmbientLightSource = Environment.AmbientSource.Color,
                AmbientLightColor = new Color(1, 1, 1),
                AmbientLightEnergy = 0.45f,
            },
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), ShadowEnabled = true });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(60, 60) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.62f, 0.60f) },
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.04f, 0.01f, 1.2f) },
            Position = new Vector3(0, 0.005f, -0.9f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.1f, 0.1f) },
        });
        AddChild(_body);
        _body.Paint(new Color(0.30f, 0.62f, 0.50f), Colors.Black, 0);
        AddChild(_camera);
        AddChild(_label);

        DirAccess.MakeDirRecursiveAbsolute(_folder);
        int saved = 0;
        foreach (var name in _names)
        {
            var pose = _sheet.Find(name);
            int colon = name.IndexOf(':');
            int hits = colon > 0 && int.TryParse(name[(colon + 1)..], out int n) && n > 0 ? n : int.MaxValue;
            var id = colon > 0 ? name[..colon] : name;
            var technique = _techniques.FirstOrDefault(t => t.Id == id);
            Image? picture = pose != null ? await Picture(name, pose) : technique != null ? await Strip(technique, hits) : null;
            if (picture == null)
            {
                GD.PushError($"pose shots: no pose or technique called '{name}'");
                continue;
            }
            var path = System.IO.Path.Combine(_folder, name.Replace(':', '-') + (pose != null ? "" : ".strip") + ".png");
            if (picture.SavePng(path) == Error.Ok) saved++;
            else GD.PushError($"pose shots: couldn't save {path}");
        }
        GD.Print($"pose shots: {saved} picture(s) in {_folder}");
        GetTree().Quit(saved > 0 ? 0 : 1);
    }

    /// <summary>One pose from the three views.</summary>
    private async System.Threading.Tasks.Task<Image> Picture(string name, Pose pose)
    {
        _label.Visible = false;
        _body.Apply(pose, plantFeet: name != "down");
        var sheet = Image.CreateEmpty(Size * Views.Length, Size, false, Image.Format.Rgb8);
        for (int v = 0; v < Views.Length; v++)
            sheet.BlitRect(await Shot(Views[v], Size), new Rect2I(0, 0, Size, Size), new Vector2I(v * Size, 0));
        return sheet;
    }

    /// <summary>
    /// The string <paramref name="first"/> starts, frame by frame: the fighter presses it, then chains each next hit on its
    /// first chain frame (up to <paramref name="hits"/> hits), against a standing opponent placed so the first hit lands.
    /// </summary>
    private async System.Threading.Tasks.Task<Image> Strip(Technique first, int hits)
    {
        var book = new TechniqueBook(_techniques);
        var mine = new Loadout(book);
        mine.Assign(Slot.Square, first);
        int apart = System.Math.Max(_rules.MinDistance + 10, first.FullReachCm - 20);
        var duel = new Duel(_rules, mine, new Loadout(book), 0, apart);
        var me = duel.One;

        var frames = new List<(Pose pose, string label, bool active)>();
        var shown = new Mannequin();
        AddChild(shown);
        shown.Visible = false;
        for (int tick = 0; tick < 400; tick++)
        {
            bool press = tick == 0 || me.InChainWindow && me.StringHit < hits;
            duel.Step(press ? SlotInput.Press(Slot.Square) : SlotInput.None, SlotInput.None);
            if (tick > 0 && !me.State.IsInMove()) break;
            shown.Show(me, _sheet);
            var t = me.Technique!;
            string part = me.State == FighterState.Startup ? "wind-up" : me.State == FighterState.Active ? "HIT" : "recovery";
            int of = me.State == FighterState.Startup ? t.StartupFrames : me.State == FighterState.Active ? t.ActiveFrames : t.RecoveryFrames;
            int at = me.State == FighterState.Startup ? me.MoveFrame : me.State == FighterState.Active ? me.MoveFrame - t.StartupFrames : me.MoveFrame - t.StartupFrames - t.ActiveFrames;
            frames.Add((shown.LastPose!, $"hit {me.StringHit} {part} {at + 1}/{of}\n+{me.XCm} cm", me.State == FighterState.Active));
        }
        shown.QueueFree();

        int rows = (frames.Count + StripColumns - 1) / StripColumns;
        var strip = Image.CreateEmpty(StripSize * StripColumns, StripSize * rows, false, Image.Format.Rgb8);
        strip.Fill(Colors.White);
        _label.Visible = true;
        for (int i = 0; i < frames.Count; i++)
        {
            _body.Apply(frames[i].pose);
            _label.Text = frames[i].label;
            _label.Modulate = frames[i].active ? new Color(0.8f, 0.05f, 0.05f) : Colors.Black;
            var shot = await Shot(Views[1], StripSize);
            strip.BlitRect(shot, new Rect2I(0, 0, StripSize, StripSize), new Vector2I(i % StripColumns * StripSize, i / StripColumns * StripSize));
        }
        return strip;
    }

    /// <summary>A square picture from <paramref name="from"/>, <paramref name="size"/> pixels across.</summary>
    private async System.Threading.Tasks.Task<Image> Shot(Vector3 from, int size)
    {
        _camera.LookAtFromPosition(from, Aim);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var shot = GetViewport().GetTexture().GetImage();
        int side = Mathf.Min(shot.GetWidth(), shot.GetHeight());
        var square = shot.GetRegion(new Rect2I((shot.GetWidth() - side) / 2, (shot.GetHeight() - side) / 2, side, side));
        square.Resize(size, size);
        square.Convert(Image.Format.Rgb8);
        return square;
    }
}
