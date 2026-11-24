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
    private static readonly Color StaggerColor = new(0.85f, 0.30f, 0.95f);
    private static readonly Color RecoilColor = new(0.55f, 0.55f, 0.60f);
    private static readonly Color PlayerColor = new(0.95f, 0.65f, 0.15f);
    private static readonly Color DummyColor = new(0.55f, 0.15f, 0.15f);

    private const string Legend =
        "[color=#ffd11a]■ startup[/color]   [color=#f2261a]■ active (hit)[/color]   [color=#33ff59]■ perfect parry[/color]   " +
        "[color=#33bfff]■ parry[/color]   [color=#4059f2]■ recovery[/color]   [color=#ffffff]■ hit[/color]   " +
        "[color=#d94df2]■ staggered (open!)[/color]   [color=#8c8c99]■ recoil / recovering from a parry[/color]";

    /// <summary>
    /// Debug floating text over the capsules ("-6", "Blocked!", "Parried!"). A playtest aid,
    /// not the real game's UI: set this to false to start with it off, or press F2 in game.
    /// </summary>
    private const bool FloatingTextOnByDefault = true;
    private const float FloatSeconds = 0.9f;
    private const float FloatRise = 0.8f; // metres over its life

    private sealed class FloatText
    {
        public Label3D Label = null!;
        public float Age;
        public Vector3 Start;
    }

    private readonly List<FloatText> _floats = new();
    private bool _floatingText = FloatingTextOnByDefault;

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
        ClearFloats();
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

        _event.Text = "Walk with A/D or the left stick. Fight with your slot buttons. Tab / Start opens the loadout. T / Select makes the dummy stand still.";
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

        if (command == Command.DummyToggle)
        {
            _dummy.Attacks = !_dummy.Attacks;
            _event.Text = _dummy.Attacks ? "The dummy attacks again." : "The dummy stands still. Hit it as much as you like.";
            RefreshMenu();
            return;
        }
        // On a pad, Start after a KO starts the next duel instead of opening the loadout.
        if (command == Command.Menu && e is InputEventJoypadButton && _duel.IsOver && !_menuOpen) command = Command.Restart;
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
        if (command == Command.FloatingText)
        {
            _floatingText = !_floatingText;
            if (!_floatingText) ClearFloats();
            return;
        }

        if (_menuOpen)
        {
            int rows = _starters.Count + 2; // techniques, the dummy row, the restart row
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
        if (_menuRow == _starters.Count + 1)
        {
            NewDuel();
            _menuOpen = false;
            _event.Text = "New duel.";
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
        var dummyLine = $"Dummy: {(_dummy.Attacks ? "attacks with 罗汉拳" : "stands still")}  (any button, ←/→, Select or T to change)";
        sb.Append(_menuRow == _starters.Count ? $"[color=#ffd11a]▶ {dummyLine}[/color]\n" : $"   {dummyLine}\n");
        var restartLine = "Restart the duel  (press any button)";
        sb.Append(_menuRow == _starters.Count + 1 ? $"[color=#ffd11a]▶ {restartLine}[/color]\n" : $"   {restartLine}\n");

        sb.Append("\n[b]Your buttons[/b]\n");
        foreach (var s in Slots.All)
        {
            var t = _playerLoadout[s];
            sb.Append($"{Slots.Label(s),-3}  {(t == null ? "—" : t.Name),-22}  [color=#aaaaaa]{Controls.LabelOf(s)}[/color]\n");
        }
        sb.Append("\nTab, Esc or the pad's Start button closes this screen.");
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
            if (hit.Landed)
            {
                bool comboing = hit.Attacker == Side.One ? twoWasStunned : oneWasStunned;
                _combo = comboing && _comboBy == hit.Attacker ? _combo + 1 : 1;
                _comboBy = hit.Attacker;
            }
            else
            {
                _combo = 0;
            }
            _event.Text = Describe(hit);
            if (_floatingText) FloatsFor(hit);
        }

        if (_duel.IsOver)
        {
            var winner = _duel.Two.IsDown ? "You win" : "The dummy wins";
            _event.Text += $"\n[font_size=28][b]{winner}![/b][/font_size]  Press Enter, or Start on the pad, to fight again.";
        }
    }

    private string Describe(HitEvent hit)
    {
        bool mine = hit.Attacker == Side.One;
        var name = hit.Technique.Name;

        switch (hit.Parry)
        {
            case ParryOutcome.PerfectParry:
                return mine
                    ? $"[color=#33ff59][b]PERFECT PARRY![/b][/color] The dummy caught your {name}. You're staggered."
                    : $"[color=#33ff59][b]PERFECT PARRY![/b][/color] You stopped {name}; the dummy is staggered. Hit it now!";
            case ParryOutcome.Parried:
                return mine
                    ? $"[color=#33bfff][b]Blocked.[/b][/color] The dummy blocked your {name}; your string can carry on."
                    : $"[color=#33bfff][b]Blocked[/b][/color] {name}: no damage, but its string carries on, so tap again for the next hit. " +
                      $"({PerfectMiss(hit.DefenderTechnique!, hit.DefenderMoveFrame)} frame(s) late for a perfect parry.)";
        }

        switch (hit.Clash)
        {
            case ClashOutcome.BothRecoil:
                return "[color=#ffd11a][b]CLASH![/b][/color] Same weight: both of you bounce off.";
            case ClashOutcome.PerfectClash:
                return mine
                    ? "[color=#33ff59][b]PERFECT CLASH![/b][/color] You broke the dummy's wind-up. It's wide open!"
                    : "[color=#ff6666][b]PERFECT CLASH[/b][/color] by the dummy: your wind-up was broken.";
            case ClashOutcome.TargetWins:
                return mine
                    ? $"[color=#ff6666][b]Clash lost.[/b][/color] Your {name} was lighter and got knocked aside."
                    : $"[color=#ffd11a][b]Clash won![/b][/color] Your heavier move knocked {name} aside.";
        }

        var combo = _combo > 1 ? $"   [b]{_combo} hit combo[/b]" : "";
        var clash = hit.Clash == ClashOutcome.StrikerWins ? "[b]Clash won, breaking through![/b] " : "";
        if (mine)
            return $"[color=#ffb347]{clash}You hit with {name}: {hit.Damage} damage.[/color]{combo}";

        var line = $"[color=#ff6666]{(clash.Length > 0 ? "[b]Clash lost.[/b] " : "")}{name} hit you: {hit.Damage} damage.[/color]{combo}";
        return hit.DefenderTechnique != null && hit.DefenderTechnique.Category == Category.Huajin
            ? line + "\n" + MissedParry(hit.DefenderTechnique, hit.DefenderMoveFrame)
            : line;
    }

    private static int PerfectMiss(Technique parry, int frame) => frame - parry.StartupFrames - parry.PerfectParryFrames + 1;

    /// <summary>You were parrying but outside the window: say whether it was early or late, and by how much.</summary>
    private static string MissedParry(Technique parry, int frame)
    {
        int windowStart = parry.StartupFrames;
        int windowEnd = parry.StartupFrames + parry.ActiveFrames; // first frame after the window
        return frame < windowStart
            ? $"化绵掌 [b]too late[/b]: it was still winding up. Press {windowStart - frame} frame(s) earlier."
            : $"化绵掌 [b]too early[/b]: the window had closed. Press {frame - windowEnd + 1} frame(s) later.";
    }

    // ---------------------------------------------------------------- floating debug text

    private static readonly Color DamageOnYouColor = new(1.00f, 0.35f, 0.35f);
    private static readonly Color DamageOnDummyColor = new(1.00f, 0.80f, 0.30f);

    /// <summary>Text over the fighters for one strike: who took what.</summary>
    private void FloatsFor(HitEvent hit)
    {
        var attacker = hit.Attacker;
        var defender = hit.Defender;

        switch (hit.Parry)
        {
            case ParryOutcome.PerfectParry:
                Float(defender, "Parried!", ParryPerfectColor);
                Float(attacker, "Stagger!", StaggerColor);
                return;
            case ParryOutcome.Parried:
                Float(defender, "Blocked!", ParryColor);
                return;
        }

        switch (hit.Clash)
        {
            case ClashOutcome.BothRecoil:
                Float(attacker, "CLASH!", StartupColor);
                Float(defender, "CLASH!", StartupColor);
                return;
            case ClashOutcome.PerfectClash:
                Float(attacker, "Perfect clash!", ParryPerfectColor);
                Float(defender, "Stagger!", StaggerColor);
                return;
            case ClashOutcome.TargetWins:
                Float(defender, "CLASH!", StartupColor);
                Float(attacker, "Stagger!", StaggerColor);
                return;
            case ClashOutcome.StrikerWins:
                Float(attacker, "CLASH!", StartupColor);
                break;
        }

        if (hit.Landed)
        {
            Float(defender, $"-{hit.Damage}", defender == Side.One ? DamageOnYouColor : DamageOnDummyColor);
            if (hit.DefenderTechnique != null && hit.DefenderTechnique.Category == Category.Huajin)
                Float(defender, hit.DefenderMoveFrame < hit.DefenderTechnique.StartupFrames ? "Too late!" : "Too early!",
                    new Color(1, 1, 1));
        }
    }

    private void Float(Side side, string text, Color color)
    {
        var f = side == Side.One ? _duel.One : _duel.Two;
        // Stack new text above any that is still young over the same fighter.
        int young = _floats.Count(t => t.Age < 0.3f && Mathf.Abs(t.Start.X - f.PositionCm / 100f) < 0.01f);
        var label = new Label3D
        {
            Text = text,
            Modulate = color,
            OutlineModulate = new Color(0, 0, 0),
            OutlineSize = 10,
            FontSize = 64,
            PixelSize = 0.006f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };
        var start = new Vector3(f.PositionCm / 100f, 2.3f + young * 0.35f, 0.5f);
        label.Position = start;
        AddChild(label);
        _floats.Add(new FloatText { Label = label, Start = start });
    }

    public override void _Process(double delta)
    {
        for (int i = _floats.Count - 1; i >= 0; i--)
        {
            var t = _floats[i];
            t.Age += (float)delta;
            if (t.Age >= FloatSeconds)
            {
                t.Label.QueueFree();
                _floats.RemoveAt(i);
                continue;
            }
            float k = t.Age / FloatSeconds;
            t.Label.Position = t.Start + new Vector3(0, FloatRise * k, 0);
            var c = t.Label.Modulate;
            t.Label.Modulate = new Color(c.R, c.G, c.B, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            t.Label.OutlineModulate = new Color(0, 0, 0, t.Label.Modulate.A);
        }
    }

    private void ClearFloats()
    {
        foreach (var t in _floats) t.Label.QueueFree();
        _floats.Clear();
    }

    // ---------------------------------------------------------------- drawing

    private void Draw()
    {
        Place(_player, _playerReach, _duel.One, PlayerColor, +1);
        Place(_opponent, _opponentReach, _duel.Two, DummyColor, -1);

        _status.Text =
            $"[b]You[/b] {Bar(_duel.One)}  {Doing(_duel.One)}\n" +
            $"[b]Dummy[/b] {Bar(_duel.Two)}  {Doing(_duel.Two)}\n" +
            $"Distance {_duel.DistanceCm} cm   ·   dummy {(_dummy.Attacks ? "attacking" : "[color=#ffd11a]standing still[/color]")}   ·   tick {_duel.Tick}{(_slowMotion ? "   ·   [color=#ffd11a]half speed (F1)[/color]" : "")}";

        _pad.Text = Controls.PadStatus() + (_lastPadInput.Length > 0 ? $"   ·   last pad input: {_lastPadInput}" : "");
        _help.Text =
            "Walk: A/D, ←/→, D-pad or left stick  ·  Loadout: Tab / Esc / pad Start  ·  Dummy still/attack: T / pad Select  ·  Restart: Enter  ·  Half speed: F1  ·  Floating text: F2\n" +
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
            case FighterState.Hitstun: return HitstunColor;
            case FighterState.Stagger: return StaggerColor;
            case FighterState.Recoil: return RecoilColor;
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
