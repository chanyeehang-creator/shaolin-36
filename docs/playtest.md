# First playable test: how to play it

You (the orange capsule, with the camera behind you) against a training dummy (the dark red capsule) that throws the 罗汉拳 string over and over, on a square floor in full 3D. The point is to feel the numbers, so play for a few minutes and then answer the questions at the bottom. New in this build: walking in any direction with a lock-on camera, hit angles, attacks that step in, and the quick step (all in section 5, under "Full 3D").

## 1. Get the new version on your PC

You already have the repo at `D:\GitHub\shaolin-36` in GitHub Desktop.

1. Open **GitHub Desktop**.
2. Click **Fetch origin** (top bar).
3. To play **before** the pull request is merged: click **Current branch** (top bar), find `claude/project-thread-xjwa4z` in the list and click it. If it isn't listed, click Fetch origin again.
   To play **after** it is merged: switch to `main` and click **Pull origin**.

## 2. Start the game

1. Open the folder `D:\GitHub\shaolin-36\scripts` in File Explorer.
2. Double-click **`play.bat`**.
3. A black window builds the code for a few seconds, then the game window opens.

If the black window says Godot can't be found, your `GODOT` setting points at the wrong file. In a Command Prompt, run this once (change the file name if yours is different), then close and reopen File Explorer:

```bat
setx GODOT "D:\Tools\Godot-dotnet\Godot_v4.7.2-stable_mono_win64.exe"
```

If it says the build failed, double-click `scripts\test.bat` and send Claude a screenshot of what it prints.

## 3. Connect the Logitech Dual Action gamepad

1. Plug the pad into a USB port **before** you start the game. Windows sets it up by itself the first time (wait for the "device is ready" message, about 10 seconds).
2. Optional check that Windows sees it: press **Win + R**, type `joy.cpl`, press Enter. "Logitech Dual Action" should be listed with status OK. Click **Properties** and press buttons: the matching numbers light up.
3. Look at the small **MODE** light on the pad. When it is **off**, the left stick is the stick and the D-pad is the D-pad. When it's on they swap. Either way both of them walk, so leave it how you like.
4. Start the game. The grey line under the fight text should say **"Gamepad: Logitech Dual Action"**. Press a few buttons: the same line shows "last pad input: …" so you can see the game hears them.

If that line says "unknown layout", or a button does the wrong thing, send Claude a screenshot of that line after pressing the button. Keyboard and mouse always work at the same time as the pad, so you can keep playing either way.

## 4. Controls

Every fight button is a **slot**. Slots don't mean anything on their own: whatever technique you put on a slot is what that button does.

| Slot | Gamepad (Dual Action) | Keyboard + mouse | Starts with |
| --- | --- | --- | --- |
| □ | 1 (left face button) | J or left click | 罗汉拳 |
| △ | 4 (top) | I | 铁砂掌 |
| ○ | 3 (right) | L | |
| ✕ | 2 (bottom) | K or Space | |
| L1 | 5 | Q or right click | 化绵掌 |
| L2 | 7 | Left Shift | |
| R1 | 6 | E | |
| R2 | 8 | F | |

| Other | Gamepad | Keyboard |
| --- | --- | --- |
| Walk (any direction) | Left stick or D-pad | W / A / S / D or the arrow keys |
| Quick step | Tap the stick or D-pad twice the same way | Tap W, A, S or D twice |
| Dummy stands still / attacks again | 9 (Back / Select) | T |
| Loadout screen (pauses) | 10 (Start) | Tab or Esc |
| Restart the duel | Start after a KO, or the loadout screen's last row | Enter |
| Half speed on/off | | F1 |
| Floating text on/off | | F2 |
| Feel kit on/off (hit-stop, sparks, sound, camera) | | F3 |

On the **loadout screen**: move up and down to pick a technique (W/S, ↑/↓ or the D-pad), then press the button you want it on. Want 化绵掌 on ✕ instead of L1? Highlight 化绵掌 and press ✕. If something was already on that button, the two swap. Below the techniques, one row turns the dummy's attacks on and off (Select or T does the same from the fight), and the last row restarts the duel.

## 5. Reading the screen

The capsules change colour with what they are doing:

- **Yellow**: startup (winding up). A faint yellow wedge on the floor shows where the move will hit: as long as its reach and as wide as its hit angle.
- **Red**: active, the hit is live. The floor wedge turns red.
- **Green / light blue**: your 化绵掌 window. Green is the perfect part (the first 4 frames), light blue the rest.
- **Dark blue**: recovery, can't act.
- **White**: hit and frozen (hitstun).
- **Purple**: staggered after a perfect parry or a lost clash. Wide open: hit them!
- **Grey**: recoil (a same-weight clash) or the short recovery after a successful parry.
- **Pale mint**: a quick step.

**Floating text** over the capsules shows what just happened: damage like **-6**, **Blocked!**, **Parried!** (perfect), **Stagger!**, **CLASH!**, **Too early! / Too late!** for a missed parry, and the stamina ones: **+10 stamina**, **-15 stamina**, **Exhausted!**, **Breath back**, **No stamina!** (you pressed a move while exhausted). It's a playtest aid only: press **F2** to switch it off or on.

The top lines show both HP bars and stamina bars, what each fighter is doing with its frame number, and **"chain now!"** while you are in the window where pressing the same button again starts the next hit. When the same fighter lands hits in a row before the other recovers, you see **"N hit combo"**.

**About 化绵掌 (the parry):** it is a tap, not a hold. Each press winds up for 3 frames, opens a 14-frame window (about 0.23 s), then has a 10-frame cooldown if nothing hit it, so to block a whole string you tap once for each hit. When the dummy's hit lands in your window:

- **First 4 frames: PERFECT PARRY.** No damage, the dummy's attack is stopped and it is staggered (purple) for 30 frames, and you recover in 4. Hit it straight away.
- **The next 10 frames: Blocked.** No damage, but the dummy isn't interrupted: its string carries on, so tap again for its next hit. You recover in 8 frames, then you can tap again.
- **Outside the window**, the hit lands as normal and the screen tells you whether you were too early or too late, and by how many frames.

**Stamina:** the blue bar next to each HP bar (100 to start). Both you and the dummy play by the same rules:

- **Every move costs stamina when you press it.** Each 罗汉拳 hit costs its own (8, 8, 9, 14: 39 for the whole string). Each 化绵掌 tap costs 10, whether it blocks, misses or is perfect, so blocking a full string costs 40.
- **A perfect parry gives your 10 back** and the attacker loses 15 more ("+10 stamina" / "-15 stamina" float up).
- **Clashes cost a little:** 5 each for a same-weight clash, 8 for the side that loses.
- **Getting it back:** nothing while you're doing a move. Half a second (30 frames) after your last move, it refills at 40 per second standing still, or 25 per second while walking or reeling from a hit.
- **At 0 you're exhausted** (the bar turns orange, "Exhausted!" floats up): you can't use any move that costs stamina, so **no 化绵掌, your guard is broken**. You walk at half speed and take 20% extra damage (a 6-damage hit does 7, the 12-damage finisher does 14). It lasts until the bar is back to 50: about 1.75 s if you stand still. Then "Breath back".
- The dummy gets exhausted too: its 3rd string in a row empties it (on the finisher), and it rests about 2.5 s before attacking again. That's your moment: it can't attack or parry and takes extra damage.

**Qi (气):** the gold bar after stamina, 0 to 100. Every duel starts you on 0, and it never refills by itself: you earn it by fighting well.

- **+2 for every hit that deals damage** (a full 罗汉拳 string is +8). A heavy move that wins its clash counts too.
- **+10 for a perfect parry** and **+10 for a perfect clash** ("+10 Qi" floats up in gold). A plain block, a same-weight clash or a lost clash gives nothing.
- It stops at 100.
- **铁砂掌 spends it** (below). Later 点穴 (pressure-point) and 气功 (breathing) techniques will cost Qi too, and in a run your Qi will carry over from one duel to the next.

**铁砂掌 (iron palm), on △:** one heavy palm strike instead of a string.

- Wind-up 36 frames (0.6 s), hit 4 frames, recovery 36 frames (0.6 s): 76 frames in all, about 1.3 s. Easy to see coming and very punishable if it misses.
- Costs 20 stamina, deals 15 damage, freezes the dummy for 40 frames, reaches 130 cm. It's heavy, so it wins normal clashes against 罗汉拳 hits 1 to 3.
- **With 30 Qi or more, it powers up automatically:** it spends 30 Qi ("Qi 铁砂掌!" and "-30 Qi" float up), deals **double damage (30)**, and has **hyper armor** until it lands: the capsule winds up in deep gold instead of yellow, and if the dummy hits you then, you still lose the HP ("Armor!") but your palm keeps going. The armor ends once the palm has struck, so the 0.6 s recovery is still open.
- Armor doesn't beat 化绵掌: a perfect parry still staggers it. Clashes still work the normal way.
- Under 30 Qi it's the plain palm and your Qi is kept.

**Clashes (拼招):** if your hit lands in the last frames of the dummy's wind-up, the moves clash. Same weight: both bounce off (grey). Heavier move: it breaks through. Lighter move: it gets knocked aside. Hit the very first frames of that window and it's a **PERFECT CLASH**: the dummy is staggered whatever the weights. The 罗汉拳 finisher (hit 4) is heavy, so it wins normal clashes against hits 1 to 3.

**Feel kit (Stage 2):** every strike now lands with a short freeze (**hit-stop**), a burst of sparks with a flash of light, a sound, and a camera shake. The big moments also push the camera in a little. Press **F3** to switch it all off and on, so you can feel the difference.

- **Hit-stop** freezes the whole fight for a few frames right after a strike: both fighters, every timer, stamina regen. Nothing in the frame data changes. 罗汉拳 hit 1 still stuns for exactly 27 frames, but those 27 frames start after the freeze. The top line says **(hit-stop)** while it's frozen.
- **Buttons pressed during the freeze are kept** and used straight after it, so chaining the string on the "chain now!" beat works the same.
- Starting values (1 frame = 1/60 s):

| What happened | Freeze | Sound | Sparks |
| --- | --- | --- | --- |
| 罗汉拳 hits 1 to 3 land | 5 frames (0.08 s) | smack | small, pale orange |
| Heavy hit (罗汉拳 finisher, plain 铁砂掌) | 8 frames (0.13 s) | thud | orange |
| Qi 铁砂掌 lands | 12 frames (0.2 s) | crack | big, gold, camera push |
| A hit on hyper armor | 4 frames | thud | gold |
| 化绵掌 blocks | 3 frames (0.05 s) | dull block | small, blue |
| PERFECT PARRY | 10 frames (0.17 s) | bright ring | green, camera push |
| Clash (any kind) | 6 to 9 frames | clang | yellow-white, camera push |
| PERFECT CLASH | 14 frames (0.23 s) | clang | the biggest burst, biggest push |
| The finishing hit (KO) | 20 frames (0.33 s) | gong | red |

The sounds are placeholders made in code (no sound files yet), so judge their timing and weight, not their quality.

**Hit reactions and knockback (Stage 2):** a fighter who gets hit leans back, holds that lean through the hit-stop, then springs upright in about 0.3 s. Bigger hits lean further (a 罗汉拳 jab 10°, a heavy hit 18°, Qi 铁砂掌 26°). A staggered fighter wobbles, and the loser of the duel tips over. Hyper armor barely flinches.

Hits also **push the defender back** (knockback). This is a rule, not just a look, so F3 doesn't turn it off. The push slides over 8 frames (0.13 s) once the hit-stop ends.

| What happened | Pushed back |
| --- | --- |
| 罗汉拳 hits 1 to 3 | 4 cm each |
| 罗汉拳 finisher | 60 cm, so the string ends with them out of reach |
| 铁砂掌 (plain or Qi) | 50 cm |
| Same-weight clash | both fighters 25 cm, a real reset (110 cm apart becomes 160) |
| Hit on hyper armor, a parry, a lost clash | nobody moves |

Because of the small pushes, the full 罗汉拳 string only stays in reach if you start it **116 cm away or closer**. The duel starts at 110 cm, so you're fine from the start position. If you start it at the very tip of hit 1's 120 cm reach, hit 2 will miss: step in first.

**Full 3D: the floor, the camera and walking.** The fight is no longer on a line. You and the dummy stand on a 14 m square floor (the grid lines are 1 m apart), and you can walk in any direction.

- **The camera** sits behind you (4.4 m back, 3.5 m up) and a little to your right (2.2 m), always looking at the dummy (lock-on). Each capsule has a small dark "nose" on its chest that shows which way it faces.
- **Walking is relative to the dummy:** W / stick up walks straight at it, S / stick down backs away, A and D (stick left and right) circle round it. Up close, walking into it slides you round its side. The stick walks as fast as you push it; full speed is 200 cm per second, diagonals included.
- **Both fighters turn to face each other** while they're free (standing, walking, quick-stepping or reeling from a hit). **Once a fighter attacks, the turning stops:** the attack aims where the other fighter stood on the frame it started, and every follow-up hit of that string goes the same way. Turning comes back when the string is over. So if you side-step the dummy's first punch, the rest of its 罗汉拳 string carries on into empty air.
- **Hits have a width now (hit angle).** A strike only lands if you're inside its wedge, not just inside its reach. Step to the side during the wind-up and it can miss you.

| Move | Reach | Hit angle (wedge) |
| --- | --- | --- |
| 罗汉拳 hits 1 and 2 | 120 cm | 60° (30° either side) |
| 罗汉拳 hit 3 | 125 cm | 70° |
| 罗汉拳 finisher | 140 cm | 90°: a wide hook, harder to step round |
| 铁砂掌 | 130 cm | 60° |

**Attacks step in (外功).** Fighters no longer punch from where they stand: each 外功 strike moves the attacker forward along its aim during the wind-up, so the step is done as the hit comes out. This is the same for you and the dummy. It stops when the two of you are touching (80 cm apart).

| Move | Steps forward | Over |
| --- | --- | --- |
| 罗汉拳 hit 1 | 10 cm | 13 frames (0.22 s) |
| 罗汉拳 hit 2 | 8 cm | 19 frames (0.32 s) |
| 罗汉拳 hit 3 | 8 cm | 6 frames (0.1 s) |
| 罗汉拳 finisher | 15 cm | 19 frames (0.32 s) |
| 铁砂掌 | 20 cm | 36 frames (0.6 s) |

So the whole 罗汉拳 string walks 41 cm forward, and it now reaches if you start it **130 cm away or closer** (it used to be 116). If a hit is knocked out of its wind-up, its step stops too.

**Quick step (everyone has it, no slot needed).** Tap a direction twice quickly (W W, S S, A A or D D, or the stick or D-pad the same way twice) for a short, quick step that way: forward to close in, back to get out of reach, left or right to get off the line of an attack and reach the dummy's side.

- **70 cm forward or back, 90 cm left or right**, in **8 frames (0.13 s)**, then **6 frames (0.1 s)** before you can act again: 14 frames (0.23 s) in all. Walking covers 70 cm in 0.35 s, so it's about three times as fast, but it's a step, not a roll or a dash.
- The second tap must come within **14 frames (0.23 s)** of the first. Only the four straight directions count: a diagonal does nothing.
- Costs **6 stamina**. You can't quick step while exhausted.
- **No invincibility and no perfect-timing dodge.** If a strike reaches you during the step, it hits you as normal. It works by moving you, so it has to happen before the hit comes out: a side step as the dummy starts its 罗汉拳 wind-up (yellow) takes you out of the punch's wedge, and you end up at its side, in reach for your own punch. Step on the last few frames of the wind-up and you're still hit.
- Left and right steps curve round the dummy, so you stay about the same distance away.
- Pressed while you're busy (in a move, or just hit), it waits up to 6 frames (the input buffer) and comes out as soon as you're free, like a button press.
- It isn't a 轻功 technique. Later 轻功 slot techniques will be the real dodges; the quick step is the bare minimum everyone keeps.

## 6. Tuning while you play

All the numbers are in two sheets you can open in Excel (save as **CSV UTF-8**), then close the game and double-click `play.bat` again:

- `game\data\techniques.csv`: each technique's frames, damage, reach, chain window, perfect parry frames.
- `game\data\rules.csv`: HP, input buffer, and the test settings added for this step:
  - `walk_speed`: how fast you walk, in cm per second (200).
  - `start_distance`: how far apart the duel starts (110 cm, inside 罗汉拳's 120 cm reach).
  - `min_distance`, `arena_width`: how close the capsules can get, and how big the square floor is (1400 = 14 m each way).
  - `quick_step_distance` (70 cm forward/back), `quick_step_side_distance` (90 cm left/right), `quick_step_frames` (8), `quick_step_recovery` (6), `quick_step_stamina` (6), `quick_step_tap_window` (14): the quick step.
  - `dummy_pause`: frames the dummy waits between strings (40, about 0.7 s).
  - `parry_recovery`, `parry_perfect_recovery`, `parry_perfect_stagger`: how long you recover after a block or a perfect parry, and how long a perfect parry staggers the attacker.
  - `clash_recoil_frames`, `clash_lose_stagger`, `clash_perfect_stagger`, `clash_perfect_recovery`: the clash results.
  - `stamina_max`, `stamina_regen`, `stamina_regen_still`, `stamina_regen_delay`: max stamina and how fast it comes back.
  - `exhausted_until`, `exhausted_damage_pct`, `exhausted_walk_pct`: how long exhaustion lasts, the extra damage and the slower walk.
  - `parry_perfect_drain`: how much stamina a perfect parry takes from the attacker. Each move's own cost is the `stamina` column of `techniques.csv`.
  - 铁砂掌's power-up is three columns in `techniques.csv`: `empower_qi` (30), `empower_damage_pct` (200 = double) and `empower_armor` (true). Any technique can get them.
  - `qi_max`, `qi_start`, `qi_per_hit`, `parry_perfect_qi`, `clash_perfect_qi`: the Qi bar's size, what you start on, and what a hit, a perfect parry and a perfect clash give.
  - `dummy_chain_frame`: when the dummy presses for its next hit. 0 is a true combo you can't escape. Try 7 (the last frame of the window): it leaves gaps you can parry into.

- `game\data\feel.csv`: the feel kit, one row per kind of strike (hit, heavy_hit, empowered_hit, armor_hit, block, perfect_parry, clash_recoil, clash_break, clash_lost, perfect_clash, ko):
  - `hitstop`: frames the fight freezes (0 to 30).
  - `shake`: camera shake in cm (0 to 50). `push`: how far the camera moves in, in cm (0 to 100).
  - `spark`: size of the spark burst in cm (0 = none, up to 200).
  - `sound`: none, thud, smack, crack, block, ring, clang or gong.

- Knockback: the `knockback` column of `techniques.csv` (cm per hit), and in `rules.csv` `knockback_frames` (how long the slide takes) and `clash_recoil_push` (how far a same-weight clash pushes each fighter). The test script prints how close a string must start for every hit to stay in reach.
- Stepping in and hit width: the `advance` column of `techniques.csv` (cm the attacker steps forward) and `hit_angle` (how wide the strike is, in degrees; 360 or empty = all round).

Run `scripts\test.bat` after editing to check the sheets.

## 7. The questions for this build

1. **3D and the camera:** does walking round the dummy feel right, and does the camera keep both of you in view? Is it too close, too far, or swinging round too slowly or too fast?
2. **Step-in:** do the 罗汉拳 and 铁砂掌 steps feel like a fighter committing to a strike? Too little, too much, or about right?
3. **Quick step:** can you double-tap it reliably (is 0.23 s between taps too strict or too loose)? Is 70 cm enough to slip the dummy's punch and get to its side? Does 6 stamina feel cheap enough to use, but not so cheap you'd spam it?

Anything else that feels off is useful too, with roughly which move and what you expected.
