using System.Collections.Generic;
using Godot;
using Shaolin36.Core.Poses;

namespace Shaolin36.Game;

/// <summary>
/// Pose pictures: renders poses from the pose sheet to PNG files so they can be checked without playing
/// (scripts\pose-shots.bat). Each picture shows one pose three ways, side by side: as you see an opponent in a fight
/// (from in front, a little above and to one side), from the fighter's right (facing right) and from its left
/// (facing left). A red line on the floor points where the fighter faces (at its opponent).
/// Run with "-- --pose-shots &lt;folder&gt; [pose names...]"; with no names it renders every pose.
/// </summary>
public partial class PoseShots : Node3D
{
    private const int Size = 400;
    private static readonly Vector3 Aim = new(0, 1.0f, 0);

    // Where each view's camera stands. The mannequin faces -Z.
    private static readonly Vector3[] Views =
    {
        new(-1.6f, 2.6f, -5.2f), // as you see an opponent: in front, above, a little to one side
        new(5.6f, 1.0f, 0f),     // from its right: it faces to the right of the picture
        new(-5.6f, 1.0f, 0f),    // from its left: it faces to the left of the picture
    };

    private readonly PoseSheet _sheet = null!;
    private readonly string _folder = "";
    private readonly List<string> _names = new();
    private readonly Mannequin _body = new();
    private readonly Camera3D _camera = new() { Fov = 28, Current = true };

    public PoseShots() { }

    public PoseShots(PoseSheet sheet, string folder, IEnumerable<string> names)
    {
        _sheet = sheet;
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

        DirAccess.MakeDirRecursiveAbsolute(_folder);
        int saved = 0;
        foreach (var name in _names)
        {
            var pose = _sheet.Find(name);
            if (pose == null)
            {
                GD.PushError($"pose shots: no pose called '{name}'");
                continue;
            }
            _body.Apply(pose, plantFeet: name != "down");
            var sheet = Image.CreateEmpty(Size * Views.Length, Size, false, Image.Format.Rgb8);
            for (int v = 0; v < Views.Length; v++)
            {
                _camera.LookAtFromPosition(Views[v], Aim);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var shot = GetViewport().GetTexture().GetImage();
                int side = Mathf.Min(shot.GetWidth(), shot.GetHeight());
                var square = shot.GetRegion(new Rect2I((shot.GetWidth() - side) / 2, (shot.GetHeight() - side) / 2, side, side));
                square.Resize(Size, Size);
                square.Convert(Image.Format.Rgb8);
                sheet.BlitRect(square, new Rect2I(0, 0, Size, Size), new Vector2I(v * Size, 0));
            }
            var path = System.IO.Path.Combine(_folder, name + ".png");
            if (sheet.SavePng(path) == Error.Ok) saved++;
            else GD.PushError($"pose shots: couldn't save {path}");
        }
        GD.Print($"pose shots: {saved} picture(s) in {_folder}");
        GetTree().Quit(saved > 0 ? 0 : 1);
    }
}
