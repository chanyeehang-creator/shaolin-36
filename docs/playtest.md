# First playable test: how to play it

**New in this build: the demo roguelike run.** The game now opens on a 10-fight run (section "The demo run" below). Press **F4** at any time between fights to switch to the old practice arena (you against an opponent brain or the training dummy, with the loadout menu), and F4 again to go back to the run.

You (the jade-green stick figure, with the camera behind you) against an opponent (the slate-grey stick figure) on a square floor in full 3D. The opponent has a brain: it walks in to reach you, attacks, parries strikes it sees coming, steps aside, punishes your mistakes and backs off when it's out of breath (section 5, "The opponent brain").

## The demo run

A quick test of the roguelike loop. Every number below is in `game\data\run.csv` (open it in Excel, save as CSV UTF-8).

1. **Read a scroll:** 罗汉拳 or 虎形拳 (↑/↓ or W/S, then any button or Enter), then press the button you want it on. It stays there all run. You start with **100 HP, 80 stamina and 80 max Qi** (0 Qi to begin with).
2. **Chamber 0 has no enemy and no dummy:** you start facing the **doors** on the far side. The camera is free here: the left stick (or W/A/S/D) walks the way the camera looks and the right stick (or Z / C) turns it. Walk up to a door and a **□** shows over it: press **□** to go through (at a door □ enters it instead of doing its move; Enter still works too). Each door's name and rarity float above it, coloured by rarity. Cleared chambers work the same way.
3. **Fight the chamber's enemy.** The screen before each fight shows the door you took and the enemy: its brain, HP, stamina, Qi and its moves. Press any button to start. Win and you heal **30% of your max HP**; your Qi carries over (stamina is always full at the start of a fight). Then you take the door's reward and roam the cleared chamber the same way, until you pick the next door.
4. **The doors into chamber 1** are always two new categories: one from 轻功, 防身 or 炼气, the other from 内功, 擒拿 or 点穴. Win and you pick 1 of 2 moves from that category, then press an **empty** button for it.
5. **Every other door rolls a rarity.** Two doors in one choice are never the same kind. There are 2 doors out of most chambers, **3 out of chambers 4 and 8** (into 5 and 9), and **1, the boss door, out of chamber 9**.
   - **Common (60%):** **Medicine** (choose 1: max HP +20 up to 200, max stamina +10 up to 130, or max Qi +20 up to 130), or **Skill Enhance** (choose 1 of your moves and how it goes up: **more damage**, +15% each time, or **less stamina**, -15% each time; each move takes at most 4).
   - **Uncommon (30%):** a move from a category you don't know yet (pick 1 of 2).
   - **Rare (10%):** another move from a category you already know (pick 1 of 2).
   - **Super rare, once a run:** it can show on the doors into chambers 5 and 6 (35% each) and is certain on the doors into chamber 7 if it hasn't shown yet. It leads to a **mini boss** (+40 HP and the 方丈 brain). **Elixirs:** win two of max HP, stamina and Qi as a pair. **Ancient Scroll:** two Skill Enhances on two different moves.
   - A move is never replaced. When there's no new move to learn or all 8 buttons are full, uncommon and rare doors become Skill Enhance. Skill Enhance and the Ancient Scroll only show while some move is below 4 power-ups.
6. **The enemies grow:** enemy 1 knows only 罗汉拳 (plus walking and quick step), has 80 HP and the easiest brain. Each new enemy has **one more move** (2, 3, 4 ... up to 8), a new random set of them, and **10 more HP**. From enemy 3 on its 外功 can be 虎形拳 instead of 罗汉拳. Every enemy has 100 stamina and 100 max Qi, and starts each fight with 0 Qi. The brain gets smarter: 弟子 (novice) for chambers 1 to 3, 武僧 (monk) for 4 to 7, 方丈 (master) for 8 to 10. **Chamber 10 is the boss, with 200 HP.**
7. **Lose any fight and the run is over:** press any button to start again from the scroll.

During a fight or while roaming, Tab / Esc / pad Start pauses (your stats and power-ups are on the pause screen). Paused, **Enter gives the run up** and starts over. There is no restart key or "opponent stands still" in the run. The HUD's button line shows `+N` after a move with N power-ups.

The run sheet's keys: `fights`, `player_hp`, `player_stamina`, `player_qi`, `hp_up`/`hp_cap`, `stamina_up`/`stamina_cap`, `qi_up`/`qi_cap`, `upgrade_max`, `upgrade_damage_pct`, `upgrade_stamina_pct`, `win_heal_pct`, `rarity_common`/`rarity_uncommon`/`rarity_rare` (must add up to 100), `super_rare_from`/`super_rare_to`/`super_rare_pct`, `skill_choices`, `scroll_picks`, `miniboss_hp_bonus`, `miniboss_brain`, `miniboss_opening`/`boss_opening`, `miniboss_step_escape`/`boss_step_escape`, the door lists `first_doors_a`/`first_doors_b`, `three_doors` (the chambers reached through 3 doors), `enemy_hp`, `enemy_hp_step`, `boss_hp`, `enemy_stamina`, `enemy_qi`, `enemy_qi_start`, `enemy_moves_max`, `enemy_tiger_from`, and the lists `starters`, `enemy_first`, `enemy_waigong` and `brains` (one brain per fight).
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

Every fight button is a **slot**. Slots don't mean anything on their own: whatever technique you put on a slot is what that button does. The table's starting set is the **practice arena's** (F4); in the demo run you pick your own buttons.

| Slot | Gamepad (Dual Action) | Keyboard + mouse | Starts with | Its other move (switch in the pause menu) |
| --- | --- | --- | --- | --- |
| □ | 1 (left face button) | J or left click | 外功 罗汉拳 | 虎形拳 |
| △ | 4 (top) | I | 内功 铁砂掌 | 狮吼功 |
| ○ | 3 (right) | L | 点穴 封穴 | 无相劫指 |
| ✕ | 2 (bottom) | K or Space | 轻功 dash | 纵云梯 |
| L1 | 5 | Q or right click | 防身 化绵掌 | 硬气功 (hold) |
| L2 | 7 | Left Shift | | |
| R1 | 6 | E | 炼气 仙辽术 | 易筋经 |
| R2 | 8 | F | 擒拿 小擒拿手 (grab) | 龙爪手 |

| Other | Gamepad | Keyboard |
| --- | --- | --- |
| Walk (any direction) | Left stick or D-pad | W / A / S / D or the arrow keys |
| Quick step | Tap the stick or D-pad twice the same way | Tap W, A, S or D twice |
| Opponent stands still / fights again | 9 (Back / Select) | T |
| Loadout screen (pauses) | 10 (Start) | Tab or Esc |
| Restart the duel | Start after a KO, or the loadout screen's last row | Enter |
| Half speed on/off | | F1 |
| Floating text on/off | | F2 |
| Feel kit on/off (hit-stop, sparks, sound, camera) | | F3 |
| Demo run / practice arena | | F4 |

On the **pause menu**, the top seven rows are the categories. Highlight one and press ←/→ (or any button) to switch between its two moves: the new one goes on the same button. The next row, **Opponent's moves**, is "the same as yours" by default, so your opponent always fights with whatever you picked (←/→ switches it back to the starting set). Below that is the button list as before: move up and down to pick a technique (W/S, ↑/↓ or the D-pad), then press the button you want it on. Want 化绵掌 on ✕ instead of L1? Highlight 化绵掌 and press ✕. If something was already on that button, the two swap. Below the techniques, the **Opponent** row picks who you fight: press any button or ←/→ on it to go through 弟子 (novice), 武僧 (monk), 方丈 (master) and the old training dummy. Changing it starts a new duel. Select or T makes the opponent stand still (and fight again). The last row restarts the duel.

## 5. Reading the screen

The fighters are jointed stick figures. Every move has a **wind-up pose** and a **strike pose** (in `game/data/poses.csv`), timed to its frame data: the body reaches the wind-up halfway through the startup and holds it, cocked (that's the tell to read), snaps into the strike on the first active frame, exactly when the hit can land, and eases back to the stance over the recovery. The hitboxes don't change: the pose only shows them. Hits, staggers, grabs, knockdowns and quick steps have poses of their own. 罗汉拳 is posed from reference footage of the form: 1 a knee raise with the right arm arched overhead, then a side-on horse stance strike under the arch; 2 the right fist (down from the arch) cocked by the head, then a bow-stance right punch, forward and up; 3 the right knee chambered, then a high right front kick; 4 the palm drawn back as it steps in, then a deep bow-stance palm strike. A leg raised to about hip level (a kick, a knee raise) stays off the floor. To check poses without playing, double-click `scripts\pose-shots.bat`: it saves a picture of every pose (as you see an opponent, from its right and from its left) in the `pose-shots` folder and opens it. Add pose names after it to picture just those, e.g. `scripts\pose-shots.bat luohan_quan_3.windup luohan_quan_3.strike` from a Command Prompt.

They also change colour with what they are doing:

- **Wind-up, by weight**: **yellow** light, **orange** medium, **crimson-magenta** heavy, **violet** pure energy (狮吼功). The glow builds as the strike nears. A **grab flashes white and orange**: no parry or guard stops it, so strike it or step back. A faint wedge in the same colour on the floor shows where the move will hit: as long as its reach and as wide as its hit angle. (Other wind-ups, such as a heal, are plain yellow.)
- **Red**: active, the hit is live. The floor wedge turns red.
- **Green / light blue**: your 化绵掌 window. Green is the perfect part (4 frames, after a 3-frame block), light blue the block parts either side.
- **Dark blue**: recovery, can't act.
- **White**: hit and frozen (hitstun).
- **Purple**: staggered after a perfect parry or a lost clash. Wide open: hit them!
- **Grey**: recoil (a same-weight clash) or the short recovery after a successful parry.
- **Pale mint**: a quick step.

**Floating text** over the capsules shows what just happened: damage like **-6**, **Blocked!**, **Parried!** (perfect), **Stagger!**, **CLASH!**, **Too early! / Too late!** for a missed parry, and the stamina ones: **+10 stamina**, **-15 stamina**, **Exhausted!**, **Breath back**, **No stamina!** (you pressed a move while exhausted). It's a playtest aid only: press **F2** to switch it off or on.

The top lines show both HP bars and stamina bars, what each fighter is doing with its frame number, and **"chain now!"** while you are in the window where pressing the same button again starts the next hit. When the same fighter lands hits in a row before the other recovers, you see **"N hit combo"**.

**About 化绵掌 (the parry):** it is a tap, not a hold. Each press winds up for 2 frames, opens a 17-frame window (about 0.28 s), then has a 10-frame cooldown if nothing hit it, so to block a whole string you tap once for each hit. When the dummy's hit lands in your window:

- **First 3 frames: Blocked (early).** Leeway for pressing a little early: a plain block instead of a hit.
- **Next 4 frames: PERFECT PARRY.** No damage, the dummy's attack is stopped and it is staggered (purple) for 30 frames, and you recover in 4. Hit it straight away. (Press 5 to 8 frames before the hit lands.)
- **The last 10 frames: Blocked.** No damage, but the dummy isn't interrupted: its string carries on, so tap again for its next hit. After any block you recover in 5 frames, then you can tap again.
- **Outside the window**, the hit lands as normal and the screen tells you whether you were too early or too late, and by how many frames.

**Stamina:** the blue bar next to each HP bar (100 to start). Both you and the dummy play by the same rules:

- **Every move costs stamina when you press it.** Each 罗汉拳 hit costs its own (8, 8, 9, 14: 39 for the whole string). Each 化绵掌 tap costs 10, whether it blocks, misses or is perfect, so blocking a full string costs 40.
- **A perfect parry gives your 10 back** and the attacker loses 15 more ("+10 stamina" / "-15 stamina" float up).
- **Clashes cost a little:** 5 each for a same-weight clash, 8 for the side that loses.
- **Getting it back:** nothing while you're doing a move. Half a second (30 frames) after your last move, it refills at 40 per second standing still, or 25 per second while walking or reeling from a hit.
- **At 0 you're exhausted** (the bar turns orange, "Exhausted!" floats up): you can't use any move that costs stamina, so **no 化绵掌, your guard is broken**, and no quick step or 轻功 either. You still walk at full speed (new: only opponents slow to half speed when out of breath), but you take 20% extra damage (a 6-damage hit does 7, the 12-damage finisher does 14). It lasts until the bar is back to 50: about 1.75 s if you stand still. Then "Breath back".
- The dummy gets exhausted too: its 3rd string in a row empties it (on the finisher), and it rests about 2.5 s before attacking again. That's your moment: it can't attack or parry and takes extra damage.

**Qi (气):** the gold bar after stamina, 0 to 100. Every duel starts you on 0, and it never refills by itself: you earn it by fighting well.

- **+2 for every hit that deals damage** (a full 罗汉拳 string is +8). A heavy move that wins its clash counts too.
- **+10 for a perfect parry** and **+10 for a perfect clash** ("+10 Qi" floats up in gold). A plain block, a same-weight clash or a lost clash gives nothing.
- It stops at 100.
- **铁砂掌 spends it** (below), and so does **仙辽术** (10 Qi for 10 HP). In a run your Qi will carry over from one duel to the next.

**铁砂掌 (iron palm), on △:** one heavy palm strike instead of a string.

- Wind-up 36 frames (0.6 s), hit 4 frames, recovery 36 frames (0.6 s): 76 frames in all, about 1.3 s. Easy to see coming and very punishable if it misses.
- Costs 20 stamina, deals 15 damage, freezes the dummy for 40 frames, reaches 130 cm. It's heavy, so it wins normal clashes against every 罗汉拳 hit.
- **With 30 Qi or more, it powers up automatically:** it spends 30 Qi ("Qi 铁砂掌!" and "-30 Qi" float up), deals **double damage (30)**, and has **hyper armor** until it lands: the capsule winds up in deep gold instead of yellow, and if the dummy hits you then, you still lose the HP ("Armor!") but your palm keeps going. The armor ends once the palm has struck, so the 0.6 s recovery is still open.
- Armor doesn't beat 化绵掌: a perfect parry still staggers it. Clashes still work the normal way.
- Under 30 Qi it's the plain palm and your Qi is kept.

**点穴 封穴 (seal), on ○:** one finger strike that seals your opponent's 外功.

- Wind-up 30 frames (0.5 s, standing still), then a **140 cm lunge** over 8 frames (0.13 s) with the hit going along with your body, then recovery 60 frames (1 s) if it misses: 98 frames in all. **If it hits, you're free 24 frames (0.4 s) after the hit**, a few frames before their 30-frame hitstun ends, so a landed 封穴 doesn't leave you open; a miss (or a block) does. The lunge sits between the quick step (70 / 90 cm) and the 轻功 dash (200 cm), so 封穴 connects from up to 255 cm away. The wind-up is long enough to parry or step on reaction, and a miss leaves you open for a second (a hit during the recovery interrupts it like any move).
- Costs 25 stamina (a 罗汉拳 hit costs 8), deals only 4 damage, freezes for 30 frames, reaches 115 cm from your body. It's light and can be clashed (Chan): read its long wind-up and clash it back.
- **On a landed hit the target can't use 外功 (罗汉拳) for 5 s.** 铁砂掌 is 内功 (a heavy strike), so it still works. A purple **【封】** icon and the seconds left show by their HP bar. 铁砂掌, parry, 轻功, quick step, 点穴 and 仙辽术 still work. A blocked, parried or dodged 点穴 seals nothing.

**仙辽术 (healing 炼气), on R1:** spend 10 Qi to heal 10 HP.

- Wind-up 30 frames (0.5 s), channel 60 frames (1 s, the capsule glows green), then the heal lands ("+10 HP"), then 20 frames (0.33 s) to finish. 1.8 s in all.
- Placeholder sounds (made in code until there are real ones): a soft rising hum through the wind-up and channel, and a bright chime when the heal lands. A broken heal cuts the hum. F3 turns them off with the rest of the feel kit.
- No armor: **any hit in the first 1.5 s breaks it** ("Heal broken!"), no HP back, and the 10 Qi is gone. Use it when you've made space. Costs 5 stamina.

**The opponent uses both:** it throws 封穴 like its other light attacks, but not while you're already sealed (and it can't use 外功 while you have it sealed). It heals when it's hurt, has the Qi, and is well out of your reach (the brain sheet's `heal` chance: novice 20, monk 40, master 60). It also counts your heal as an opening and runs in to punish it.

**擒拿手 (grab), on R2:** a fast, short grab that beats a parry, and a throw that knocks down.

- Reach in 10 frames (0.17 s, stepping 15 cm), then 4 frames to catch. It only reaches 100 cm (the capsules touch at 80), so you have to be close. Costs 15 stamina. No cooldown: the wind-up and recovery are the cost.
- **Caught:** both capsules turn orange. You hold them for 30 frames (0.5 s), then throw them **130 cm for 14 damage** and **knock them down**. You're free 35 frames after the throw; they can be up 40 frames after it at the soonest, so you're a little ahead, but too far away for a free hit.
- **Missed:** 32 frames (0.53 s) of recovery. A sure punish.
- **What it beats:** a 化绵掌 parry, even a perfect one ("Parry can't stop a grab!"); hyper armor (a Qi 铁砂掌 wind-up); clashes (a grab never clashes).
- **What beats it:** a strike that lands on the same tick or earlier (your 罗汉拳 hit 1 out-speeds it if you press first), stepping back out of its 100 cm, or a 轻功 dash's invincible frames (a just dodge). It can't catch a fighter in hitstun or on the floor. So it's rock-paper-scissors: **strike beats grab, grab beats parry, parry beats strike.**
- Held, you can't do anything, not even the dash.

**Knocked down:** the thrown fighter lies flat on the floor (brown). **Nothing can hit you while you're down**: every move passes over you (later moves can be marked `hits_down` in techniques.csv to change that).

- For the first 20 frames (0.33 s) you can't do anything. Then **press any button to rise** (20 frames, 0.33 s, the capsule tips back up) or **hold a direction to roll away** (150 cm over 24 frames, 0.4 s, the capsule spins along the floor; hold back to roll away from your opponent). Both are invincible. The button that rises doesn't also start a move.
- Do nothing and you rise by yourself after 90 frames (1.5 s).
- **Exhausted (stamina hit 0 and hasn't refilled to 50 yet): you stay down** until it has, however long that takes.
- The opponent gets up as soon as it can: it rolls back with its `step` chance, otherwise it rises. It doesn't attack you while you're down or getting up.
- The numbers are `down_min`, `down_max`, `rise_frames`, `roll_frames` and `roll_distance` in rules.csv.

**The opponent grabs too:** at each choice in grab reach it rolls the brain sheet's `grab` chance first (novice 10, monk 20, master 30). It never tries to parry your grab; it steps or dashes if it reacts in time, but at 10 frames most grabs are too fast to react to.

**Clashes (拼招):** if your hit lands in the last frames of the dummy's wind-up, the moves clash. Same weight: both bounce off (grey). Heavier move: it breaks through. Lighter move: it gets knocked aside. Hit the very first frames of that window and it's a **PERFECT CLASH**: the dummy is staggered whatever the weights. Weights (Chan's): 罗汉拳 hit 1 medium, hits 2 and 3 light, hit 4 medium; 铁砂掌 heavy (with or without Qi); 封穴 light. So hit 1 against the finisher bounces off, and hit 1 breaks through a 封穴 wind-up.

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
| 罗汉拳 finisher | 70 cm (its lunge follows them 10 cm), so the string ends with them out of reach |
| 铁砂掌 (plain or Qi) | 50 cm |
| Same-weight clash | both fighters 25 cm, a real reset (110 cm apart becomes 160) |
| Hit on hyper armor, a parry, a lost clash | nobody moves |

Because of the small pushes, the full 罗汉拳 string only stays in reach if you start it **135 cm away or closer** (hit 1's 125 cm reach plus its 10 cm step in; hits 2 and 3 each step 15 cm to keep up).

**The opponent brain (Stage 3).** The opponent is now played by a brain that uses the same buttons and stick you do: the same three techniques (罗汉拳 on □, 铁砂掌 on △, 化绵掌 on L1), the same stamina, Qi and frame data, and the quick step. It never reads your buttons. It only "sees" what you do a little late, like a person reacting. What it does, in order of priority:

1. **Carries on its string:** at each link of its 罗汉拳 it decides whether to press for the next hit. When it does, it presses on the first chain frame, so the rest of the string is a true combo once hit 1 lands.
   - **Getting out of your combo (new).** Once it notices it's been hit (its reaction time), it may dash out with a 轻功 dash if it has one (its `step` chance), and only if your combo caught it **doing nothing**: hit during its own attack, its string or just after one (its opening), it always takes the rest of your combo. Ordinary opponents never quick-step out. The mini boss and the boss can, also only when caught doing nothing: the mini boss 30% of the time, the boss 60% (`miniboss_step_escape` and `boss_step_escape` in `run.csv`). The top line says "stepped out of your combo".
2. **Answers a strike it sees coming:** a clash (拼招) when it can: it times its own strike to land in your move's clash window (the perfect frames when it can, which break your move and give it 10 Qi; the plain frames only with a move at least as heavy as yours). Otherwise a parry timed for your hit frame (sometimes aiming for the perfect part, sometimes the plain block part), or else a quick step to the side.
3. **Backs off to breathe** when its stamina is low (it walks back to 250 cm and waits). It still parries if it can pay for it.
4. **Punishes:** if it sees you recovering from a move, staggered or recoiling while you're in its reach, it may attack straight away.
5. **Otherwise** it walks in until its attacks reach (about 125 cm), with the odd quick step forward to close a long gap. In reach, every so often it chooses: attack (with enough Qi it always picks the Qi 铁砂掌), throw its parry on a guess, circle round you, or wait.

The top line shows what it is up to, for example "closing in", "circling you", "saw your strike, parrying", "parrying on a guess" or "backing off to breathe".

Three brains to pick from on the loadout screen (you start against the monk):

| Brain | Reaction | Clashes what it sees | Parries what it sees | Aims for perfect | Guesses | Punishes | Attacks when it chooses |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 弟子 (novice) | 24 frames (0.4 s) | 5% | 15% | 20% | 5% | 30% | 35%, every 30 frames (0.5 s) |
| 武僧 (monk) | 18 frames (0.3 s) | 15% | 40% | 35% | 15% | 60% | 50%, every 20 frames (0.33 s) |
| 方丈 (master) | 12 frames (0.2 s) | 35% | 65% | 60% | 25% | 90% | 60%, every 12 frames (0.2 s) |

Things worth knowing when you fight it:

- **Every duel starts 300 cm apart** and the brain waits half a second (30 frames) before it moves, so a new duel or a restart never opens with you getting hit. Walking in at 200 cm per second, it needs about 1.4 s in all to reach you. (The old training dummy never walks, so walk up to it.)
- **罗汉拳 hit 1 lands 13 frames (0.22 s) after you press**, faster than any of the brains can react (12 frames to see it plus 2 to open a parry is already 14). So your opening jab only gets parried when the brain **guesses**. Its slow moves are different: your 铁砂掌 winds up for 36 frames (0.6 s), and every brain sees that coming in time.
- **It clashes on reaction, so only slow strikes.** Your 铁砂掌's clash window is frames 28-35 of its 36-frame wind-up (perfect on 28-29). Its quickest clashable move is 罗汉拳 hit 1 (13 frames), and 罗汉拳 is lighter, so it has to hit the perfect frames: it must press by frame 16. The master sees your palm on frame 12 and can; the monk (18) and the novice (24) see it too late. Your 封穴 (30-frame wind-up, window frames 24-29, perfect 24-25) is light, so the master's medium 罗汉拳 breaks through it in the plain frames: a hit, not just a bounce. The top line shows "clashing into it (拼招)" while it waits for its frame. 擒拿手 can't be clashed.
- **A wrong guess is your chance.** When it throws 化绵掌 and nothing comes, it's stuck for 29 frames (0.48 s) and paid 10 stamina. Wait for the parry colours to pass, then hit it.
- **After it blocks one hit, it can block the next only if the gap is long enough.** A block costs 5 frames of recovery, a new parry takes 2 frames to open and its window starts with 3 block frames. 罗汉拳 hit 3 lands 9 frames after hit 2, so a parry re-pressed straight after a block is in time to block it. The same is true for you when it attacks you.
- **It gets tired like you do.** Every parry costs it 10 stamina and every strike its own cost. When it backs off to breathe, chase it.
- Its choices come from a random roll that starts the same way every time (a "seed"), and the seed changes each new duel, so duels differ but the game stays fully repeatable for testing.

**Full 3D: the floor, the camera and walking.** The fight is no longer on a line. You and the dummy stand on a 14 m square floor (the grid lines are 1 m apart), and you can walk in any direction.

- **The camera** sits behind you (4.4 m back, 3.5 m up) and a little to your right (2.2 m), always looking at the dummy (lock-on). Each fighter has a small dark "nose" on its face that shows which way it looks.
- **Walking is relative to the dummy:** W / stick up walks straight at it, S / stick down backs away, A and D (stick left and right) circle round it. Up close, walking into it slides you round its side. The stick walks as fast as you push it; full speed is 200 cm per second, diagonals included.
- **Both fighters turn to face each other** while they're free (standing, walking, quick-stepping or reeling from a hit). **Once a fighter attacks, the turning stops:** the attack aims where the other fighter stood on the frame it started, and every follow-up hit of that string goes the same way. Turning comes back when the string is over. So if you side-step the dummy's first punch, the rest of its 罗汉拳 string carries on into empty air.
- **Hits have a width now (hit angle).** A strike only lands if you're inside its wedge, not just inside its reach. Step to the side during the wind-up and it can miss you.

| Move | Reach | Hit angle (wedge) |
| --- | --- | --- |
| 罗汉拳 hit 1 | 125 cm | 60° (30° either side) |
| 罗汉拳 hit 2 | 120 cm | 60° |
| 罗汉拳 hit 3 | 125 cm | 70° |
| 罗汉拳 finisher | 140 cm | 90°: a wide hook, harder to step round |
| 铁砂掌 | 130 cm | 60° |

**Attacks step in (外功).** Fighters no longer punch from where they stand: each 外功 strike moves the attacker forward along its aim during the wind-up, so the step is done as the hit comes out. This is the same for you and the dummy. It stops when the two of you are touching (80 cm apart). **罗汉拳 is different (new):** it holds its ground through the wind-up and its step comes on the hit instead (a 10-15 cm lunge over the active frames), so the punch or kick drives the body forward. Same distance as before, so its reach is unchanged.

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
- **Quick step out of a combo (new).** Caught by a string? Double-tap a direction while you're still reeling (white) and you step out of the hitstun at once, like the 轻功 dash escape: same 6 stamina, same invincible slide, and it never gives a just dodge. Not while exhausted, and not while 点穴-locked (【困】). "Escaped!" floats up when it works.
- **Invincible while it slides, open in its recovery (new).** The whole 8-frame slide (0.13 s) is invincible: a strike that lands then passes through you. The 6-frame recovery is not, so a strike that lands after the slide hits you as normal. That's fewer invincible frames than the 轻功 dash (10 frames, 0.17 s), and unlike the dash a strike it slips gives **no just dodge and no Qi**. The capsule turns pale cyan while it's invincible. A side step as the dummy starts its 罗汉拳 wind-up (yellow) still takes you out of the punch's wedge, and you end up at its side, in reach for your own punch. The number is `quick_step_invincible` in `rules.csv` (at most `quick_step_frames`).
- Left and right steps curve round the dummy, so you stay about the same distance away.
- Pressed while you're busy (in a move, or just hit), it waits up to 6 frames (the input buffer) and comes out as soon as you're free, like a button press.
- It isn't a 轻功 technique. Later 轻功 slot techniques will be the real dodges; the quick step is the bare minimum everyone keeps.

## 6. Tuning while you play

All the numbers are in two sheets you can open in Excel (save as **CSV UTF-8**), then close the game and double-click `play.bat` again:

- `game\data\techniques.csv`: each technique's frames, damage, reach, chain window, perfect parry frames.
- `game\data\rules.csv`: HP, input buffer, and the test settings added for this step:
  - `walk_speed`: how fast you walk, in cm per second (200).
  - `start_distance`: how far apart the duel starts (300 cm, out of every attack's reach, so nobody is hit the moment a duel starts or restarts).
  - `opponent_ready`: frames the opponent brain waits at the start of a duel before it moves (30, half a second).
  - `min_distance`, `arena_width`: how close the capsules can get, and how big the square floor is (1400 = 14 m each way).
  - `quick_step_distance` (70 cm forward/back), `quick_step_side_distance` (90 cm left/right), `quick_step_frames` (8), `quick_step_recovery` (6), `quick_step_invincible` (8, the whole slide), `quick_step_stamina` (6), `quick_step_tap_window` (14): the quick step.
  - `dummy_pause`: frames the training dummy waits between strings (40, about 0.7 s).
  - `parry_recovery`, `parry_perfect_recovery`, `parry_perfect_stagger`: how long you recover after a block or a perfect parry, and how long a perfect parry staggers the attacker.
  - `clash_recoil_frames`, `clash_lose_stagger`, `clash_perfect_stagger`, `clash_perfect_recovery`: the clash results.
  - `stamina_max`, `stamina_regen`, `stamina_regen_still`, `stamina_regen_delay`: max stamina and how fast it comes back.
  - `exhausted_until`, `exhausted_damage_pct`, `exhausted_walk_pct`: how long exhaustion lasts, the extra damage and the opponents' slower walk (you always walk at full speed).
  - `parry_perfect_drain`: how much stamina a perfect parry takes from the attacker. Each move's own cost is the `stamina` column of `techniques.csv`.
  - 铁砂掌's power-up is three columns in `techniques.csv`: `empower_qi` (30), `empower_damage_pct` (200 = double) and `empower_armor` (true). Any technique can get them.
  - `qi_max`, `qi_start`, `qi_per_hit`, `parry_perfect_qi`, `clash_perfect_qi`: the Qi bar's size, what you start on, and what a hit, a perfect parry and a perfect clash give.
  - `dummy_chain_frame`: when the training dummy presses for its next hit. 0 is a true combo you can't escape. Try 7 (the last frame of the window): it leaves gaps you can parry into.

- `game\data\feel.csv`: the feel kit, one row per kind of strike (hit, heavy_hit, empowered_hit, armor_hit, block, perfect_parry, clash_recoil, clash_break, clash_lost, perfect_clash, ko):
  - `hitstop`: frames the fight freezes (0 to 30).
  - `shake`: camera shake in cm (0 to 50). `push`: how far the camera moves in, in cm (0 to 100).
  - `spark`: size of the spark burst in cm (0 = none, up to 200).
  - `sound`: none, thud, smack, crack, block, ring, clang or gong.

- Knockback: the `knockback` column of `techniques.csv` (cm per hit), and in `rules.csv` `knockback_frames` (how long the slide takes) and `clash_recoil_push` (how far a same-weight clash pushes each fighter). The test script prints how close a string must start for every hit to stay in reach.
- Stepping in and hit width: the `advance` column of `techniques.csv` (cm the attacker steps forward) and `hit_angle` (how wide the strike is, in degrees; 360 or empty = all round).

- `game\data\brains.csv`: the opponent brains, one row each (novice, monk, master). Add a row to make a new opponent; it appears on the loadout screen.
  - `reaction`: frames before it notices what you do (0 to 60; a person takes 15 to 20).
  - `think_every`: in reach with nothing to react to, how often it makes a new choice, in frames.
  - Chances out of 100: `aggression` (attack at a choice), `punish` (attack when it sees you open), `chain` (press for the next hit of its string), `parry` (parry a strike it sees coming), `perfect` (aim that parry at the perfect frames), `step` (quick step aside from a strike it didn't parry, or forward to close a long gap), `circle` (circle round you instead of waiting), `guess` (throw its parry on a guess), `heavy` (pick 铁砂掌 over 罗汉拳).
  - `rest_below`: below this much stamina it backs off to breathe.

Run `scripts\test.bat` after editing to check the sheets.

## The second move in each category

Switch them in the pause menu (Tab, Esc or pad Start). The numbers are in `techniques.csv` and `rules.csv`.

- **虎形拳 (外功, □).** Three medium hits, 8 + 8 + 10 = 26 damage (罗汉拳: 31). Each hit leaps: hit 1 springs **125 cm** forward (封穴's lunge is 140), hits 2 and 3 45 cm (罗汉拳 steps 10 to 15). A nearly even beat, hit - hit - hit: hit 1 winds up 18 frames, then the hits land **about 17 frames apart** (about 30% slower than before), against 罗汉拳's hit, hithit, hit. Tap □ in each chain window like 罗汉拳; it connects from 245 cm away.
  **Its poses (new, from the reference footage):** hit 1 stands still in the tiger form (square on in a horse stance, one claw curled over the head, the other low), then **pounces**: a diving lunge with both claws out, the hit carried along with the body over 0.1 s. Hit 2 comes up out of the roll in a low crouch (moving forward 45 cm as it does) and strikes with the left claw from a bow stance, the right arm arched over the head. Hit 3 stands on the left leg with the right knee raised, aiming with the left hand and charging the right at the waist, then **stamps forward** 45 cm as the right hand shoots out. From 150 cm the pounce lands on its 2nd frame, so the hits land 19 then 17 frames apart. The forward roll itself can't show (each hit has one wind-up pose), so it's the crouch.
- **狮吼功 (内功, △).** Stand still and roar: 0.4 s wind-up, then a ring 220 cm all round you. Needs 5 Qi and spends it; you can't use it with less. 20 damage and knocks down. It's **pure energy**: no light, medium or heavy move can clash it (a strike into its wind-up just hits you), and two energy moves that meet cancel out ("Cancelled!"), whatever they cost. 化绵掌 can block it but never perfect-parry it; 硬气功 takes half. 0.6 s recovery if it misses.
- **纵云梯 (轻功, ✕).** A high jump: 300 cm the way you hold the stick (none = straight at your opponent), over their head, so from 150 cm away you land 150 cm behind them. Invincible for its first 6 frames but never a just dodge, and you can be hit in the air after that. 15 stamina like the dash. It can't break out of a combo (only the dash can).
  **Landing behind them loses you:** the opponent stays facing where you took off and keeps going the way it was moving or attacking. It finds you again only once it **stands still** for a while: the novice 3 s, the monk 1.5 s, the master as soon as it stops. Hitting it or knocking it down makes it find you at once. Even then it turns round over 0.3 s instead of snapping back, so you get a moment at its back. While it is lost the HUD says "lost you after your jump". You never lose your opponent yourself.
- **无相劫指 (点穴, ○).** Tap ○ twice: two quick light finger strikes (3-frame wind-up; tap again in the 6-frame chain window, like 罗汉拳). 5 + 5 damage. Very close: 95 cm (touching is 80). If **both** land, your opponent is locked for 5 s: no quick step, no 轻功 at all, and half walking speed (【困】 by the HP bar). Recovery 21 frames, like the end of a 外功 string, so it works as a second 外功.
- **硬气功 (防身, L1).** Press and **hold**: the guard is up from the first frame and stays while you hold. Light and medium strikes deal nothing; heavy (铁砂掌) and energy (狮吼功) deal half. No hitstun, no knockback, no knockdown, no 点穴 seal or lock gets through. Raising it is free; each block costs stamina (6 light, 10 medium, 16 heavy), and at 0 stamina the guard drops and you're exhausted. No parry. Grabs go through it, and it stays facing the way it was raised, so a quick step or 纵云梯 round to its back gets through. Let go: 8 frames to lower it.
- **龙爪手 (擒拿, R2).** A grab like 小擒拿手 (0.2 s reach-in, 100 cm). Once it catches, **tap R2** to strike, up to 3 times, 7 damage each (21 in all; 小擒拿手's throw does 14). The 3rd knocks down and sends them 100 cm. Taps closer than 0.2 s apart wait their turn. Each window is 0.75 s: tap nothing and they slip free while you finish your recovery.
- **Breaking out of a grab (new, any grab).** Grabbed? Press **your own grab button** (whichever grab you have) to break out: both of you reel back 0.33 s and step back 40 cm each, Street Fighter style (from touching range you end up about 160 cm apart). Against 小擒拿手 you can do it any time before the throw; against 龙爪手 only before its 1st or 2nd strike: the 3rd window is too late. The novice has no grab, so it never breaks out; the monk sometimes does in time; the master usually does.
- **易筋经 (炼气, R1).** The same channel as 仙辽术 (0.5 s wind-up, 1 s channel, 0.33 s finish) for 10 Qi. When it finishes, all your damage is x1.5 for **10 s** (【气 x1.5】 by the HP bar). Hit before then, nothing, and the Qi is gone.

**Every block works the same** (化绵掌's block frames or 硬气功): light and medium strikes deal nothing, heavy and energy strikes deal half, and a blocked 点穴 applies no seal or lock. A 化绵掌 perfect parry still stops everything except energy.

**Renames:** the parry category 化劲 is now **防身** (the sheet still accepts `huajin`), and the old grab is **小擒拿手**.

## 7. The questions for this build

**First, the demo run:** play a few runs. Is the climb from chamber 1 to the boss about right? Which rewards feel worth taking and which never get picked? Do the doors give you real choices? Does the super rare door show up often enough? Are 15% per power-up and the 30% heal too much or too little? Does an enemy with a random set of moves read as a different opponent each time?

0. **The second moves:** switch each category in the pause menu and try it against an opponent using the same move. Which numbers feel off (虎形拳's leaps, 狮吼功's ring and Qi cost, 纵云梯's distance, 无相劫指's lock time, 硬气功's stamina per block, 龙爪手's tap window, 易筋经's 1.5x)?
1. **Does it feel like a fight now?** Against the monk, does the opponent close in, attack and defend in a way you can read? Anything it does that looks robotic or unfair?
2. **Difficulty:** is the novice easy enough to learn on, and the master hard but beatable? Which numbers would you change first (reaction, parry chance, how often it attacks)?
3. **Guessing:** can you spot its guessed parries and punish them? Does it guess too often or too rarely?
4. **What should it learn next?** For example: clashing on purpose, keeping its distance with quick steps, or holding back its string when you block.

Anything else that feels off is useful too, with roughly which move and what you expected.
