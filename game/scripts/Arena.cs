using System.Collections.Generic;
using System.Linq;
using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Side = Shaolin36.Core.Combat.Side;

namespace Shaolin36.Game;

/// <summary>
/// The first playable test (Stage 1 brief 5): you against a dummy that repeats its string.
/// This node only shows and drives the core: it turns button presses into slot presses,
/// steps the <see cref="Duel"/> once per 60 Hz physics tick, and paints the capsules by state.
/// </summary>
public partial class Arena : Node3D
{
    // Colours for each phase, so the timing can be read by eye.
    private static readonly Color StartupColor = new(1.00f, 0.82f, 0.10f);
    private static readonly Color ActiveColor = new(0.95f, 0.15f, 0.10f);
    private static readonly Color ParryPerfectColor = new(0.20f, 1.00f, 0.35f);
    private static readonly Color ParryColor = new(0.20f, 0.75f, 1.00f);
    private static readonly Color RecoveryColor = new(0.25f, 0.35f, 0.95f);
    private static readonly Color HitstunColor = new(1.00f, 1.00f, 1.00f);
    private static readonly Color PlayerColor = new(0.95f, 0.65f, 0.15f);
    private static readonly Color DummyColor = new(0.55f, 0.15f, 0.15f);

    private const string Legend =
        "[color=#ffd11a]■ startup[/color]   [color=#f2261a]■ active (hit)[/color]   [color=#33ff59]■ perfect parry[/color]   " +
        "[color=#33bfff]■ parry[/color]   [color=#4059f2]■ recovery[/color]   [color=#ffffff]■ hit[/color]";

    private readonly Rules _rules = null!;
    private readonly TechniqueBook _book = null!;
    private readonly List<Technique> _starters = new();
    private readonly Loadout _playerLoadout = null!;
    private readonly Loadout _dummyLoadout = null!;
    private readonly TrainingDummy _dummy = null!;

    private Duel _duel = null!;
    private byte _pending; // slot presses since the last tick
    private bool _menuOpen;
    private int _menuRow;
    private bool _slowMotion;
    private int _physicsFrame;
    private int _combo;
    private Side? _comboBy;

    private MeshInstance3D _player = null!, _opponent = null!;
    private MeshInstance3D _playerReach = null!, _opponentReach = null!;
    private RichTextLabel _status = null!, _event = null!, _menu = null!;
    private Label _help = null!, _pad = null!;
    private ColorRect _menuBack = null!;
    private string _lastPadInput = "";

    /// <param name="techniques">Every technique from the sheet.</param>
    public Arena(Rules rules, IReadOnlyList<Technique> techniques)
    {
        _rules = rules;
        _book = new TechniqueBook(techniques);
        // What can sit on a slot: everything that isn't a later hit of a string.
        var followUps = new HashSet<string>(techniques.Where(t => t.HasNext).Select(t => t.Next));
        _starters = techniques.Where(t => !followUps.Contains(t.Id)).ToList();

        _playerLoadout = new Loadout(_book);
        _playerLoadout.Assign(Slot.Square, Find("luohan_quan_1"));
        _playerLoadout.Assign(Slot.L1, Find("timed_parry"));

        _dummyLoadout = new Loadout(_book);
        _dummyLoadout.Assign(Slot.Square, Find("luohan_quan_1"));
        _dummy = TrainingDummy.FromRules(rules, Slot.Square);

        NewDuel();
    }

    /// <summary>Godot needs this; the arena is always built in code with the constructor above.</summary>
    public Arena() { }

    private Technique Find(string id) =>
        _book.Find(id) ?? throw new KeyNotFoundException($"techniques.csv has no '{id}', which the playtest needs");

    private void NewDuel()
    {
        _duel = Duel.Start(_rules, _playerLoadout, _dummyLoadout);
        _combo = 0;
        _comboBy = null;
    }

    // ---------------------------------------------------------------- headless check

    /// <summary>
    /// Runs the duel for <paramref name="ticks"/> ticks with no screen, the player fighting
    /// back with its own dummy brain. Returns how many hits landed. Used by the boot check.
    /// </summary>
    public int Simulate(int ticks)
    {
        var player = TrainingDummy.FromRules(_rules, Slot.Square);
        for (int i = 0; i < ticks && !_duel.IsOver; i++)
            _duel.Step(player.Think(_duel.One), _dummy.Think(_duel.Two), i % 90 < 10 ? 1 : 0, 0);
        return _duel.Hits.Count;
    }

    // ---------------------------------------------------------------- scene

    public override void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.16f, 0.17f, 0.20f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.6f, 0.6f, 0.65f),
            },
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -30, 0), ShadowEnabled = true });
        AddChild(new Camera3D { Position = new Vector3(0, 1.7f, 6.5f), RotationDegrees = new Vector3(-8, 0, 0), Fov = 50 });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(_rules.ArenaWidth / 100f + 2, 6) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.38f, 0.30f) },
        });

        _player = Capsule("Player", PlayerColor);
        _opponent = Capsule("Dummy", DummyColor);
        _playerReach = ReachBar();
        _opponentReach = ReachBar();

        var layer = new CanvasLayer();
        AddChild(layer);
        // A font with Chinese and the □△○✕ symbols: Windows' Microsoft YaHei, with fallbacks for other systems.
        var font = new SystemFont
        {
            FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Segoe UI Symbol", "sans-serif" },
            AllowSystemFallback = true,
        };
        var hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFont = font, DefaultFontSize = 18 } };
        hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(hud);
        _status = Rich(new Vector2(16, 12), new Vector2(1100, 90));
        _event = Rich(new Vector2(16, 100), new Vector2(1100, 70));
        _pad = new Label { Position = new Vector2(16, 170), Modulate = new Color(1, 1, 1, 0.7f) };
        _help = new Label
        {
            AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 16, OffsetRight = -16, OffsetTop = -64, OffsetBottom = -8,
            Modulate = new Color(1, 1, 1, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        var legend = new RichTextLabel
        {
            AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 16, OffsetRight = -16, OffsetTop = -96, OffsetBottom = -66,
            BbcodeEnabled = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = Legend,
        };
        _menu = Rich(new Vector2(120, 140), new Vector2(900, 460));
        _menu.Visible = false;
        _menuBack = new ColorRect { Color = new Color(0, 0, 0, 0.8f), Position = new Vector2(100, 120), Size = new Vector2(940, 500), Visible = false };
        foreach (var c in new Control[] { _status, _event, _pad, _help, legend, _menuBack, _menu }) hud.AddChild(c);

        _event.Text = "Walk with A/D or the left stick. Fight with your slot buttons. Tab / Back opens the loadout.";
        RefreshMenu();
    }

    private MeshInstance3D Capsule(string name, Color color)
    {
        var m = new MeshInstance3D
        {
            Name = name,
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 2f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
        };
        AddChild(m);
        return m;
    }

    private MeshInstance3D ReachBar()
    {
        var m = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1, 0.04f, 0.5f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, 0.45f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Visible = false,
        };
        AddChild(m);
        return m;
    }

    private static RichTextLabel Rich(Vector2 position, Vector2 size) => new()
    {
        Position = position,
        Size = size,
        BbcodeEnabled = true,
        ScrollActive = false,
        FitContent = true,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    // ---------------------------------------------------------------- input

    public override void _Input(InputEvent e)
    {
        var described = Controls.Describe(e);
        if (described != null) _lastPadInput = described;

        var command = Controls.CommandOf(e);
        var slot = Controls.SlotPressed(e);

        if (command == Command.Menu)
        {
            _menuOpen = !_menuOpen;
            _pending = 0;
            RefreshMenu();
            return;
        }
        if (command == Command.Restart)
        {
            NewDuel();
            _pending = 0;
            _event.Text = "New duel.";
            return;
        }
        if (command == Command.SlowMotion)
        {
            _slowMotion = !_slowMotion;
            return;
        }

        if (_menuOpen)
        {
            int rows = _starters.Count + 1;
            if (command == Command.MenuUp) _menuRow = (_menuRow + rows - 1) % rows;
            else if (command == Command.MenuDown) _menuRow = (_menuRow + 1) % rows;
            else if (slot != null) PutOnSlot(slot.Value);
            else if ((command == Command.MenuLeft || command == Command.MenuRight) && _menuRow == _starters.Count)
                _dummy.Attacks = !_dummy.Attacks;
            RefreshMenu();
            return;
        }

        if (slot != null) _pending |= (byte)(1 << (int)slot.Value);
    }

    /// <summary>In the loadout screen, a slot button puts the highlighted technique on that slot.</summary>
    private void PutOnSlot(Slot target)
    {
        if (_menuRow == _starters.Count)
        {
            _dummy.Attacks = !_dummy.Attacks;
            return;
        }
        var technique = _starters[_menuRow];
        Slot? from = null;
        foreach (var s in Slots.All) if (_playerLoadout[s] == technique) from = s;
        if (from == target) return;
        if (from != null) _playerLoadout.Swap(from.Value, target); // whatever was on target moves to the old slot
        else _playerLoadout.Assign(target, technique);
    }

    private void RefreshMenu()
    {
        _menu.Visible = _menuOpen;
        _menuBack.Visible = _menuOpen;
        if (!_menuOpen) return;

        var sb = new System.Text.StringBuilder();
        sb.Append("[font_size=26][b]Loadout[/b][/font_size]   (the fight is paused)\n\n");
        sb.Append("Choose a technique with W/S, ↑/↓ or the D-pad, then press the button you want it on.\n");
        sb.Append("If something was already there, the two swap.\n\n");
        for (int i = 0; i < _starters.Count; i++)
        {
            var t = _starters[i];
            var on = Slots.All.Where(s => _playerLoadout[s] == t).Select(Slots.Label).ToList();
            var where = on.Count == 0 ? "not on a button" : "on " + string.Join(", ", on);
            var line = $"{t.Name}  ·  {t.Category}  ·  {where}";
            sb.Append(i == _menuRow ? $"[color=#ffd11a]▶ {line}[/color]\n" : $"   {line}\n");
        }
        var dummyLine = $"Dummy: {(_dummy.Attacks ? "attacks with 罗汉拳" : "stands still")}  (press any button or ←/→ to change)";
        sb.Append(_menuRow == _starters.Count ? $"[color=#ffd11a]▶ {dummyLine}[/color]\n" : $"   {dummyLine}\n");

        sb.Append("\n[b]Your buttons[/b]\n");
        foreach (var s in Slots.All)
        {
            var t = _playerLoadout[s];
            sb.Append($"{Slots.Label(s),-3}  {(t == null ? "—" : t.Name),-22}  [color=#aaaaaa]{Controls.LabelOf(s)}[/color]\n");
        }
        sb.Append("\nTab, Esc or the pad's Back button closes this screen.");
        _menu.Text = sb.ToString();
    }

    // ---------------------------------------------------------------- the fixed tick

    public override void _PhysicsProcess(double delta)
    {
        _physicsFrame++;
        if (!_menuOpen && !(_slowMotion && _physicsFrame % 2 == 1)) Step();
        Draw();
    }

    private void Step()
    {
        if (_duel.IsOver)
        {
            _pending = 0;
            return;
        }

        bool oneWasStunned = _duel.One.State == FighterState.Hitstun;
        bool twoWasStunned = _duel.Two.State == FighterState.Hitstun;
        var input = new SlotInput(_pending);
        _pending = 0;

        var hits = _duel.Step(input, _dummy.Think(_duel.Two), Controls.WalkDirection(), 0);
        foreach (var hit in hits)
        {
            bool comboing = hit.Attacker == Side.One ? twoWasStunned : oneWasStunned;
            _combo = comboing && _comboBy == hit.Attacker ? _combo + 1 : 1;
            _comboBy = hit.Attacker;
            _event.Text = Describe(hit);
        }

        if (_duel.IsOver)
        {
            var winner = _duel.Two.IsDown ? "You win" : "The dummy wins";
            _event.Text += $"\n[font_size=28][b]{winner}![/b][/font_size]  Press Enter or Start to fight again.";
        }
    }

    private string Describe(HitEvent hit)
    {
        var combo = _combo > 1 ? $"   [b]{_combo} hit combo[/b]" : "";
        if (hit.Attacker == Side.One)
            return $"[color=#ffb347]You hit with {hit.Technique.Name}: {hit.Damage} damage.[/color]{combo}";

        var line = $"[color=#ff6666]{hit.Technique.Name} hit you: {hit.Damage} damage.[/color]{combo}";
        return hit.DefenderTechnique != null && hit.DefenderTechnique.Category == Category.Huajin
            ? line + "\n" + ParryTiming(hit.DefenderTechnique, hit.DefenderMoveFrame)
            : line;
    }

    /// <summary>
    /// Parries don't stop hits until the next step of Stage 1, so for now this says how
    /// the timing was: perfect, parry, too early or too late, and by how many frames.
    /// </summary>
    private static string ParryTiming(Technique parry, int frame)
    {
        int windowStart = parry.StartupFrames;
        int windowEnd = parry.StartupFrames + parry.ActiveFrames; // first frame after the window
        switch (Parry.Resolve(parry, frame))
        {
            case ParryOutcome.PerfectParry:
                return $"[color=#33ff59][b]PERFECT PARRY timing[/b][/color] (frame {frame - windowStart + 1} of the {parry.PerfectParryFrames} perfect frames). Parrying doesn't block hits yet; that's the next step.";
            case ParryOutcome.Parried:
                return $"[color=#33bfff][b]PARRY timing[/b][/color] (frame {frame - windowStart + 1} of {parry.ActiveFrames}; {frame - windowStart - parry.PerfectParryFrames + 1} frame(s) too late for perfect). Parrying doesn't block hits yet; that's the next step.";
            default:
                return frame < windowStart
                    ? $"Parry [b]too late[/b]: it was still winding up. Press {windowStart - frame} frame(s) earlier."
                    : $"Parry [b]too early[/b]: the window had closed. Press {frame - windowEnd + 1} frame(s) later.";
        }
    }

    // ---------------------------------------------------------------- drawing

    private void Draw()
    {
        Place(_player, _playerReach, _duel.One, PlayerColor, +1);
        Place(_opponent, _opponentReach, _duel.Two, DummyColor, -1);

        _status.Text =
            $"[b]You[/b] {Bar(_duel.One)}  {Doing(_duel.One)}\n" +
            $"[b]Dummy[/b] {Bar(_duel.Two)}  {Doing(_duel.Two)}\n" +
            $"Distance {_duel.DistanceCm} cm   ·   tick {_duel.Tick}{(_slowMotion ? "   ·   [color=#ffd11a]half speed (F1)[/color]" : "")}";

        _pad.Text = Controls.PadStatus() + (_lastPadInput.Length > 0 ? $"   ·   last pad input: {_lastPadInput}" : "");
        _help.Text =
            "Walk: A/D, ←/→, D-pad or left stick  ·  Loadout: Tab / Esc / pad Back  ·  Restart: Enter / pad Start  ·  Half speed: F1\n" +
            "Buttons: " + string.Join("   ", Slots.All.Where(s => _playerLoadout[s] != null)
                .Select(s => $"{Slots.Label(s)} {_playerLoadout[s]!.Name} ({Controls.LabelOf(s)})"));
    }

    private void Place(MeshInstance3D body, MeshInstance3D reach, Fighter f, Color idle, int facing)
    {
        float x = f.PositionCm / 100f;
        body.Position = new Vector3(x, 1f, 0);
        ((StandardMaterial3D)body.MaterialOverride).AlbedoColor = ColorOf(f, idle);

        var t = f.Technique;
        bool show = t != null && t.ReachCm > 0 && (f.State == FighterState.Startup || f.State == FighterState.Active);
        reach.Visible = show;
        if (!show) return;
        float length = t!.ReachCm / 100f;
        reach.Scale = new Vector3(length, 1, 1);
        reach.Position = new Vector3(x + facing * length / 2, 0.03f, 0);
        ((StandardMaterial3D)reach.MaterialOverride).AlbedoColor =
            f.State == FighterState.Active ? new Color(ActiveColor, 0.6f) : new Color(StartupColor, 0.35f);
    }

    private static Color ColorOf(Fighter f, Color idle)
    {
        switch (f.State)
        {
            case FighterState.Startup: return StartupColor;
            case FighterState.Active:
                if (f.Technique!.Category == Category.Huajin)
                    return f.Technique.IsInPerfectParry(f.MoveFrame) ? ParryPerfectColor : ParryColor;
                return ActiveColor;
            case FighterState.Recovery: return RecoveryColor;
            case FighterState.Hitstun:
            case FighterState.Stagger:
            case FighterState.Recoil: return HitstunColor;
            default: return idle;
        }
    }

    private static string Bar(Fighter f)
    {
        const int width = 24;
        int filled = (int)System.Math.Round(width * (double)f.Hp / f.MaxHp);
        return $"[color=#5fd35f]{new string('█', filled)}[/color][color=#444444]{new string('█', width - filled)}[/color] {f.Hp}/{f.MaxHp}";
    }

    private static string Doing(Fighter f)
    {
        if (f.State.IsInMove())
        {
            var t = f.Technique!;
            var chain = f.InChainWindow ? "  [color=#ffd11a]chain now![/color]" : "";
            return $"{t.Name} · {f.State} · frame {f.MoveFrame + 1}/{t.TotalFrames}{chain}";
        }
        if (f.State.IsStunned()) return $"{f.State} · {f.StunFramesLeft} frames left";
        return f.IsDown ? "down" : "ready";
    }
}
