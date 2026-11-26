using System.Linq;
using Godot;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Runs;

namespace Shaolin36.Game;

/// <summary>
/// The demo roguelike run (Chan's test run) on top of the arena: the screens between fights (pick 罗汉拳 or 虎形拳, pick a
/// button, the next enemy, the rewards, the end of the run) and the hand-off of each duel to and from the core's
/// <see cref="Run"/>. The arena opens on the run; F4 switches to the practice arena and back.
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

    /// <summary>Frames after a KO before a press goes on (0.75 s).</summary>
    private const int KoPause = 45;

    private bool RunFighting => _runMode && _runFighting;

    /// <summary>A run screen covers the floor and the sim is paused.</summary>
    private bool RunScreenUp => _runMode && !_runFighting;

    private Loadout PlayerLoadout => _runMode ? _run.Player : _playerLoadout;

    private BrainStyle RunBrainStyle =>
        _brains.Find(_run.Enemy!.BrainId) ?? throw new System.Collections.Generic.KeyNotFoundException($"brains.csv has no '{_run.Enemy.BrainId}'");

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

        if (_runFighting)
        {
            if (_duel.IsOver)
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
                    RefreshMenu();
                }
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
                _runNote = $"You learnt {starter.Name}.";
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
    }

    private void StartRunFight()
    {
        _runFighting = true;
        _menuOpen = false;
        _pending = 0;
        NewDuel();
        var e = _run.Enemy!;
        _event.Text = $"[b]Fight {e.Number} of {_run.Sheet.Fights}{(e.IsBoss ? " · BOSS" : "")}[/b] against {RunBrainStyle.Name}. Fight!";
        RefreshMenu();
    }

    private void FinishRunFight()
    {
        bool won = _duel.Two.IsDown;
        int fight = _run.FightNumber;
        _run.EndFight(_duel);
        _runFighting = false;
        _runRow = 0;
        _runNote = won
            ? _run.Phase == RunPhase.Victory ? "" : $"You won fight {fight}! Healed {_run.LastHeal} HP (now {_run.Hp}/{_run.MaxHp}). Your {_run.Qi} Qi carries over."
            : "";
        RefreshMenu();
    }

    /// <summary>
    /// Headless check of the run glue: picks the first starter onto □, starts fight 1 through the arena (the run's duel and
    /// brain) and plays it for <paramref name="ticks"/> ticks with a training dummy's presses. Returns the hits landed.
    /// </summary>
    public int SimulateRun(int ticks, out string summary)
    {
        _runMode = true;
        _run.ChooseStarter(0);
        _run.Place(Slot.Square);
        _runFighting = true;
        NewDuel();
        var player = TrainingDummy.FromRules(_rules, Slot.Square);
        for (int i = 0; i < ticks && !_duel.IsOver; i++)
        {
            if (_clock.IsFrozen)
            {
                _clock.Step(Shaolin36.Core.Fighters.SlotInput.None, Shaolin36.Core.Fighters.SlotInput.None);
                continue;
            }
            var opponent = OpponentInput();
            _clock.Step(player.Think(_duel.One), opponent.Slots, MoveInput.WalkForward, opponent.Move);
        }
        summary = $"run fight 1: you {_duel.One.Hp}/{_duel.One.MaxHp} HP, {RunBrainStyle.Name} {_duel.Two.Hp}/{_duel.Two.MaxHp} HP";
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
        if (_runFighting)
        {
            sb.Append("[font_size=26][b]Paused[/b][/font_size]\n\n");
            AppendPlayer(sb);
            sb.Append("\nTab, Esc or Start goes back to the fight.   [color=#ff6666]Enter: give this run up and start over.[/color]");
            _menu.Text = sb.ToString();
            return;
        }

        sb.Append($"[font_size=26][b]Demo run[/b][/font_size]   attempt {_run.Attempt}   ·   fight {_run.FightNumber} of {_run.Sheet.Fights}\n");
        if (_runNote.Length > 0) sb.Append(_runNote).Append('\n');
        sb.Append('\n');
        switch (_run.Phase)
        {
            case RunPhase.ChooseStarter:
                sb.Append("[b]Choose your first move[/b] (↑/↓, then any button or Enter):\n");
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
                sb.Append($"[b]Next: fight {e.Number}{(e.IsBoss ? " · [color=#ff6666]BOSS[/color]" : "")}[/b] against {RunBrainStyle.Name}\n");
                sb.Append($"   HP {e.Stats.MaxHp}   ·   stamina {e.Stats.MaxStamina}   ·   Qi {e.Stats.Qi}/{e.Stats.MaxQi}\n");
                sb.Append("   Its moves: ").Append(string.Join(", ", Slots.All.Where(s => e.Loadout[s] != null).Select(s => e.Loadout[s]!.Name)))
                  .Append(" (and walking and quick step)\n\n");
                AppendPlayer(sb);
                sb.Append("\n[b]Press any button (or Enter) to fight.[/b]");
                break;
            case RunPhase.Reward:
                sb.Append("[b]Choose a reward[/b] (↑/↓, then any button or Enter):\n");
                for (int i = 0; i < _run.Offers.Count; i++) Row(sb, i, OfferText(_run.Offers[i]));
                sb.Append('\n');
                AppendPlayer(sb);
                break;
            case RunPhase.Victory:
                sb.Append($"[font_size=30][color=#7dffb0][b]Run complete![/b][/color][/font_size]  You beat all {_run.Sheet.Fights} opponents.\n\n");
                AppendPlayer(sb);
                sb.Append("\nPress any button (or Enter) to start a new run.");
                break;
            case RunPhase.Defeat:
                sb.Append($"[font_size=30][color=#ff6666][b]Defeated at fight {_run.FightNumber}.[/b][/color][/font_size]  The run is over.\n\n");
                AppendPlayer(sb);
                sb.Append("\nPress any button (or Enter) to start again from the beginning.");
                break;
        }
        sb.Append("\n\n[color=#aaaaaa]F4: practice arena[/color]");
        _menu.Text = sb.ToString();
        if (_runFighting == false && _runNote.Length > 0) _event.Text = _runNote;
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
            case OfferKind.MaxHp: return $"Max HP +{o.Amount} ({_run.MaxHp} → {_run.MaxHp + o.Amount})";
            case OfferKind.MaxStamina: return $"Max stamina +{o.Amount} ({_run.MaxStamina} → {_run.MaxStamina + o.Amount})";
            case OfferKind.MaxQi: return $"Max Qi +{o.Amount} ({_run.MaxQi} → {_run.MaxQi + o.Amount})";
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

    /// <summary>The run's line at the top of the HUD during a fight.</summary>
    private string RunStatusLine() =>
        _runFighting && _run.Enemy != null
            ? $"[b]Fight {_run.Enemy.Number}/{_run.Sheet.Fights}{(_run.Enemy.IsBoss ? " · BOSS" : "")}[/b]   ·   "
            : "";
}
