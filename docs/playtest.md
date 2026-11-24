# First playable test: how to play it

This is the Stage 1 step 5 test: you (the orange capsule, on the left) against a training dummy (the dark red capsule) that throws the 罗汉拳 string over and over. The point is to feel the numbers, so play for a few minutes and then answer the two questions at the bottom.

## 1. Get the new version on your PC

You already have the repo at `D:\GitHub\shaolin-36` in GitHub Desktop.

1. Open **GitHub Desktop**.
2. Click **Fetch origin** (top bar).
3. To play **before** the pull request is merged: click **Current branch** (top bar), find `claude/project-thread-uhc6ej` in the list and click it. If it isn't listed, click Fetch origin again.
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
| Walk left / right | Left stick or D-pad | A / D or ← / → |
| Dummy stands still / attacks again | 9 (Back / Select) | T |
| Loadout screen (pauses) | 10 (Start) | Tab or Esc |
| Restart the duel | Start after a KO, or the loadout screen's last row | Enter |
| Half speed on/off | | F1 |

On the **loadout screen**: move up and down to pick a technique (W/S, ↑/↓ or the D-pad), then press the button you want it on. Want 化绵掌 on ✕ instead of L1? Highlight 化绵掌 and press ✕. If something was already on that button, the two swap. Below the techniques, one row turns the dummy's attacks on and off (Select or T does the same from the fight), and the last row restarts the duel.

## 5. Reading the screen

The capsules change colour with what they are doing:

- **Yellow**: startup (winding up). A faint yellow bar on the floor shows how far the move will reach.
- **Red**: active, the hit is live. The floor bar turns red.
- **Green / light blue**: your 化绵掌 window. Green is the perfect part (the first 4 frames), light blue the rest.
- **Dark blue**: recovery, can't act.
- **White**: hit and frozen (hitstun).
- **Purple**: staggered after a perfect parry or a lost clash. Wide open: hit them!
- **Grey**: recoil (a same-weight clash) or the short recovery after a successful parry.

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

## 6. Tuning while you play

All the numbers are in two sheets you can open in Excel (save as **CSV UTF-8**), then close the game and double-click `play.bat` again:

- `game\data\techniques.csv`: each technique's frames, damage, reach, chain window, perfect parry frames.
- `game\data\rules.csv`: HP, input buffer, and the test settings added for this step:
  - `walk_speed`: how fast you walk, in cm per second (200).
  - `start_distance`: how far apart the duel starts (110 cm, inside 罗汉拳's 120 cm reach).
  - `min_distance`, `arena_width`: how close the capsules can get, and how wide the floor is.
  - `dummy_pause`: frames the dummy waits between strings (40, about 0.7 s).
  - `parry_recovery`, `parry_perfect_recovery`, `parry_perfect_stagger`: how long you recover after a block or a perfect parry, and how long a perfect parry staggers the attacker.
  - `clash_recoil_frames`, `clash_lose_stagger`, `clash_perfect_stagger`, `clash_perfect_recovery`: the clash results.
  - `stamina_max`, `stamina_regen`, `stamina_regen_still`, `stamina_regen_delay`: max stamina and how fast it comes back.
  - `exhausted_until`, `exhausted_damage_pct`, `exhausted_walk_pct`: how long exhaustion lasts, the extra damage and the slower walk.
  - `parry_perfect_drain`: how much stamina a perfect parry takes from the attacker. Each move's own cost is the `stamina` column of `techniques.csv`.
  - 铁砂掌's power-up is three columns in `techniques.csv`: `empower_qi` (30), `empower_damage_pct` (200 = double) and `empower_armor` (true). Any technique can get them.
  - `qi_max`, `qi_start`, `qi_per_hit`, `parry_perfect_qi`, `clash_perfect_qi`: the Qi bar's size, what you start on, and what a hit, a perfect parry and a perfect clash give.
  - `dummy_chain_frame`: when the dummy presses for its next hit. 0 is a true combo you can't escape. Try 7 (the last frame of the window): it leaves gaps you can parry into.

Run `scripts\test.bat` after editing to check the sheets.

## 7. The two questions

1. **罗汉拳 rhythm:** does pressing on the "chain now!" beat feel natural? Is the 4-hit string too fast, too slow, or about right?
2. **化绵掌:** with the dummy attacking, how often do you get PERFECT PARRY and Blocked? Do 4 perfect frames and 10 block frames feel too strict, too easy, or fair? Is the stagger after a perfect parry long enough to punish?

Anything else that feels off is useful too, with roughly which move and what you expected.
