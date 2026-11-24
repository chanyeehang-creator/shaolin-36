# Shaolin 36

A third-person 3D roguelike of 36 one-on-one duels. Every win teaches the monk a new technique (偷师).

Built in **Godot 4.7 (.NET / C#)**. The combat rules live in a plain C# library with no engine code, so they run under `dotnet test` in milliseconds and could move to Unity unchanged.

## Folder layout

| Path | What it is |
| --- | --- |
| `src/Shaolin36.Core/` | The rules: techniques, frame data, clash, and later stamina, Qi, the AI brain. **No Godot code allowed here.** |
| `tests/Shaolin36.Core.Tests/` | Unit and scenario tests for the rules (xUnit). |
| `tools/DataCheck/` | Check script: rejects bad rows in the data sheets before the game loads them. |
| `game/` | The Godot project (`project.godot`). Shows and drives the core; holds scenes, scripts and assets. |
| `game/data/` | The data sheets (CSV exported from the xlsx). `techniques.csv` is the technique sheet; `rules.csv` holds base stats and the clash and parry numbers. |
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

Double-click `scripts\play.bat` (Windows) or run `scripts/play.sh`. It builds the C# code and opens the game: an arena with two capsules and a line saying how many techniques loaded.

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

A fighter has 8 slots: □ △ ○ ✕ L1 L2 R1 R2. Each holds any technique or nothing, and the rules only ever see "slot N pressed", never "parry pressed". So 化绵掌 can sit on L1, ○ or ✕, and a later 化劲 technique such as 金钟罩 drops into the same slot the same way. A gamepad (including DirectInput pads like the Logitech Dual Action) and keyboard + mouse will each map their buttons onto these 8 slots; that binding is set up in the first playable test.

## Hits and reach

Two fighters stand on a line, positions in whole centimetres. A hit connects when the other fighter is within the technique's `reach` (distance ≤ reach) on any of its active frames; each move hits at most once. The hit deals its `damage` and freezes the defender for `hitstun` frames counted from the hit frame. Hits on the same tick trade. A technique with reach 0, like 化绵掌, never hits. Clash and parry join the fight in a later step.

## Editing data

Edit `game/data/techniques.csv` or `game/data/rules.csv` (in Excel, save as **CSV UTF-8**). Category and weight accept English or Chinese (`waigong` or `外功`, `heavy` or `重`). Lines starting with `#` are comments. Run the test script afterwards: the check tells you the line and column of anything wrong.

Frames are 1/60 s sim ticks. The current numbers are Stage 1 starting values, open for tuning. The data check also prints how many frames each combo string gives you to chain and still link.

## How we work: one change, then test

Every step is one brief: one change, the tests that prove it, and one question for Chan to answer by playing. The next brief starts only after that answer. Every bug found in play becomes a test so it can't come back silently.
