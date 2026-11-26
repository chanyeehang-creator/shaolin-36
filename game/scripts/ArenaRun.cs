using System.Collections.Generic;
using System.Linq;
using Godot;
using Shaolin36.Core;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Runs;

namespace Shaolin36.Game;

/// <summary>
/// The demo roguelike run (Chan's chamber-and-door design) on top of the arena: the screens (read a scroll, pick a button, the
/// chamber's enemy, the rewards, the end of the run), roaming a cleared chamber with a wooden dummy to try moves on and doors
/// to walk through, and the hand-off of each duel to and from the core's <see cref="Run"/>. The arena opens on the run; F4
/// switches to the practice arena and back.
/// </summary>
public partial class Arena
{
    private readonly Run _run = null!;
    /// <summary>True: the demo run. False: the practice arena (the old playtest, loadout menu and all).</summary>
    private bool _runMode = true;
    /// <summary>A run duel is under way (from the "fight" press until the KO has been acknowledged).</summary>
    private bool _runFighting;
    /// <summary>The highlighted row on a run screen.</summary>
    private int _runRow;
    /// <summary>A short line for the run screen: why the last press did nothing, or what was just picked.</summary>
    private string _runNote = "";
    /// <summary>Physics frames since the duel ended, so the button that landed the KO doesn't also skip the next screen.</summary>
    private int _overFrames;
    /// <summary>The door meshes standing in the chamber while you roam.</summary>
    private readonly List<Node3D> _doorNodes = new();
    /// <summary>The door you're standing in, or -1.</summary>
    private int _atDoor = -1;
    /// <summary>The □ prompt over each door, shown while you stand at it.</summary>
    private readonly List<Label3D> _doorPrompts = new();
    /// <summary>Roaming: which way the free camera looks across the floor, in radians (0 = +X, toward the doors).</summary>
    private float _roamYaw;
    /// <summary>Roaming: how fast the free camera turns at full stick, in radians per second.</summary>
    private const float RoamTurnPerSecond = 2.4f;

    /// <summary>Frames after a KO before a press goes on (0.75 s).</summary>
    private const int KoPause = 45;
    /// <summary>The wall the doors stand along: this far along X from the centre, in cm.</summary>
    private const int DoorWallCm = 600;
    /// <summary>Space between doors along the wall, in cm.</summary>
    private const int DoorGapCm = 260;
    /// <summary>Come within this many cm of a door and its □ prompt shows: □ then goes through it.</summary>
    private const int DoorReachCm = 160;
    /// <summary>The wooden dummy's HP: it never goes down.</summary>
    private const int DummyHp = 99999;

    private bool RunFighting => _runMode && _runFighting;

    /// <summary>Walking about a chamber with no fight on: the wooden dummy and the doors.</summary>
    private bool RunRoaming => _runMode && !_runFighting && _run.Phase == RunPhase.Roam;

    /// <summary>A run screen covers the floor and the sim is paused.</summary>
    private bool RunScreenUp => _runMode && !_runFighting && !RunRoaming;

    private Loadout PlayerLoadout => _runMode ? _run.Player : _playerLoadout;

    private BrainStyle RunBrainStyle =>
        _brains.Find(_run.Enemy!.BrainId) ?? throw new KeyNotFoundException($"brains.csv has no '{_run.Enemy.BrainId}'");

    private void ToggleRunMode()
    {
        if (RunFighting && !_duel.IsOver && !_menuOpen)
        {
            _event.Text = "Finish the fight first (or pause with Tab / Start and press Enter to give the run up).";
            return;
        }
        _runMode = !_runMode;
        _runFighting = false;
        _menuOpen = false;
        _pending = 0;
        _runRow = 0;
        _runNote = "";
        NewDuel();
        RefreshDoors();
        _event.Text = _runMode ? "Back to the demo run." : "Practice arena: Tab / Start for the loadout menu. F4 goes back to the demo run.";
        RefreshMenu();
    }

    /// <summary>
    /// Handles a press in run mode. True if it was used (or swallowed); false lets the arena treat it as a fight press or
    /// one of the F-key toggles.
    /// </summary>
    private bool RunInput(InputEvent e, Command command, Slot? slot)
    {
        // The F1 / F2 / F3 toggles work everywhere.
        if (command == Command.SlowMotion || command == Command.FloatingText || command == Command.FeelKit) return false;

        if (_runFighting || RunRoaming)
        {
            if (_runFighting && _duel.IsOver)
            {
                bool press = slot != null || command == Command.Restart || command == Command.Menu;
                if (press && _overFrames >= KoPause) FinishRunFight();
                return true;
            }
            if (command == Command.Menu)
            {
                _menuOpen = !_menuOpen;
                _pending = 0;
                RefreshMenu();
                return true;
            }
            if (_menuOpen)
            {
                // Paused: Enter gives the run up and starts over.
                if (command == Command.Restart)
                {
                    _menuOpen = false;
                    _runFighting = false;
                    _run.Restart();
                    _runRow = 0;
                    _runNote = "You gave the run up. A new run:";
                    RefreshDoors();
                    RefreshMenu();
                }
                return true;
            }
            // At a door, □ goes through it instead of doing its move (Chan: the pad has no other button for it).
            if (RunRoaming && slot == Slot.Square && _atDoor >= 0)
            {
                GoThrough(_atDoor);
                return true;
            }
            if (RunRoaming && (command == Command.Restart || command == Command.DummyToggle))
            {
                if (_atDoor >= 0) GoThrough(_atDoor);
                else _event.Text = "Walk up to a door: when its □ shows, press □ to go through.";
                return true;
            }
            // No restarting a duel or freezing the enemy in a run.
            return command == Command.Restart || command == Command.DummyToggle;
        }

        int rows = RunRows();
        if (command == Command.MenuUp && rows > 0) _runRow = (_runRow + rows - 1) % rows;
        else if (command == Command.MenuDown && rows > 0) _runRow = (_runRow + 1) % rows;
        else if (slot != null || command == Command.Restart || (command == Command.Menu && e is InputEventJoypadButton)) Confirm(slot);
        RefreshMenu();
        return true;
    }

    /// <summary>How many rows the current run screen lets you pick from.</summary>
    private int RunRows() => _run.Phase switch
    {
        RunPhase.ChooseStarter => _run.Starters.Count,
        RunPhase.Reward => _run.Offers.Count,
        _ => 0,
    };

    /// <summary>A button (or Enter) on a run screen.</summary>
    private void Confirm(Slot? slot)
    {
        _runNote = "";
        switch (_run.Phase)
        {
            case RunPhase.ChooseStarter:
                var starter = _run.Starters[_runRow];
                _run.ChooseStarter(_runRow);
                _runNote = $"You read the scroll of {starter.Name}.";
                break;
            case RunPhase.PlaceMove:
                if (slot == null)
                {
                    _runNote = "[color=#ffd11a]Press the button you want it on.[/color]";
                    return;
                }
                var move = _run.Pending!;
                if (!_run.Place(slot.Value))
                {
                    _runNote = $"[color=#ff6666]{Slots.Label(slot.Value)} already has {_run.Player[slot.Value]!.Name}: a move is never replaced. Pick an empty button.[/color]";
                    return;
                }
                _runNote = $"{move.Name} is on {Slots.Label(slot.Value)}.";
                break;
            case RunPhase.Fight:
                StartRunFight();
                return;
            case RunPhase.Reward:
                var offer = _run.Offers[_runRow];
                var said = OfferText(offer);
                _run.Choose(_runRow);
                _runNote = $"You took: {said}.";
                break;
            case RunPhase.Victory:
            case RunPhase.Defeat:
                _run.Restart();
                _runNote = "A new run:";
                break;
        }
        _runRow = 0;
        if (_run.Phase == RunPhase.Roam) StartRoam();
    }

    // ------------------------------------------------------------ roaming and doors

    /// <summary>Into a cleared chamber (or chamber 0): the doors and free walking with the camera (no dummy, Chan).</summary>
    private void StartRoam()
    {
        _menuOpen = false;
        _pending = 0;
        _atDoor = -1;
        NewDuel();
        RefreshDoors();
        _event.Text = (_runNote.Length > 0 ? _runNote + "\n" : "") + (_run.Chamber == 0
            ? "Chamber 0: no enemy here. Walk to the door you want (turn the camera with the right stick or Z / C); when its □ shows, press □ to go through."
            : $"Chamber {_run.Chamber} cleared. Walk to the door you want (turn the camera with the right stick or Z / C); when its □ shows, press □ to go through.");
        RefreshMenu();
    }

    /// <summary>
    /// The chamber with no fight: you (HP and Qi as they are; nothing done here carries over). The duel needs a second fighter, so
    /// there is one, but the duel is set to roaming (no hits, no bumping, no lock-on) and it isn't drawn.
    /// </summary>
    private Duel RoamDuel()
    {
        var dummy = new Loadout(_book);
        return Duel.Start(_rules, _run.Player, dummy, _run.PlayerStats, new FighterStats(DummyHp, DummyHp, _run.Sheet.EnemyStamina, 0, 0));
    }

    private void GoThrough(int door)
    {
        var d = _run.Doors[door];
        _run.EnterDoor(door);
        _runNote = $"Through the {DoorName(d)} door.";
        _runRow = 0;
        RefreshDoors();
        RefreshMenu();
    }

    /// <summary>Build the door meshes for the doors on offer while roaming; clear them otherwise.</summary>
    private void RefreshDoors()
    {
        foreach (var n in _doorNodes) n.QueueFree();
        _doorNodes.Clear();
        _doorPrompts.Clear();
        _atDoor = -1;
        if (!RunRoaming || !IsInsideTree()) return;
        for (int i = 0; i < _run.Doors.Count; i++)
        {
            var d = _run.Doors[i];
            var at = DoorPoint(i);
            var color = RarityColor(d.Rarity);
            var door = new Node3D { Position = new Vector3(at.X / (100f * FloorPoint.UnitsPerCm), 0, at.Z / (100f * FloorPoint.UnitsPerCm)) };
            var mat = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color * 0.35f };
            // Two posts and a lintel, facing back toward the middle of the floor.
            door.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.25f, 2.6f, 0.25f) }, Position = new Vector3(0, 1.3f, -0.8f), MaterialOverride = mat });
            door.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.25f, 2.6f, 0.25f) }, Position = new Vector3(0, 1.3f, 0.8f), MaterialOverride = mat });
            door.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.25f, 0.3f, 1.85f) }, Position = new Vector3(0, 2.6f, 0), MaterialOverride = mat });
            // The way in: a glowing pad on the floor.
            door.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.02f, 1.6f) },
                Position = new Vector3(-0.6f, 0.01f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(color, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            });
            door.AddChild(new Label3D
            {
                Text = $"{RarityName(d.Rarity)}\n{DoorName(d)}",
                Position = new Vector3(0, 3.1f, 0),
                Modulate = color,
                OutlineModulate = new Color(0, 0, 0),
                OutlineSize = 10,
                FontSize = 56,
                Font = _font,
                PixelSize = 0.006f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
            });
            // The □ button: shows over the door while you stand at it.
            var prompt = new Label3D
            {
                Text = "□  enter",
                Position = new Vector3(-0.6f, 1.6f, 0),
                Modulate = new Color(1, 1, 1),
                OutlineModulate = new Color(0, 0, 0),
                OutlineSize = 14,
                FontSize = 96,
                Font = _font,
                PixelSize = 0.006f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Visible = false,
            };
            door.AddChild(prompt);
            _doorPrompts.Add(prompt);
            AddChild(door);
            _doorNodes.Add(door);
        }
    }

    /// <summary>Where door <paramref name="i"/> stands: along the far wall, spread out and centred.</summary>
    private FloorPoint DoorPoint(int i)
    {
        int count = _run.Doors.Count;
        int z = (int)((i - (count - 1) / 2.0) * DoorGapCm);
        return FloorPoint.FromCm(DoorWallCm, z);
    }

    /// <summary>Each roaming tick: which door you stand in, and say what's behind it when that changes.</summary>
    private void UpdateDoorHint()
    {
        int at = -1;
        for (int i = 0; i < _run.Doors.Count; i++)
            if (FloorPoint.DistanceCm(_duel.One.Position, DoorPoint(i)) <= DoorReachCm) at = i;
        if (at == _atDoor) return;
        _atDoor = at;
        for (int i = 0; i < _doorPrompts.Count; i++) _doorPrompts[i].Visible = i == at;
        if (at < 0) return;
        var d = _run.Doors[at];
        _event.Text = $"[color=#{RarityColor(d.Rarity).ToHtml(false)}][b]{RarityName(d.Rarity)} door: {DoorName(d)}[/b][/color] to chamber {_run.Chamber + 1}. {DoorWhat(d)}\n[b]Press □ ({Controls.LabelOf(Slot.Square)}) to go through.[/b]";
    }

    /// <summary>Roaming starts facing the doors, with the camera behind you.</summary>
    private void StartRoamFacing()
    {
        _roamYaw = 0;
        _duel.One.Facing = new FloorPoint(1, 0);
    }

    /// <summary>The way the free camera looks across the floor, as a unit vector in the scene (Y = 0).</summary>
    private Vector3 RoamForward => new(Mathf.Cos(_roamYaw), 0, Mathf.Sin(_roamYaw));

    /// <summary>
    /// Roaming: turn the stick (relative to the camera) into the way you face, then walk straight ahead that fast. A quick step
    /// goes the way you turn to, so it is always a forward one. You only turn while free (a move keeps the way it started).
    /// </summary>
    private void RoamWalk(ref MoveInput walk, ref StepDirection quickStep)
    {
        if (!walk.IsWalking && quickStep == StepDirection.None) return;
        var f = RoamForward;
        var r = new Vector3(-f.Z, 0, f.X);
        var way = f * walk.Forward + r * walk.Right;
        if (quickStep != StepDirection.None && way.LengthSquared() < 0.0001f)
        {
            way = quickStep switch
            {
                StepDirection.Forward => f,
                StepDirection.Back => -f,
                StepDirection.Right => r,
                _ => -r,
            };
        }
        if (_duel.One.CanAct && way.LengthSquared() > 0.0001f)
            _duel.One.Facing = new FloorPoint(Mathf.RoundToInt(way.X * 1000), Mathf.RoundToInt(way.Z * 1000));
        int tilt = Mathf.RoundToInt(Mathf.Min(MoveInput.Full, Mathf.Sqrt((float)walk.Forward * walk.Forward + (float)walk.Right * walk.Right)));
        walk = new MoveInput(tilt, 0);
        if (quickStep != StepDirection.None) quickStep = StepDirection.Forward;
    }

    /// <summary>The free camera while roaming: behind you along its own yaw (right stick or Z / C turns it), looking past you.</summary>
    private void RoamCamera(float delta)
    {
        _roamYaw += Controls.CameraTurnInput() * RoamTurnPerSecond * delta;
        var me = Floor(_duel.One.Position);
        var f = RoamForward;
        var spot = me - f * CameraBack + Vector3.Up * CameraHeight;
        var look = me + f * 3f + Vector3.Up * CameraLookHeight;
        float k = _cameraPlaced ? 1 - Mathf.Exp(-CameraFollowPerSecond * delta) : 1;
        _cameraBase = _cameraBase.Lerp(spot, k);
        _cameraLook = _cameraLook.Lerp(look, k);
        _cameraPlaced = true;
    }

    private static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Common => new Color(0.85f, 0.85f, 0.85f),
        Rarity.Uncommon => new Color(0.35f, 0.95f, 0.45f),
        Rarity.Rare => new Color(0.35f, 0.65f, 1.00f),
        Rarity.SuperRare => new Color(1.00f, 0.80f, 0.20f),
        _ => new Color(1.00f, 0.30f, 0.25f),
    };

    private static string RarityName(Rarity r) => r switch
    {
        Rarity.Common => "Common",
        Rarity.Uncommon => "Uncommon",
        Rarity.Rare => "Rare",
        Rarity.SuperRare => "Super rare",
        _ => "Boss",
    };

    private string DoorName(Door d) => d.Kind switch
    {
        DoorKind.NewCategory => $"{CategoryName(d.Category!.Value)} (new)",
        DoorKind.SameCategory => $"more {CategoryName(d.Category!.Value)}",
        DoorKind.Medicine => "Medicine",
        DoorKind.SkillEnhance => "Skill Enhance",
        DoorKind.Elixirs => "Elixirs · mini boss",
        DoorKind.AncientScroll => "Ancient Scroll · mini boss",
        _ => "the Boss",
    };

    /// <summary>What winning behind a door gives.</summary>
    private string DoorWhat(Door d) => d.Kind switch
    {
        DoorKind.NewCategory => $"Win: learn one of {_run.Sheet.SkillChoices} {CategoryName(d.Category!.Value)} moves.",
        DoorKind.SameCategory => $"Win: learn another {CategoryName(d.Category!.Value)} move.",
        DoorKind.Medicine => "Win: raise your max HP, stamina or Qi.",
        DoorKind.SkillEnhance => "Win: power up one of your moves.",
        DoorKind.Elixirs => "A stronger mini boss. Win: raise two of max HP, stamina and Qi.",
        DoorKind.AncientScroll => $"A stronger mini boss. Win: power up {_run.Sheet.ScrollPicks} of your moves.",
        _ => $"The last fight: {_run.Sheet.BossHp} HP.",
    };

    // ------------------------------------------------------------ fights

    private void StartRunFight()
    {
        _runFighting = true;
        _menuOpen = false;
        _pending = 0;
        NewDuel();
        var e = _run.Enemy!;
        _event.Text = $"[b]Chamber {e.Number} of {_run.Sheet.Fights}{(e.IsBoss ? " · BOSS" : e.IsMiniboss ? " · MINI BOSS" : "")}[/b] against {RunBrainStyle.Name}. Fight!";
        RefreshMenu();
    }

    private void FinishRunFight()
    {
        bool won = _duel.Two.IsDown;
        int chamber = _run.Chamber;
        _run.EndFight(_duel);
        _runFighting = false;
        _runRow = 0;
        _runNote = won && _run.Phase != RunPhase.Victory
            ? $"You won chamber {chamber}! Healed {_run.LastHeal} HP (now {_run.Hp}/{_run.MaxHp}). Your {_run.Qi} Qi carries over."
            : "";
        if (_run.Phase == RunPhase.Roam) StartRoam();
        else RefreshMenu();
    }

    /// <summary>
    /// Headless check of the run glue: reads the first scroll onto □, goes through the first door, starts chamber 1's fight
    /// through the arena (the run's duel and brain) and plays it for <paramref name="ticks"/> ticks with a training dummy's
    /// presses. Returns the hits landed.
    /// </summary>
    public int SimulateRun(int ticks, out string summary)
    {
        _runMode = true;
        _run.ChooseStarter(0);
        _run.Place(Slot.Square);
        NewDuel(); // chamber 0: the wooden dummy
        int doors = _run.Doors.Count;
        _run.EnterDoor(0);
        _runFighting = true;
        NewDuel();
        var player = TrainingDummy.FromRules(_rules, Slot.Square);
        for (int i = 0; i < ticks && !_duel.IsOver; i++)
        {
            if (_clock.IsFrozen)
            {
                _clock.Step(SlotInput.None, SlotInput.None);
                continue;
            }
            var opponent = OpponentInput();
            _clock.Step(player.Think(_duel.One), opponent.Slots, MoveInput.WalkForward, opponent.Move);
        }
        summary = $"{doors} doors out of chamber 0; chamber 1: you {_duel.One.Hp}/{_duel.One.MaxHp} HP, {RunBrainStyle.Name} {_duel.Two.Hp}/{_duel.Two.MaxHp} HP";
        return _duel.Hits.Count;
    }

    // ------------------------------------------------------------ the screens

    private void RefreshRunScreen()
    {
        bool show = RunScreenUp || _menuOpen;
        _menu.Visible = show;
        _menuBack.Visible = show;
        if (!show) return;

        var sb = new System.Text.StringBuilder();
        if (_runFighting || RunRoaming)
        {
            sb.Append("[font_size=26][b]Paused[/b][/font_size]\n\n");
            AppendPlayer(sb);
            sb.Append("\nTab, Esc or Start goes back.   [color=#ff6666]Enter: give this run up and start over.[/color]");
            _menu.Text = sb.ToString();
            return;
        }

        sb.Append($"[font_size=26][b]Demo run[/b][/font_size]   attempt {_run.Attempt}   ·   chamber {_run.Chamber} of {_run.Sheet.Fights}\n");
        if (_runNote.Length > 0) sb.Append(_runNote).Append('\n');
        sb.Append('\n');
        switch (_run.Phase)
        {
            case RunPhase.ChooseStarter:
                sb.Append("[b]Two 外功 scrolls lie before you. Read one[/b] (↑/↓, then any button or Enter):\n");
                for (int i = 0; i < _run.Starters.Count; i++) Row(sb, i, $"外功 · {_run.Starters[i].Name}");
                break;
            case RunPhase.PlaceMove:
                sb.Append($"[b]Press the button to put [color=#7dffb0]{_run.Pending!.Name}[/color] on.[/b] It stays there for the whole run.\n");
                foreach (var s in Slots.All)
                {
                    var t = _run.Player[s];
                    sb.Append($"   {Slots.Label(s),-3} {(t == null ? "[color=#7dffb0]empty[/color]" : $"[color=#888888]{t.Name} (taken)[/color]")}   [color=#aaaaaa]{Controls.LabelOf(s)}[/color]\n");
                }
                break;
            case RunPhase.Fight:
                var e = _run.Enemy!;
                var tag = e.IsBoss ? " · [color=#ff6666]BOSS[/color]" : e.IsMiniboss ? " · [color=#ffcc33]MINI BOSS[/color]" : "";
                sb.Append($"[b]Chamber {e.Number}{tag}[/b] ({DoorName(_run.Door!)}) against {RunBrainStyle.Name}\n");
                sb.Append($"   HP {e.Stats.MaxHp}   ·   stamina {e.Stats.MaxStamina}   ·   Qi {e.Stats.Qi}/{e.Stats.MaxQi}\n");
                sb.Append("   Its moves: ").Append(string.Join(", ", Slots.All.Where(s => e.Loadout[s] != null).Select(s => e.Loadout[s]!.Name)))
                  .Append(" (and walking and quick step)\n   ").Append(DoorWhat(_run.Door!)).Append("\n\n");
                AppendPlayer(sb);
                sb.Append("\n[b]Press any button (or Enter) to fight.[/b]");
                break;
            case RunPhase.Reward:
                var picks = _run.PicksLeft > 1 ? $" ({_run.PicksLeft} different moves to power up)" : "";
                sb.Append($"[b]{DoorName(_run.Door!)}: choose a reward{picks}[/b] (↑/↓, then any button or Enter):\n");
                for (int i = 0; i < _run.Offers.Count; i++) Row(sb, i, OfferText(_run.Offers[i]));
                sb.Append('\n');
                AppendPlayer(sb);
                break;
            case RunPhase.Victory:
                sb.Append($"[font_size=30][color=#7dffb0][b]Run complete![/b][/color][/font_size]  You cleared all {_run.Sheet.Fights} chambers.\n\n");
                AppendPlayer(sb);
                sb.Append("\nPress any button (or Enter) to start a new run.");
                break;
            case RunPhase.Defeat:
                sb.Append($"[font_size=30][color=#ff6666][b]Defeated in chamber {_run.Chamber}.[/b][/color][/font_size]  The run is over.\n\n");
                AppendPlayer(sb);
                sb.Append("\nPress any button (or Enter) to start again from scratch.");
                break;
        }
        sb.Append("\n\n[color=#aaaaaa]F4: practice arena[/color]");
        _menu.Text = sb.ToString();
        if (_runNote.Length > 0) _event.Text = _runNote;
    }

    private void Row(System.Text.StringBuilder sb, int i, string text) =>
        sb.Append(i == _runRow ? $"[color=#ffd11a]▶ {text}[/color]\n" : $"   {text}\n");

    /// <summary>Your stats and buttons, with each move's power-ups.</summary>
    private void AppendPlayer(System.Text.StringBuilder sb)
    {
        sb.Append($"[b]You[/b]   HP {_run.Hp}/{_run.MaxHp}   ·   stamina {_run.MaxStamina}   ·   Qi {_run.Qi}/{_run.MaxQi}\n");
        foreach (var s in Slots.All)
        {
            var t = _run.Player[s];
            if (t == null) continue;
            var u = _run.Player.UpgradesOf(t);
            var ups = u.Levels == 0 ? "" : $"   [color=#ffd640]{u.Levels}/{_run.Sheet.UpgradeMax}: {(u.DamageLevels > 0 ? $"damage x{u.DamagePct / 100.0:0.00}" : "")}{(u.DamageLevels > 0 && u.StaminaLevels > 0 ? ", " : "")}{(u.StaminaLevels > 0 ? $"stamina x{u.StaminaPct / 100.0:0.00}" : "")}[/color]";
            sb.Append($"   {Slots.Label(s),-3} {t.Name}{ups}\n");
        }
    }

    /// <summary>A reward in words.</summary>
    private string OfferText(Offer o)
    {
        switch (o.Kind)
        {
            case OfferKind.Upgrade:
                var u = _run.Player.UpgradesOf(o.Move!);
                var next = _run.LevelUp(u, o.Upgrade);
                return o.Upgrade == UpgradeKind.Damage
                    ? $"Power up {o.Move!.Name}: more damage (x{u.DamagePct / 100.0:0.00} → x{next.DamagePct / 100.0:0.00}), power-up {next.Levels}/{_run.Sheet.UpgradeMax}"
                    : $"Power up {o.Move!.Name}: less stamina (x{u.StaminaPct / 100.0:0.00} → x{next.StaminaPct / 100.0:0.00}), power-up {next.Levels}/{_run.Sheet.UpgradeMax}";
            case OfferKind.Stats:
                var parts = new List<string>();
                if (o.Hp > 0) parts.Add($"max HP +{o.Hp} ({_run.MaxHp} → {_run.MaxHp + o.Hp})");
                if (o.Stamina > 0) parts.Add($"max stamina +{o.Stamina} ({_run.MaxStamina} → {_run.MaxStamina + o.Stamina})");
                if (o.Qi > 0) parts.Add($"max Qi +{o.Qi} ({_run.MaxQi} → {_run.MaxQi + o.Qi})");
                var text = string.Join(" and ", parts);
                return text.Length > 0 ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;
            case OfferKind.NewMove: return $"Learn {CategoryName(o.Move!.Category)} · {o.Move!.Name} (goes on an empty button)";
            default: return o.Kind.ToString();
        }
    }

    /// <summary>The category's name as the pause menu shows it (外功, 内功 ... 炼气).</summary>
    private string CategoryName(Category c)
    {
        foreach (var (category, first, _) in MoveSets)
            if (Find(first).Category == c) return category;
        return c.ToString();
    }

    /// <summary>" +2" after a move's name on the HUD when it has run power-ups.</summary>
    private static string UpgradeMark(Loadout loadout, Technique t)
    {
        int levels = loadout.UpgradesOf(t).Levels;
        return levels == 0 ? "" : $" +{levels}";
    }

    /// <summary>The run's line at the top of the HUD during a fight or while roaming.</summary>
    private string RunStatusLine()
    {
        if (_runFighting && _run.Enemy != null)
            return $"[b]Chamber {_run.Enemy.Number}/{_run.Sheet.Fights}{(_run.Enemy.IsBoss ? " · BOSS" : _run.Enemy.IsMiniboss ? " · MINI BOSS" : "")}[/b]   ·   ";
        if (RunRoaming)
            return $"[b]Chamber {_run.Chamber}{(_run.Chamber == 0 ? "" : " (cleared)")}[/b] · {_run.Doors.Count} door{(_run.Doors.Count == 1 ? "" : "s")}   ·   ";
        return "";
    }
}
