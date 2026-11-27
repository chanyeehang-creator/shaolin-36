---
name: shaolin-enemy-design
description: Shaolin 36's rules for PvE enemies - dumb, choreographed common foes, big clear tells, one fixed rhythm per move, difficulty from reactions and defence, boss-only spectacle later - plus where brains, movesets and tells live, and how to test them. Use when designing, building or tuning any enemy, opponent brain, enemy moveset, attack telegraph, mini boss or boss.
---

<!-- Written for Shaolin 36 on 27 Nov 2026 from Chan's direction. The "Build it" and "Test it" parts adapt MengTo/Skills agent-skills/game-development/tune-enemy-ai and design-action-combat (MIT, https://github.com/MengTo/Skills), reworded from web games to Godot and the engine-free core. -->

# Shaolin 36 enemy design

Shaolin 36 is strictly PvE for now, and an enemy exists to be read. The player should see the attack coming, answer it, and feel good about getting it right.

Use this skill with two others:
- `combat-design` for frame data, telegraphs and punish windows;
- `game-ai` for decision models and state-machine pitfalls.

Where they disagree with this skill, this skill and Chan win.

## The rules (Chan)

1. **Common foes are dumb on purpose.** They don't need the player's tools or advantages: for example, no guessing, no perfect parries and no stepping out of combos. They move in a predictable, choreographed way so the player can read it and react.
2. **Every attack has a big, clear tell.**
   - The tell is a distinct wind-up pose (poses.csv) plus the wind-up cue (`WindUpCue`: light, medium, heavy, energy or grab).
   - Add sound and effects where they help.
   - A first-time player should be able to tell which attack is coming from its wind-up.
3. **Each move has one fixed rhythm.**
   - The same wind-up always takes the same number of frames.
   - There are no delayed, held or feinted swings (no Elden Ring mind games).
   - An enemy may choose *which* move comes next. It never changes *when* a move it has started will land.
4. **The rhythm between moves is easy to read too.**
   - Give common foes a beat: approach, tell, strike, a clear recovery the player can punish, then reset.
   - Avoid back-to-back attacks with no opening.
5. **Harder means faster reactions and better defence, not harder to read.**
   - Scale the `reaction`, `parry`, `perfect`, `step`, `clash` and `break` columns in brains.csv, and HP.
   - Keep the tells the same. If a harder enemy needs a quicker attack, give it a new move with its own clear tell.
6. **Enemies get their own movesets.** Many enemies will soon have moves that differ from the player's, and each one still follows rules 2 and 3.
7. **Bosses come later.**
   - They get over-the-top, boss-only skills, such as an energy wave that covers much of the floor or an instant teleport strike.
   - These still telegraph: a long, loud wind-up and a safe answer (a dodge, a jump, or a gap in the wave).
   - Ask Chan before designing one.

## Where things live

- **Brains:** `src/Shaolin36.Core/Brains/` (`Brain.cs`, `BrainStyle.cs`, `Dice.cs`).
  - A brain presses the same slot buttons and stick as the player.
  - It sees what the other fighter does only after its `reaction` frames, and it never reads inputs.
  - Its randomness comes from the seeded `Dice`, so a fight replays exactly.
- **Brain numbers:** `game/data/brains.csv`. Every column is explained in the comments at the top.
- **Moves:** `game/data/techniques.csv`. Startup is the tell, active is the hit, and recovery is the punish window. The sheet also holds advance, lunge, reach and the rest.
- **Wind-up poses:** `game/data/poses.csv`. The `<id>.windup@p` keys are what the player reads.
- **The run:** `game/data/run.csv` sets the 10 chambers, enemy growth, the mini boss and the chamber-10 boss.
- **Feel:** `game/data/feel.csv` sets hit-stop, shake, sparks and sound for each event. A brain doesn't think during a hit-stop freeze (`FeelClock.IsFrozen`).

## Build it

- **Use a small, explicit state machine.**
  - Name the states: closing, waiting, wind-up, attacking, recovering, staggered, resting, and so on.
  - Give every transition a stated prerequisite, an exit condition and a minimum time in state, so a foe never flickers between choices.
  - `BrainIntent` already names what a brain is doing for the playtest screen. Extend it rather than adding hidden flags.
- **Keep perception, intent and motion separate.**
  1. Gather only what the brain may know: distance, the other fighter's state after the reaction delay, and its own stamina, Qi and HP.
  2. Pick one intent from the moves that are legal right now.
  3. Carry it out without rewriting the decision mid-move.
- **A choreographed foe can be a script.**
  - A list of beats ("walk to 180 cm, wait 40 frames, 罗汉拳 hit 1, recover, step back") often suits a common foe better than dice.
  - Use the dice to pick which beat comes next, never to change the timing inside a beat.
- **Add fairness guards:** no instant turn-and-hit, no strings without a recovery, and no endless chasing.
- **Keep the rules engine-free.**
  - The core targets netstandard2.1 with C# 9.
  - Use integer frames, with no wall clock, no unseeded randomness and no floating-point state.
  - New numbers go in a sheet, where `tools/DataCheck` checks them.

## Test it

- **Write deterministic core tests for each enemy or move.** Assert transitions and outcomes, not just end positions. Check that:
  - the tell lasts the full startup, every time;
  - the move lands on the same frame whatever the dice roll;
  - the recovery leaves a punish window at least as long as the player's quickest answer;
  - the foe never acts in hitstun or during a freeze;
  - a harder brain differs only in its reaction and defence numbers.
- **Run `scripts/test.sh` after each change.** CLAUDE.md says one change, then test. Then run `scripts/smoke.sh` for the Godot side.
- **Render the tell.** `scripts\pose-shots.bat <move>` writes `pose-shots/<move>.strip.png`. Check that the tell reads at a glance in the view as the opponent sees it.
- **Ask Chan to play it.** Did the attack read? Did answering it feel satisfying? Did any hit come as a surprise?
