using System.Collections.Generic;
using System.Linq;
using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Feel;
using Shaolin36.Core.Fighters;
using Side = Shaolin36.Core.Combat.Side;

namespace Shaolin36.Game;

/// <summary>
/// The playable test: you against an opponent brain (or the old training dummy), on a square floor in full 3D.
/// This node only shows and drives the core: it turns button presses into slot presses and the
/// stick or WASD into lock-on-relative walking, steps the <see cref="Duel"/> once per 60 Hz physics tick
/// (through the <see cref="FeelClock"/>, which adds hit-stop), paints the capsules by state and keeps a
/// third-person camera behind you, locked on to the opponent. The feel kit's sparks, sounds and camera
/// moves are in ArenaFeel.cs.
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
    private static readonly Color QuickStepColor = new(0.70f, 1.00f, 0.85f);
    private static readonly Color InvincibleColor = new(0.85f, 1.00f, 1.00f);
    private static readonly Color PlayerColor = new(0.95f, 0.65f, 0.15f);
    private static readonly Color DummyColor = new(0.55f, 0.15f, 0.15f);

    private const string Legend =
        "[color=#ffd11a]■ startup[/color]   [color=#f2261a]■ active (hit)[/color]   [color=#33ff59]■ perfect parry[/color]   " +
        "[color=#33bfff]■ parry[/color]   [color=#4059f2]■ recovery[/color]   [color=#ffffff]■ hit[/color]   " +
        "[color=#d94df2]■ staggered (open!)[/color]   [color=#8c8c99]■ recoil / recovering from a parry[/color]   [color=#b3ffd9]■ quick step[/color]";

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
    private readonly BrainSheet _brains = null!;
    private Brain? _brain;
    /// <summary>Which opponent: an index into the brain sheet's rows, or the row count for the training dummy.</summary>
    private int _opponentMode;
    /// <summary>False: the opponent stands still and presses nothing (T / Select).</summary>
    private bool _opponentActive = true;
    private int _duelNumber;
    private readonly DoubleTap _doubleTap = null!;

    private Duel _duel = null!;
    private readonly FeelSheet _feel = null!;
    private FeelClock _clock = null!;
    private bool _feelOn = true;
    private byte _pending; // slot presses since the last tick
    private bool _menuOpen;
    private int _menuRow;
    private bool _slowMotion;
    /// <summary>Sim ticks still to play at half speed after your just dodge (feel.csv just_dodge slow).</summary>
    private int _dodgeSlowTicks;
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
    /// <param name="feel">The feel sheet: hit-stop, sparks, sound and camera for each kind of strike.</param>
    /// <param name="brains">The brain sheet: how each opponent plays. The arena starts against the "monk" row.</param>
    public Arena(Rules rules, IReadOnlyList<Technique> techniques, FeelSheet feel, BrainSheet brains)
    {
        _rules = rules;
        _feel = feel;
        _brains = brains;
        _opponentMode = System.Math.Max(0, brains.Styles.ToList().FindIndex(b => b.Id == "monk"));
        _book = new TechniqueBook(techniques);
        // What can sit on a slot: everything that isn't a later hit of a string.
        var followUps = new HashSet<string>(techniques.Where(t => t.HasNext).Select(t => t.Next));
        _starters = techniques.Where(t => !followUps.Contains(t.Id)).ToList();

        _playerLoadout = new Loadout(_book);
        _playerLoadout.Assign(Slot.Square, Find("luohan_quan_1"));
        _playerLoadout.Assign(Slot.L1, Find("timed_parry"));
        _playerLoadout.Assign(Slot.Triangle, Find("tiesha_zhang"));
        _playerLoadout.Assign(Slot.Cross, Find("qinggong_dash"));

        // The opponent gets the same starting techniques as you: 罗汉拳 on □, 铁砂掌 on △, 轻功 dash on ✕, 化绵掌 on L1.
        // (The training dummy only ever presses □.)
        _dummyLoadout = new Loadout(_book);
        _dummyLoadout.Assign(Slot.Square, Find("luohan_quan_1"));
        _dummyLoadout.Assign(Slot.L1, Find("timed_parry"));
        _dummyLoadout.Assign(Slot.Triangle, Find("tiesha_zhang"));
        _dummyLoadout.Assign(Slot.Cross, Find("qinggong_dash"));
        _dummy = TrainingDummy.FromRules(rules, Slot.Square);
        _doubleTap = new DoubleTap(rules.QuickStepTapWindow);

        NewDuel();
    }

    /// <summary>Godot needs this; the arena is always built in code with the constructor above.</summary>
    public Arena() { }

    private Technique Find(string id) =>
        _book.Find(id) ?? throw new KeyNotFoundException($"techniques.csv has no '{id}', which the playtest needs");

    private void NewDuel()
    {
        _lean[0] = _lean[1] = 0;
        _dodgeSlowTicks = 0;
        _cameraPlaced = false;
        _doubleTap?.Reset();
        _duel = Duel.Start(_rules, _playerLoadout, _dummyLoadout);
        // A new seed each duel, counted up from 1: the brain's choices change from duel to duel but every run is repeatable.
        _duelNumber++;
        _brain = UsingDummy ? null : new Brain(_brains.Styles[_opponentMode], _duelNumber) { Active = _opponentActive };
        _dummy.Attacks = _opponentActive;
        _clock = new FeelClock(_duel, _feel) { HitStopOn = _feelOn };
        _combo = 0;
        _comboBy = null;
        ClearFloats();
    }

    private bool UsingDummy => _opponentMode >= _brains.Styles.Count;

    /// <summary>The opponent's name for the screen: the brain row's name, or "Training dummy".</summary>
    private string OpponentName => UsingDummy ? "Training dummy" : _brains.Styles[_opponentMode].Name;

    /// <summary>This tick's buttons and stick for the opponent. Not called during hit-stop, so its timers stay in step with the fight.</summary>
    private BrainInput OpponentInput()
    {
        if (_brain != null) return _brain.Think(_duel, Side.Two);
        return new BrainInput(_dummy.Think(_duel.Two), MoveInput.None);
    }

    /// <summary>Switch to the next opponent (each brain row, then the training dummy) and start a new duel.</summary>
    private void NextOpponent(int by)
    {
        int count = _brains.Styles.Count + 1;
        _opponentMode = (_opponentMode + by + count) % count;
        NewDuel();
        _event.Text = $"New duel against: {OpponentName}.";
    }

    private void ToggleOpponent()
    {
        _opponentActive = !_opponentActive;
        _dummy.Attacks = _opponentActive;
        if (_brain != null) _brain.Active = _opponentActive;
    }

    // ---------------------------------------------------------------- headless check

    /// <summary>
    /// Runs the duel for <paramref name="ticks"/> ticks with no screen, the player fighting
    /// back with a training dummy's presses against the opponent brain, hit-stop on. Returns how many hits landed, and how many
    /// ticks were hit-stop. Used by the boot check.
    /// </summary>
    public int Simulate(int ticks, out int frozenTicks)
    {
        var player = TrainingDummy.FromRules(_rules, Slot.Square);
        frozenTicks = 0;
        for (int i = 0; i < ticks && !_duel.IsOver; i++)
        {
            if (_clock.IsFrozen)
            {
                frozenTicks++;
                _clock.Step(SlotInput.None, SlotInput.None);
                continue;
            }
            // Circle and close in now and then, so the boot check walks the floor in 3D too.
            var walk = i % 90 < 10 ? MoveInput.WalkForward : i % 90 < 30 ? MoveInput.WalkRight : MoveInput.None;
            if (i % 150 == 75) walk = MoveInput.Step(StepDirection.Left);
            var opponent = OpponentInput();
            _clock.Step(player.Think(_duel.One), opponent.Slots, walk, opponent.Move);
        }
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
        _camera = new Camera3D { Fov = 55 };
        AddChild(_camera);
        BuildSounds();
        BuildFloor();

        _player = Capsule("Player", PlayerColor);
        _opponent = Capsule("Opponent", DummyColor);
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

        _event.Text = "Walk any way with W/A/S/D or the left stick: the camera stays behind you, locked on to your opponent. It walks in, attacks, parries and steps aside like you do. Fight with your slot buttons. Tab / Start opens the loadout, where you can pick the opponent. T / Select makes it stand still.";
        RefreshMenu();
    }

    /// <summary>The square floor (arena_width each way), with a 1 m grid and a darker rim so movement reads at a glance.</summary>
    private void BuildFloor()
    {
        float size = _rules.ArenaWidth / 100f;
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(size + 2, size + 2) },
            Position = new Vector3(0, -0.01f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.19f, 0.16f) },
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(size, size) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.38f, 0.30f) },
        });
        var lineMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.36f, 0.30f, 0.24f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        int lines = (int)size;
        for (int i = 0; i <= lines; i++)
        {
            float at = -size / 2 + i * size / lines;
            bool centre = System.Math.Abs(at) < 0.01f;
            var w = centre ? 0.05f : 0.02f;
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(size, 0.004f, w) }, Position = new Vector3(0, 0.002f, at), MaterialOverride = lineMaterial });
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, 0.004f, size) }, Position = new Vector3(at, 0.002f, 0), MaterialOverride = lineMaterial });
        }
    }

    private MeshInstance3D Capsule(string name, Color color)
    {
        var m = new MeshInstance3D
        {
            Name = name,
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 2f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
        };
        // A "nose" on the front of the chest, so you can see which way each fighter faces.
        m.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.14f, 0.2f) },
            Position = new Vector3(0, 0.45f, -0.4f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) },
        });
        AddChild(m);
        return m;
    }

    /// <summary>The strike's reach shown on the floor: a wedge as wide as its hit angle, as long as its reach.</summary>
    private MeshInstance3D ReachBar()
    {
        var m = new MeshInstance3D
        {
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, 0.45f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            Visible = false,
        };
        AddChild(m);
        return m;
    }

    private readonly Dictionary<int, ArrayMesh> _wedges = new();

    /// <summary>A flat wedge of radius 1 m, <paramref name="degrees"/> wide, pointing along -Z (a node's forward).</summary>
    private ArrayMesh Wedge(int degrees)
    {
        if (_wedges.TryGetValue(degrees, out var mesh)) return mesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int segments = System.Math.Max(2, degrees / 6);
        float half = Mathf.DegToRad(degrees / 2f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = -half + 2 * half * i / segments;
            float a1 = -half + 2 * half * (i + 1) / segments;
            st.AddVertex(Vector3.Zero);
            st.AddVertex(new Vector3(Mathf.Sin(a0), 0, -Mathf.Cos(a0)));
            st.AddVertex(new Vector3(Mathf.Sin(a1), 0, -Mathf.Cos(a1)));
        }
        mesh = st.Commit();
        _wedges[degrees] = mesh;
        return mesh;
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
            ToggleOpponent();
            _event.Text = _opponentActive ? "Your opponent fights again." : "Your opponent stands still. Hit it as much as you like.";
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
        if (command == Command.FeelKit)
        {
            _feelOn = !_feelOn;
            _clock.HitStopOn = _feelOn;
            _event.Text = _feelOn
                ? "Feel kit [b]on[/b]: hit-stop, sparks, sound, camera shake and hit reactions. F3 turns it off to compare."
                : "Feel kit [b]off[/b]: no hit-stop, sparks, sound, camera moves or hit reactions. Knockback still happens (it's a rule). F3 turns it back on.";
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
            int rows = _starters.Count + 2; // techniques, the opponent row, the restart row
            if (command == Command.MenuUp) _menuRow = (_menuRow + rows - 1) % rows;
            else if (command == Command.MenuDown) _menuRow = (_menuRow + 1) % rows;
            else if (slot != null) PutOnSlot(slot.Value);
            else if ((command == Command.MenuLeft || command == Command.MenuRight) && _menuRow == _starters.Count)
                NextOpponent(command == Command.MenuLeft ? -1 : 1);
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
            NextOpponent(1);
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
        var what = UsingDummy ? "repeats 罗汉拳 where it stands" : "walks in, attacks, parries, steps aside";
        var dummyLine = $"Opponent: {OpponentName} ({what}){(_opponentActive ? "" : ", standing still")}  (any button or ←/→ to change and restart; Select or T: stand still)";
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
        bool halfSpeed = _slowMotion || _dodgeSlowTicks > 0;
        if (!_menuOpen && !(halfSpeed && _physicsFrame % 2 == 1))
        {
            if (_dodgeSlowTicks > 0) _dodgeSlowTicks--;
            Step();
        }
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
        bool oneWasExhausted = _duel.One.IsExhausted, twoWasExhausted = _duel.Two.IsExhausted;
        var oneMoveBefore = _duel.One.Technique;
        var twoMoveBefore = _duel.Two.Technique;
        var input = new SlotInput(_pending);
        _pending = 0;
        if (_floatingText && _duel.One.IsExhausted && PressedUnaffordable(input))
            Float(Side.One, "No stamina!", ExhaustedColor);

        // The opponent doesn't think during hit-stop, so its own timers stay in step with the fight.
        var opponent = _clock.IsFrozen ? BrainInput.None : OpponentInput();
        // Double-tapping one of the four directions asks for a quick step that way.
        var walk = Controls.WalkInput();
        var quickStep = _doubleTap.Update(DoubleTap.Cardinal(walk));
        if (_floatingText && quickStep != StepDirection.None && _duel.One.IsExhausted && !_duel.One.CanQuickStep && _duel.One.CanAct)
            Float(Side.One, "No stamina!", ExhaustedColor);
        var hits = _clock.Step(input, opponent.Slots, walk.WithQuickStep(quickStep), opponent.Move);
        if (_feelOn) foreach (var cue in _clock.Cues) PlayCue(cue);
        // Your own just dodge slows the screen for a moment (only the screen: the rules still run tick by tick).
        foreach (var cue in _clock.Cues)
            if (_feelOn && cue.Hit.JustDodged && cue.Hit.Defender == Side.One) _dodgeSlowTicks = System.Math.Max(_dodgeSlowTicks, cue.Row.SlowFrames);
        EscapeStarted(Side.One, oneWasStunned);
        EscapeStarted(Side.Two, twoWasStunned);
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

        EmpoweredStarted(Side.One, oneMoveBefore);
        EmpoweredStarted(Side.Two, twoMoveBefore);
        ExhaustionChanged(Side.One, oneWasExhausted);
        ExhaustionChanged(Side.Two, twoWasExhausted);

        if (_duel.IsOver)
        {
            var winner = _duel.Two.IsDown ? "You win" : "Your opponent wins";
            _event.Text += $"\n[font_size=28][b]{winner}![/b][/font_size]  Press Enter, or Start on the pad, to fight again.";
        }
    }

    /// <summary>Say so when a fighter dashes out of hitstun (a combo escape).</summary>
    private void EscapeStarted(Side side, bool wasStunned)
    {
        var f = _duel[side];
        if (!wasStunned || !f.EscapedHitstun || f.MoveFrame != 0) return;
        _event.Text = side == Side.One
            ? $"[color=#ccffff][b]Escaped![/b][/color] You dashed out of the combo ({f.Technique!.StaminaCost} stamina). No just dodge from an escape."
            : "[color=#ccffff][b]Your opponent escaped[/b][/color] your combo with its 轻功 dash.";
        if (_floatingText) Float(side, "Escape!", InvincibleColor);
    }

    /// <summary>True if a slot pressed this tick holds a move the player can't pay for.</summary>
    private bool PressedUnaffordable(SlotInput input)
    {
        foreach (var s in Slots.All)
        {
            var t = _playerLoadout[s];
            if (input.IsPressed(s) && t != null && !_duel.One.CanAfford(t)) return true;
        }
        return false;
    }

    /// <summary>Say so when a fighter starts the Qi-empowered version of a move (such as 铁砂掌 at 30+ Qi).</summary>
    private void EmpoweredStarted(Side side, Technique? before)
    {
        var f = side == Side.One ? _duel.One : _duel.Two;
        if (!f.Empowered || f.Technique == before || f.MoveFrame != 0) return;
        var t = f.Technique!;
        _event.Text = $"[color=#ffd640][b]Qi {t.Name}![/b][/color] {(side == Side.One ? "You spent" : "Your opponent spent")} {t.EmpowerQi} Qi: " +
                      $"{t.EmpowerDamagePct}% damage{(t.EmpowerArmor ? ", and hits won't stop it until it lands (hyper armor)" : "")}.";
        if (_floatingText)
        {
            Float(side, $"Qi {t.Name}!", QiColor);
            Float(side, $"-{t.EmpowerQi} Qi", QiColor);
        }
    }

    /// <summary>Say so when a fighter runs out of stamina or gets their breath back.</summary>
    private void ExhaustionChanged(Side side, bool wasExhausted)
    {
        var f = _duel[side];
        if (f.IsExhausted == wasExhausted || _duel.IsOver) return;
        bool you = side == Side.One;
        if (f.IsExhausted)
        {
            _event.Text += you
                ? "\n[color=#ff8c1a][b]You're exhausted![/b][/color] No moves (and no 化绵掌) until your stamina is back to " +
                  $"{_rules.ExhaustedUntil}. You walk slower and take {_rules.ExhaustedDamagePct - 100}% extra damage. Stand still to recover faster."
                : $"\n[color=#ff8c1a][b]Your opponent is exhausted![/b][/color] It can't attack or parry, and takes {_rules.ExhaustedDamagePct - 100}% extra damage. Punish it!";
            if (_floatingText) Float(side, "Exhausted!", ExhaustedColor);
        }
        else
        {
            _event.Text += you ? "\nYou have your breath back." : "\nYour opponent has its breath back.";
            if (_floatingText) Float(side, "Breath back", StaminaColor);
        }
    }

    private string Describe(HitEvent hit)
    {
        bool mine = hit.Attacker == Side.One;
        var name = hit.Technique.Name;

        if (hit.JustDodged)
            return mine
                ? $"[color=#ccffff][b]Just dodged.[/b][/color] Your opponent's 轻功 dash slipped your {name} (+{hit.DefenderQiGained} Qi to it)."
                : $"[color=#ccffff][b]JUST DODGE![/b][/color] {name} passed through your 轻功 dash. +{hit.DefenderQiGained} Qi.";

        switch (hit.Parry)
        {
            case ParryOutcome.PerfectParry:
                return mine
                    ? $"[color=#33ff59][b]PERFECT PARRY![/b][/color] Your opponent caught your {name}. You're staggered."
                    : $"[color=#33ff59][b]PERFECT PARRY![/b][/color] You stopped {name}; your opponent is staggered. Hit it now! " +
                      $"Your {hit.ParryEffect!.DefenderStaminaRefund} stamina came back and your opponent lost {hit.ParryEffect.AttackerStaminaLoss}.";
            case ParryOutcome.Parried:
                return mine
                    ? $"[color=#33bfff][b]Blocked.[/b][/color] Your opponent blocked your {name}; your string can carry on."
                    : $"[color=#33bfff][b]Blocked[/b][/color] {name}: no damage, but its string carries on, so tap again for the next hit " +
                      $"(each tap costs {hit.DefenderTechnique!.StaminaCost} stamina). " +
                      $"({PerfectMiss(hit.DefenderTechnique!, hit.DefenderMoveFrame)} frame(s) late for a perfect parry.)";
        }

        switch (hit.Clash)
        {
            case ClashOutcome.BothRecoil:
                return "[color=#ffd11a][b]CLASH![/b][/color] Same weight: both of you bounce off.";
            case ClashOutcome.PerfectClash:
                return mine
                    ? "[color=#33ff59][b]PERFECT CLASH![/b][/color] You broke your opponent's wind-up. It's wide open!"
                    : "[color=#ff6666][b]PERFECT CLASH[/b][/color] by your opponent: your wind-up was broken.";
            case ClashOutcome.TargetWins:
                return mine
                    ? $"[color=#ff6666][b]Clash lost.[/b][/color] Your {name} was lighter and got knocked aside."
                    : $"[color=#ffd11a][b]Clash won![/b][/color] Your heavier move knocked {name} aside.";
        }

        var combo = (_combo > 1 ? $"   [b]{_combo} hit combo[/b]" : "") +
                    (hit.DefenderExhausted ? $"   [color=#ff8c1a]exhausted: +{_rules.ExhaustedDamagePct - 100}% damage[/color]" : "");
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
    private static readonly Color StaminaColor = new(0.40f, 0.85f, 1.00f);
    private static readonly Color ExhaustedColor = new(1.00f, 0.55f, 0.10f);
    private static readonly Color QiColor = new(1.00f, 0.84f, 0.25f);
    /// <summary>Wind-up of a Qi-empowered move with hyper armor: deep gold instead of yellow.</summary>
    private static readonly Color ArmorColor = new(0.85f, 0.55f, 0.05f);

    /// <summary>Text over the fighters for one strike: who took what.</summary>
    private void FloatsFor(HitEvent hit)
    {
        var attacker = hit.Attacker;
        var defender = hit.Defender;

        if (hit.JustDodged)
        {
            Float(defender, "Just dodge!", InvincibleColor);
            if (hit.DefenderQiGained > 0) Float(defender, $"+{hit.DefenderQiGained} Qi", QiColor);
            return;
        }

        switch (hit.Parry)
        {
            case ParryOutcome.PerfectParry:
                Float(defender, "Parried!", ParryPerfectColor);
                Float(defender, $"+{hit.ParryEffect!.DefenderStaminaRefund} stamina", StaminaColor);
                Float(attacker, "Stagger!", StaggerColor);
                Float(attacker, $"-{hit.ParryEffect.AttackerStaminaLoss} stamina", StaminaColor);
                if (hit.DefenderQiGained > 0) Float(defender, $"+{hit.DefenderQiGained} Qi", QiColor);
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
                if (hit.AttackerQiGained > 0) Float(attacker, $"+{hit.AttackerQiGained} Qi", QiColor);
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
            Float(defender, $"-{hit.Damage}{(hit.Empowered ? $" (x{hit.Technique.EmpowerDamagePct / 100.0:0.#})" : "")}{(hit.DefenderExhausted ? " (exhausted)" : "")}",
                defender == Side.One ? DamageOnYouColor : DamageOnDummyColor);
            if (hit.DefenderArmored) Float(defender, "Armor!", QiColor);
            if (hit.DefenderTechnique != null && hit.DefenderTechnique.Category == Category.Huajin)
                Float(defender, hit.DefenderMoveFrame < hit.DefenderTechnique.StartupFrames ? "Too late!" : "Too early!",
                    new Color(1, 1, 1));
        }
    }

    private void Float(Side side, string text, Color color)
    {
        var f = side == Side.One ? _duel.One : _duel.Two;
        var at = Floor(f.Position);
        // Stack new text above any that is still young over the same fighter.
        int young = _floats.Count(t => t.Age < 0.3f && new Vector2(t.Start.X - at.X, t.Start.Z - at.Z).Length() < 0.01f);
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
        var start = at + new Vector3(0, 2.3f + young * 0.35f, 0);
        label.Position = start;
        AddChild(label);
        _floats.Add(new FloatText { Label = label, Start = start });
    }

    public override void _Process(double delta)
    {
        UpdateFeel((float)delta);
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
        Place(_player, _playerReach, Side.One, PlayerColor);
        Place(_opponent, _opponentReach, Side.Two, DummyColor);

        _status.Text =
            $"[b]You[/b]    HP {Bar(_duel.One)}  stamina {StaminaBar(_duel.One)}  Qi {QiBar(_duel.One)}  {Doing(_duel.One)}\n" +
            $"[b]Foe[/b]   HP {Bar(_duel.Two)}  stamina {StaminaBar(_duel.Two)}  Qi {QiBar(_duel.Two)}  {Doing(_duel.Two)}\n" +
            $"Distance {_duel.DistanceCm} cm   ·   {OpponentName}: {(_opponentActive ? Thinking() : "[color=#ffd11a]standing still[/color]")}   ·   tick {_duel.Tick}{(_clock.IsFrozen ? " [color=#ffd11a](hit-stop)[/color]" : "")}   ·   feel kit {(_feelOn ? "on" : "[color=#ffd11a]off[/color]")} (F3){(_slowMotion ? "   ·   [color=#ffd11a]half speed (F1)[/color]" : "")}";

        _pad.Text = Controls.PadStatus() + (_lastPadInput.Length > 0 ? $"   ·   last pad input: {_lastPadInput}" : "");
        _help.Text =
            "Walk any way: W/A/S/D, arrows, D-pad or left stick (forward = toward your opponent)  ·  Quick step: double-tap a direction  ·  轻功 dash: ✕ (hold a direction to aim it; none = straight in; also breaks out of a combo)  ·  Loadout: Tab / Esc / pad Start  ·  Opponent still/fight: T / pad Select  ·  Restart: Enter  ·  Half speed: F1  ·  Floating text: F2  ·  Feel kit: F3\n" +
            "Buttons: " + string.Join("   ", Slots.All.Where(s => _playerLoadout[s] != null)
                .Select(s => $"{Slots.Label(s)} {_playerLoadout[s]!.Name} ({Controls.LabelOf(s)})"));
    }

    /// <summary>A floor point in metres in the scene (core X and Z are the scene's X and Z).</summary>
    private static Vector3 Floor(FloorPoint p) =>
        new(p.X / (100f * FloorPoint.UnitsPerCm), 0, p.Z / (100f * FloorPoint.UnitsPerCm));

    /// <summary>The scene rotation (about Y) that points a node's forward (-Z) along <paramref name="facing"/>.</summary>
    private static float Yaw(FloorPoint facing) => Mathf.Atan2(-facing.X, -facing.Z);

    private void Place(MeshInstance3D body, MeshInstance3D reach, Side side, Color idle)
    {
        var f = _duel[side];
        var feet = Floor(f.Position);
        // Hit reaction: lean back from the feet, away from the way the fighter faces (see ArenaFeel.cs).
        float lean = Mathf.DegToRad(LeanDegrees(side));
        var basis = new Basis(Vector3.Up, Yaw(f.Facing)) * new Basis(Vector3.Right, lean);
        body.Transform = new Transform3D(basis, feet + basis * new Vector3(0, 1, 0));
        ((StandardMaterial3D)body.MaterialOverride).AlbedoColor = ColorOf(f, idle);

        var t = f.Technique;
        bool show = t != null && t.ReachCm > 0 && (f.State == FighterState.Startup || f.State == FighterState.Active);
        reach.Visible = show;
        if (!show) return;
        float length = t!.ReachCm / 100f;
        reach.Mesh = Wedge(t.HitAngle);
        reach.Transform = new Transform3D(new Basis(Vector3.Up, Yaw(f.Facing)).Scaled(new Vector3(length, 1, length)),
            feet + new Vector3(0, 0.03f, 0));
        ((StandardMaterial3D)reach.MaterialOverride).AlbedoColor =
            f.State == FighterState.Active ? new Color(ActiveColor, 0.6f) : new Color(StartupColor, 0.35f);
    }

    private static Color ColorOf(Fighter f, Color idle)
    {
        if (f.IsInvincible) return InvincibleColor;
        if (f.IsDashing) return QuickStepColor;
        switch (f.State)
        {
            case FighterState.Startup: return f.HasArmor ? ArmorColor : StartupColor;
            case FighterState.Active:
                if (f.Technique!.Category == Category.Huajin)
                    return f.Technique.IsInPerfectParry(f.MoveFrame) ? ParryPerfectColor : ParryColor;
                return ActiveColor;
            case FighterState.Recovery: return RecoveryColor;
            case FighterState.Hitstun: return HitstunColor;
            case FighterState.Stagger: return StaggerColor;
            case FighterState.Recoil: return RecoilColor;
            case FighterState.QuickStep: return QuickStepColor;
            default: return idle;
        }
    }

    private static string Bar(Fighter f)
    {
        const int width = 24;
        int filled = (int)System.Math.Round(width * (double)f.Hp / f.MaxHp);
        return $"[color=#5fd35f]{new string('█', filled)}[/color][color=#444444]{new string('█', width - filled)}[/color] {f.Hp}/{f.MaxHp}";
    }

    /// <summary>Stamina in blue; orange with EXHAUSTED and the refill mark while out of breath.</summary>
    private string StaminaBar(Fighter f)
    {
        const int width = 16;
        int filled = (int)System.Math.Round(width * (double)f.Stamina / f.MaxStamina);
        var color = f.IsExhausted ? "#ff8c1a" : "#66d9ff";
        var text = $"[color={color}]{new string('█', filled)}[/color][color=#444444]{new string('█', width - filled)}[/color] {f.Stamina}/{f.MaxStamina}";
        return f.IsExhausted ? text + $" [color=#ff8c1a][b]EXHAUSTED[/b] until {_rules.ExhaustedUntil}[/color]" : text;
    }

    /// <summary>Qi in gold. Nothing spends it yet: later 点穴 and 气功 techniques will.</summary>
    private static string QiBar(Fighter f)
    {
        const int width = 10;
        int filled = f.MaxQi == 0 ? 0 : (int)System.Math.Round(width * (double)f.Qi / f.MaxQi);
        return $"[color=#ffd640]{new string('█', filled)}[/color][color=#444444]{new string('█', width - filled)}[/color] {f.Qi}/{f.MaxQi}";
    }

    /// <summary>What the opponent brain is up to, in plain words.</summary>
    private string Thinking()
    {
        if (_brain == null) return "repeating 罗汉拳";
        switch (_brain.Intent)
        {
            case BrainIntent.Closing: return "closing in";
            case BrainIntent.Waiting: return "waiting for its moment";
            case BrainIntent.Circling: return "circling you";
            case BrainIntent.Attacking: return "attacking";
            case BrainIntent.Punishing: return "[color=#ff6666]punishing your opening[/color]";
            case BrainIntent.Parrying: return "[color=#33bfff]saw your strike, parrying[/color]";
            case BrainIntent.Guessing: return "[color=#33bfff]parrying on a guess[/color]";
            case BrainIntent.Dodging: return "saw your strike, stepping aside";
            case BrainIntent.Resting: return "[color=#ff8c1a]backing off to breathe[/color]";
            case BrainIntent.Escaping: return "[color=#ccffff]dashed out of your combo[/color]";
            default: return "still";
        }
    }

    private static string Doing(Fighter f)
    {
        if (f.State == FighterState.QuickStep)
            return $"quick step {f.QuickStepDirection.ToString().ToLowerInvariant()} · frame {f.QuickStepFrame + 1}/{f.QuickStepRules!.TotalFrames}";
        if (f.State.IsInMove())
        {
            var t = f.Technique!;
            var chain = f.InChainWindow ? "  [color=#ffd11a]chain now![/color]"
                : f.IsInvincible ? "  [color=#ccffff]invincible[/color]" : "";
            return $"{t.Name} · {f.State} · frame {f.MoveFrame + 1}/{t.TotalFrames}{chain}";
        }
        if (f.State.IsStunned()) return $"{f.State} · {f.StunFramesLeft} frames left";
        return f.IsDown ? "down" : "ready";
    }
}
