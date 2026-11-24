# Shaolin 36

A third-person 3D roguelike of 36 one-on-one duels. Every win teaches the monk a new technique (偷师).

Built in **Godot 4.7 (.NET / C#)**. The combat rules live in a plain C# library with no engine code, so they run under `dotnet test` in milliseconds and could move to Unity unchanged.

## Folder layout

| Path | What it is |
| --- | --- |
| `src/Shaolin36.Core/` | The rules: techniques, frame data, clash, parry, stamina, Qi, and later the AI brain. **No Godot code allowed here.** |
| `tests/Shaolin36.Core.Tests/` | Unit and scenario tests for the rules (xUnit). |
| `tools/DataCheck/` | Check script: rejects bad rows in the data sheets before the game loads them. |
| `game/` | The Godot project (`project.godot`). Shows and drives the core; holds scenes, scripts and assets. |
| `game/data/` | The data sheets (CSV exported from the xlsx). `techniques.csv` is the technique sheet; `rules.csv` holds base stats and the clash and parry numbers; `feel.csv` holds the hit feel (hit-stop, sparks, sound, camera) for each kind of strike. |
| `scripts/` | One-click Play, test and headless boot scripts. |
| `.github/workflows/` | CI: data check, core tests and a headless Godot boot on every push. |

## What you need

- [Godot 4.7.2 .NET](https://godotengine.org/download) (the **.NET** build, not the standard one)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Tell the scripts where Godot is, once:

```bat
setx GODOT "C:\path\to\Godot_v4.7.2-stable_mono_win64.exe"
```

## Play

Double-click `scripts\play.bat` (Windows) or run `scripts/play.sh`. It builds the C# code and opens the playable test: you against a training dummy that repeats the 罗汉拳 string, on a square floor with the camera behind you, and 罗汉拳, 铁砂掌 and 化绵掌 on buttons you can swap. Step-by-step instructions, the full control table and gamepad setup are in [docs/playtest.md](docs/playtest.md).

To work in the editor instead, open `game/project.godot` in Godot .NET and press F5.

## Test

Double-click `scripts\test.bat`, or run:

```sh
dotnet run --project tools/DataCheck -- game/data                   # data check
dotnet test Shaolin36.sln                                           # rule tests
scripts/smoke.sh                                                    # headless boot (needs GODOT set)
```

CI runs all three on every push.

## Buttons are slots

A fighter has 8 slots: □ △ ○ ✕ L1 L2 R1 R2. Each holds any technique or nothing, and the rules only ever see "slot N pressed", never "parry pressed". So 化绵掌 can sit on L1, ○ or ✕, and a later 化劲 technique such as 金钟罩 drops into the same slot the same way. A gamepad (including DirectInput pads like the Logitech Dual Action) and keyboard + mouse both map their buttons onto these 8 slots, at the same time (`game/scripts/Controls.cs`). In the game, the loadout screen (Tab, Esc or the pad's Start button) puts any technique on any button.

## The floor, facing and reach (full 3D)

Two fighters stand anywhere on a square floor, `arena_width` cm each way. Positions are whole numbers in 1/60 cm (`FloorPoint`), so a speed of N cm per second is exactly N units per tick and the same inputs always give the same positions. The fighters face each other automatically (lock-on). An attack takes its aim on the tick it starts, and its whole string keeps that aim; turning resumes once the fighter is free again.

A hit connects when, on any of its active frames, the other fighter is within the technique's `reach` across the floor (centre to centre) **and** inside its `hit_angle`, a wedge centred on the aim (60 = 30° either side; empty or 360 = all round). So stepping to the side during a wind-up can make a strike miss. Each move hits at most once. The hit deals its `damage` and freezes the defender for `hitstun` frames counted from the hit frame. Hits on the same tick trade. A technique with reach 0, like 化绵掌, never hits.

## Walking, stepping in, quick step and the training dummy

- **Walking:** an idle fighter walks the way the stick or WASD says, relative to the lock-on (forward is straight at the opponent, right goes round their side), at `walk_speed` cm per second scaled by how far the stick is pushed. Diagonals are no faster. Fighters stop `min_distance` apart (walking round someone up close slides you round them) and at the floor's edges.
- **Stepping in:** a technique's `advance` (cm) steps the attacker forward along its aim, slid over its startup frames. 罗汉拳 steps 10, 8, 8 and 15 cm; 铁砂掌 20 cm. It stops at `min_distance`, and being hit out of the move stops it.
- **Quick step:** double-tap forward, back, left or right (within `quick_step_tap_window` frames) for a short, fast step that way: `quick_step_distance` cm forward or back, `quick_step_side_distance` cm left or right, over `quick_step_frames`, then `quick_step_recovery` frames before you can act. It costs `quick_step_stamina` and can't start while exhausted. It is not a dodge roll: no invincible frames, no perfect-timing bonus. Side steps curve round the opponent. Anyone can use it at any time they're free; it isn't on a slot.
- **The playtest dummy** (`TrainingDummy`) stands where it is, turns to face you, waits `dummy_pause` frames, then throws its string, chaining on chain-window frame `dummy_chain_frame`.

All of these numbers are in `rules.csv` and `techniques.csv`.

## Hit feel

Every strike is sorted into one row of `feel.csv` (hit, heavy hit, block, perfect parry, the clashes, KO...). The core's `FeelClock` drives the duel and freezes it for that row's `hitstop` frames after the strike: nothing in the rules moves during a freeze, so all frame data keeps its meaning, and slot presses made during it are used right after. The Godot side plays the row's sparks, sound and camera shake and push. F3 turns the kit off and on in the playtest.

## Knockback

A landed hit pushes the defender straight away from the attacker by the technique's `knockback` (cm), slid over `knockback_frames` ticks starting the tick after the hit; a same-weight clash pushes both apart by `clash_recoil_push`. Hyper armor holds its ground, parries and lost clashes push nobody, and the floor's edges stop a push. The data check prints how close each string must start (counting its step-in and knockback) so every hit stays in reach.

## Editing data

Edit `game/data/techniques.csv`, `game/data/rules.csv` or `game/data/feel.csv` (in Excel, save as **CSV UTF-8**). Category and weight accept English or Chinese (`waigong` or `外功`, `heavy` or `重`). Lines starting with `#` are comments. Run the test script afterwards: the check tells you the line and column of anything wrong.

Frames are 1/60 s sim ticks. The current numbers are Stage 1 starting values, open for tuning. The data check also prints how many frames each combo string gives you to chain and still link.

## How we work: one change, then test

Every step is one brief: one change, the tests that prove it, and one question for Chan to answer by playing. The next brief starts only after that answer. Every bug found in play becomes a test so it can't come back silently.
