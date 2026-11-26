using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// One fighter's rules state, advanced one frame per <see cref="Tick"/> at
    /// <see cref="Sim.TicksPerSecond"/>. It knows nothing about buttons or devices:
    /// input arrives as "slot N pressed", and the slot's technique decides what happens.
    ///
    /// Timing, with the tick a move starts on counted as its frame 0:
    /// <list type="bullet">
    /// <item>An idle fighter pressing a filled slot starts that technique on the same tick.</item>
    /// <item>The move then runs exactly startup + active + recovery ticks, and on the next
    /// tick the fighter is idle and may start another move on that very tick.</item>
    /// <item>A stun of N frames applied on tick t (the hit frame) ends on tick t + N, when the
    /// fighter is idle again and may act.</item>
    /// </list>
    ///
    /// Strings: during the first <see cref="Data.Technique.ChainWindowFrames"/> recovery frames of
    /// a technique that has a next hit, pressing the slot the string started from starts the
    /// next hit on that tick, cutting the rest of the recovery. The next hit comes from
    /// <see cref="Fighters.Loadout.Book"/>. Chaining doesn't need the hit to have landed: a
    /// whiffed string still plays out. Once the window passes, the string is over.
    ///
    /// Input buffer: a press that can't be used on its tick (busy, or a different slot
    /// during a chain window) is kept and tried again on each of the next
    /// <see cref="InputBufferFrames"/> ticks, so mashing a little early still chains on the
    /// first chain frame. Only the newest unused press is kept. A new press on a tick is tried
    /// before the buffered one. With a buffer of 0, presses made while busy are dropped.
    ///
    /// Stamina (only when the fighter is given <see cref="StaminaRules"/>; a <see cref="Combat.Duel"/> always does):
    /// <list type="bullet">
    /// <item>Starting a technique, or chaining to the next hit, pays its stamina cost on that tick.
    /// Any amount left is enough: a move that costs more than you have still comes out and leaves you at 0.</item>
    /// <item>Reaching 0 makes the fighter <see cref="IsExhausted"/> until stamina is back up to
    /// <see cref="Fighters.StaminaRules.ExhaustedUntil"/>. While exhausted, techniques that cost stamina
    /// can't be started (so no parry: the guard is broken); presses wait in the buffer as usual.</item>
    /// <item>Regen: none on ticks spent in a move. After <see cref="Fighters.StaminaRules.RegenDelayFrames"/>
    /// ticks with no move and no stamina lost, stamina comes back at the still rate while idle and not
    /// walking, else (walking, hitstun, stagger, recoil) at the normal rate.</item>
    /// </list>
    ///
    /// Qi (only when the fighter is given a max Qi above 0; a <see cref="Combat.Duel"/> always does):
    /// Qi is earned in the fight (<see cref="GainQi"/>, never above <see cref="MaxQi"/>) and spent by
    /// techniques with a Qi cost: such a technique only starts with at least its cost in Qi, and pays it
    /// on the tick it starts. Qi does not regenerate. A technique with an empowered version
    /// (<see cref="Data.Technique.EmpowerQi"/>) is empowered automatically when, after paying any Qi
    /// cost, the fighter still has EmpowerQi Qi: it spends that too (see <see cref="Empowered"/>).
    ///
    /// Quick step (only when the fighter is given <see cref="QuickStepRules"/>; a <see cref="Combat.Duel"/> always does):
    /// an idle fighter asked for a quick step starts it on that tick, pays its stamina, and is in
    /// <see cref="FighterState.QuickStep"/> for its slide and recovery frames, then idle again. The
    /// <see cref="Combat.Duel"/> does the sliding. Like a move: it can't start while exhausted (unless it costs
    /// nothing), stamina doesn't regenerate during it, an ask made while busy waits in the input buffer (its own,
    /// beside the slot one), and a slot press on the same tick goes first. Unlike a 轻功 dodge it has no
    /// invincible frames: a hit during a quick step lands as normal and stops it.
    ///
    /// 轻功 dash (a technique with <see cref="Data.Technique.DashCm"/>, on a slot like any other): it starts like any
    /// move and pays its stamina. The way it goes is fixed on the tick it starts from the stick held then
    /// (<see cref="DashForward"/>, <see cref="DashRight"/>), or straight at the other fighter when no direction is
    /// held. Its first <see cref="Data.Technique.InvincibleFrames"/> frames are invincible (<see cref="IsInvincible"/>):
    /// strikes pass through. The <see cref="Combat.Duel"/> does the sliding.
    ///
    /// Combo escape: a technique that <see cref="Data.Technique.CanEscape"/> (the default 轻功 dash) can be started
    /// while in hitstun, which ends the hitstun on that tick (<see cref="EscapedHitstun"/>). Nothing else can. Presses of
    /// other slots during hitstun wait in the buffer as before.
    ///
    /// 点穴 seal: while <see cref="SealedFrames"/> is above 0 (set by <see cref="Seal"/> when a 点穴 hit lands), no 外功
    /// (waigong) technique can be started or chained. Other categories still work. A 外功 move already under way carries on.
    ///
    /// 炼气 heal: a technique with <see cref="Data.Technique.Heal"/> restores that much HP (never above max) on the tick
    /// its active frames end (its first recovery frame), and <see cref="HealedThisTick"/> says how much. Knocked out
    /// of the move before then (a hit during the wind-up or the channel), it heals nothing; its Qi is spent anyway.
    ///
    /// 擒拿 grab: when a grab catches, the <see cref="Combat.Duel"/> calls <see cref="Hold"/> on the grabber and stuns the other
    /// fighter in <see cref="FighterState.Grabbed"/>. While holding, the grabber's move is paused on the frame it caught;
    /// on the tick the hold runs out <see cref="ThrewThisTick"/> is true (the Duel lands the throw), and the move carries on
    /// from the next frame.
    ///
    /// Knocked down (<see cref="KnockDown"/>, needs <see cref="DownRules"/>; a <see cref="Combat.Duel"/> always gives them):
    /// the fighter lies in <see cref="FighterState.KnockedDown"/> and can't act. Once <see cref="Fighters.DownRules.MinFrames"/>
    /// have passed (<see cref="CanGetUp"/>), a direction held rolls away (<see cref="FighterState.Rolling"/>, aimed like a dash,
    /// relative to the lock-on) and any slot button rises (<see cref="FighterState.Rising"/>); after
    /// <see cref="Fighters.DownRules.MaxFrames"/> they rise by themselves. While exhausted they can do neither, and stay down
    /// until stamina is back up to exhausted_until. Rising and rolling are invincible and cost nothing; the button that rises
    /// is used up (it doesn't also start a move). Presses on the floor aren't buffered.
    ///
    /// 硬气功 guard (<see cref="Data.Technique.IsGuard"/>): guards from the tick it is pressed; while its slot is held
    /// (<see cref="SlotInput.IsHeld"/>) it stays on its last active frame, then plays its recovery once let go. It can't
    /// start while exhausted, and running out of stamina mid-guard drops it.
    ///
    /// 无相劫指 double strike (<see cref="Data.Technique.AutoChain"/>): the next hit starts by itself on the first chain frame.
    /// Its movement lock (<see cref="Cripple"/>): no quick step and no 轻功 while <see cref="CrippledFrames"/> is above 0.
    ///
    /// 龙爪手 (<see cref="Data.Technique.IsMultiGrab"/>): while holding, each press of the grab's slot (at least
    /// <see cref="GrabStrikeGap"/> frames apart) is a strike (<see cref="GrabStrikeThisTick"/>) and restarts the hold; the
    /// last one ends it. Held by any grab, pressing a slot with a grab of your own asks to break out (<see cref="GrabBreakWith"/>);
    /// the <see cref="Combat.Duel"/> decides if it is still in time.
    ///
    /// 易筋经 buff (<see cref="Data.Technique.IsBuff"/>): when the channel finishes, the fighter's hits deal
    /// <see cref="BuffDamagePct"/> damage for <see cref="BuffFramesLeft"/> frames (<see cref="Boost"/>).
    /// </summary>
    public sealed class Fighter
    {
        /// <param name="loadout">What sits on the 8 slots; empty if null.</param>
        /// <param name="maxHp">Starting and max HP. A <see cref="Combat.Duel"/> passes the rules sheet's hp_max.</param>
        /// <param name="positionCm">Where the fighter stands along X, in whole centimetres (Z starts at 0; set <see cref="Position"/> for anywhere else).</param>
        /// <param name="inputBufferFrames">How long an unused press is kept. A <see cref="Combat.Duel"/> passes the rules sheet's input_buffer.</param>
        /// <param name="stamina">The stamina numbers. Null turns stamina off: moves are free and the fighter is never exhausted.</param>
        /// <param name="maxQi">Max Qi. 0 turns Qi off: nothing is gained and techniques that cost Qi can't start.</param>
        /// <param name="qi">Qi to start with (a <see cref="Combat.Duel"/> passes qi_start, or what a run carried over).</param>
        /// <param name="quickStep">The quick step's numbers. Null means this fighter can't quick-step.</param>
        public Fighter(Loadout? loadout = null, int maxHp = 1, int positionCm = 0, int inputBufferFrames = 0, StaminaRules? stamina = null,
            int maxQi = 0, int qi = 0, QuickStepRules? quickStep = null, DownRules? down = null)
        {
            DownRules = down;
            QuickStepRules = quickStep;
            if (maxQi < 0) throw new ArgumentOutOfRangeException(nameof(maxQi), maxQi, "max Qi cannot be negative");
            if (qi < 0 || qi > maxQi) throw new ArgumentOutOfRangeException(nameof(qi), qi, $"starting Qi must be between 0 and max Qi ({maxQi})");
            MaxQi = maxQi;
            Qi = qi;
            StaminaRules = stamina;
            Stamina = stamina?.Max ?? 0;
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "a fighter has at least 1 HP");
            if (inputBufferFrames < 0) throw new ArgumentOutOfRangeException(nameof(inputBufferFrames), inputBufferFrames, "the input buffer cannot be negative");
            InputBufferFrames = inputBufferFrames;
            Loadout = loadout ?? new Loadout();
            MaxHp = maxHp;
            Hp = maxHp;
            Position = FloorPoint.FromCm(positionCm, 0);
        }

        public Loadout Loadout { get; }

        /// <summary>Frames left in which this fighter can't start 外功 techniques (a 点穴 seal). 0 when free.</summary>
        public int SealedFrames { get; private set; }

        public bool IsSealed => SealedFrames > 0;

        /// <summary>Frames left of a 擒拿 hold: the grabber's move is paused while this is above 0.</summary>
        public int HoldFramesLeft { get; private set; }

        /// <summary>True while this fighter is holding someone it grabbed.</summary>
        public bool IsHolding => HoldFramesLeft > 0;

        /// <summary>True on the tick the hold ran out: the throw lands on this tick.</summary>
        public bool ThrewThisTick { get; private set; }

        /// <summary>
        /// The current grab caught: pause the move here for <paramref name="frames"/> ticks (counted from the next tick),
        /// then throw. Only a fighter in a move can hold.
        /// </summary>
        public void Hold(int frames)
        {
            if (!State.IsInMove()) throw new InvalidOperationException("only a fighter in a move can hold a grab");
            if (frames < 1) throw new ArgumentOutOfRangeException(nameof(frames), frames, "a hold lasts at least 1 frame");
            HoldFramesLeft = frames;
        }

        /// <summary>
        /// True while this fighter has the other one locked on (the usual case). A fighter that <see cref="CanLoseLock"/> loses
        /// it when the other lands a 纵云梯 behind them: they keep facing, walking and attacking the way they were until they
        /// stand still for <see cref="RelockFrames"/>, or are hit, grabbed or knocked down; then they turn back round
        /// (<see cref="IsTurning"/>) over relock_turn_frames per half turn.
        /// </summary>
        public bool IsLockedOn { get; private set; } = true;

        /// <summary>True while turning back round after finding the other fighter again.</summary>
        public bool IsTurning { get; private set; }

        /// <summary>True if a 纵云梯 over this fighter's head can make them lose the lock-on (the opponent brains; set by the game).</summary>
        public bool CanLoseLock { get; set; }

        /// <summary>Frames of standing still it takes to find the other fighter again after losing them (0 = at once when they stop).</summary>
        public int RelockFrames { get; set; }

        /// <summary>Frames in a row stood still since losing the lock-on.</summary>
        public int StillFrames { get; private set; }

        /// <summary>Lost the other fighter: keep facing this way until found again.</summary>
        public void LoseLock()
        {
            IsLockedOn = false;
            IsTurning = false;
            StillFrames = 0;
        }

        /// <summary>Found the other fighter again: turn back round to them.</summary>
        public void Relock()
        {
            if (IsLockedOn) return;
            IsLockedOn = true;
            IsTurning = true;
            StillFrames = 0;
        }

        /// <summary>Facing the other fighter again: normal tracking resumes.</summary>
        public void FinishTurning() => IsTurning = false;

        /// <summary>The Duel calls this each tick while the lock-on is lost: standing still long enough finds the other fighter.</summary>
        public void CountStill(bool still)
        {
            if (IsLockedOn) return;
            StillFrames = still ? StillFrames + 1 : 0;
            if (still && StillFrames > RelockFrames) Relock();
        }

        /// <summary>Frames left of 无相劫指's movement lock: no quick step, no 轻功, slower walking. 0 when free.</summary>
        public int CrippledFrames { get; private set; }

        public bool IsCrippled => CrippledFrames > 0;

        /// <summary>Lock this fighter's movement for <paramref name="frames"/> frames from this tick (refreshes, doesn't stack).</summary>
        public void Cripple(int frames)
        {
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames), frames, "a lock cannot be negative");
            if (frames > CrippledFrames) CrippledFrames = frames;
        }

        /// <summary>Frames left of a 易筋经 damage buff. 0 when none.</summary>
        public int BuffFramesLeft { get; private set; }

        /// <summary>Damage of this fighter's hits while <see cref="BuffFramesLeft"/> is above 0, as a percentage.</summary>
        public int BuffDamagePct { get; private set; } = 100;

        public bool IsBuffed => BuffFramesLeft > 0;

        /// <summary>True on the tick a 易筋经 buff took hold.</summary>
        public bool BuffedThisTick { get; private set; }

        /// <summary><paramref name="damage"/> with this fighter's 易筋经 buff on it, if one is running (rounded half up).</summary>
        public int Boost(int damage) => IsBuffed ? (damage * BuffDamagePct + 50) / 100 : damage;

        /// <summary>How many hits of the current string have landed (for 无相劫指: both must land to lock movement).</summary>
        public int StringHitsLanded { get; private set; }

        /// <summary>The Duel calls this when a hit of the current move landed its damage.</summary>
        public void CountHitLanded()
        {
            if (State.IsInMove()) StringHitsLanded++;
        }

        /// <summary>True if every hit of the current string so far has landed.</summary>
        public bool WholeStringLanded => State.IsInMove() && StringHitsLanded >= StringHit;

        /// <summary>龙爪手: strikes dealt during the current hold.</summary>
        public int GrabHitsDone { get; private set; }

        /// <summary>True on the tick a multi-strike grab struck (the Duel deals the damage).</summary>
        public bool GrabStrikeThisTick { get; private set; }

        /// <summary>Fewest frames between two strikes of a multi-strike grab (a Duel sets grab_strike_gap).</summary>
        public int GrabStrikeGap { get; set; }

        private int _grabStrikeCooldown;

        /// <summary>
        /// True while the fighter this one holds may still break free with a grab of their own: any time in an ordinary
        /// grab's hold, and in a multi-strike grab's windows before its last strike (龙爪手: before strikes 1 and 2).
        /// </summary>
        public bool HoldCanBeBroken =>
            IsHolding && (!Technique!.IsMultiGrab || GrabHitsDone - (GrabStrikeThisTick ? 1 : 0) < Technique.GrabHits - 1);

        /// <summary>While grabbed: the grab technique pressed this tick to break out with, or null.</summary>
        public Technique? GrabBreakWith { get; private set; }

        /// <summary>The slot of the current move is held down this tick (for a 硬气功 guard).</summary>
        private bool _held;

        /// <summary>A grabbed fighter is let go: the hold ran out on a multi-strike grab, so they're free on this tick.</summary>
        public void Release()
        {
            if (State != FighterState.Grabbed) return;
            StunFramesLeft = 0;
            State = FighterState.Idle;
        }

        /// <summary>Knocked-down and getting-up numbers, or null when this fighter can't be knocked down.</summary>
        public DownRules? DownRules { get; }

        /// <summary>Frames spent on the floor since the knockdown. -1 when not knocked down.</summary>
        public int DownFrame { get; private set; } = -1;

        /// <summary>Frame within the current rise or roll, 0-based. -1 when not getting up.</summary>
        public int GetUpFrame { get; private set; } = -1;

        /// <summary>Which way the current roll goes, relative to the lock-on (out of 100). 0, 0 when not rolling.</summary>
        public int RollForward { get; private set; }

        /// <summary>The sideways part of the current roll's way (negative is left).</summary>
        public int RollRight { get; private set; }

        public bool IsKnockedDown => State == FighterState.KnockedDown;

        /// <summary>True when a knocked-down fighter may rise or roll now: down_min frames passed and not exhausted.</summary>
        public bool CanGetUp => State == FighterState.KnockedDown && DownFrame >= DownRules!.MinFrames && !IsExhausted;

        /// <summary>Total frames of the current rise or roll.</summary>
        public int GetUpTotalFrames =>
            State == FighterState.Rising ? DownRules!.RiseFrames : State == FighterState.Rolling ? DownRules!.RollFrames : 0;

        /// <summary>Knock this fighter down on this tick: whatever they were doing stops and they lie on the floor.</summary>
        public void KnockDown()
        {
            if (DownRules == null) throw new InvalidOperationException("this fighter has no down rules, so it can't be knocked down");
            EndMove();
            EndQuickStep();
            StunFramesLeft = 0;
            State = FighterState.KnockedDown;
            DownFrame = 0;
            GetUpFrame = -1;
            ClearBuffer();
        }

        /// <summary>HP a 炼气 heal restored on the latest tick (0 on every other tick).</summary>
        public int HealedThisTick { get; private set; }

        /// <summary>
        /// Seal this fighter's 外功 for <paramref name="frames"/> frames from this tick. A seal already running for longer
        /// is kept (seals don't stack, they refresh).
        /// </summary>
        public void Seal(int frames)
        {
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames), frames, "a seal cannot be negative");
            if (frames > SealedFrames) SealedFrames = frames;
        }

        /// <summary>True if a 点穴 debuff stops <paramref name="technique"/> right now: a seal stops 外功, a movement lock stops 轻功.</summary>
        public bool IsSealedFrom(Technique technique) =>
            IsSealed && technique.Category == Category.Waigong || IsCrippled && technique.Category == Category.Qinggong;

        /// <summary>
        /// True if <paramref name="technique"/> could start now as far as stamina, Qi and a 点穴 seal go
        /// (not whether the fighter is free).
        /// </summary>
        public bool CanUse(Technique technique) =>
            CanAfford(technique) && !IsSealedFrom(technique);

        /// <summary>The quick step's numbers, or null when this fighter can't quick-step.</summary>
        public QuickStepRules? QuickStepRules { get; }

        /// <summary>Which way the current quick step goes; <see cref="StepDirection.None"/> when not stepping.</summary>
        public StepDirection QuickStepDirection { get; private set; }

        /// <summary>Frame within the current quick step, 0-based. -1 when not stepping.</summary>
        public int QuickStepFrame { get; private set; } = -1;

        /// <summary>A quick step asked for while busy and not used yet, or <see cref="StepDirection.None"/>.</summary>
        public StepDirection BufferedStep { get; private set; }

        private int _bufferedStepTicksLeft;

        /// <summary>The stick held on the current tick (set by <see cref="Tick"/>), for aiming a dash.</summary>
        private MoveInput _stick;

        /// <summary>Which way the current dash goes, relative to the lock-on (out of 100, like <see cref="MoveInput"/>). 0, 0 when not dashing.</summary>
        public int DashForward { get; private set; }

        /// <summary>The sideways part of the current dash's way (negative is left).</summary>
        public int DashRight { get; private set; }

        /// <summary>True while strikes pass through this fighter: the invincible frames at the start of a move such as a 轻功 dash.</summary>
        public bool IsInvincible =>
            State.IsInMove() && Technique!.IsInvincible(MoveFrame) || State == FighterState.Rising || State == FighterState.Rolling;

        /// <summary>True while in the air in a 纵云梯 leap (its startup and active frames): passes over the other fighter.</summary>
        public bool IsAirborne => State.IsInMove() && Technique!.IsJump && MoveFrame < Technique.DashFrames;

        /// <summary>True while in a 轻功 dash (its slide or its recovery).</summary>
        public bool IsDashing => State.IsInMove() && Technique!.IsDash;

        /// <summary>True while the current move was started out of hitstun (a combo escape). Such a dash never earns a just dodge.</summary>
        public bool EscapedHitstun { get; private set; }

        /// <summary>The stamina numbers, or null when this fighter doesn't use stamina.</summary>
        public StaminaRules? StaminaRules { get; }

        public bool UsesStamina => StaminaRules != null;

        /// <summary>Max stamina; 0 when stamina is off.</summary>
        public int MaxStamina => StaminaRules?.Max ?? 0;

        /// <summary>Current stamina, from <see cref="MaxStamina"/> down to 0.</summary>
        public int Stamina { get; private set; }

        /// <summary>
        /// True from the tick stamina reaches 0 until it is back up to exhausted_until. An exhausted
        /// fighter can't start moves that cost stamina, walks slower and takes extra damage.
        /// </summary>
        public bool IsExhausted { get; private set; }

        /// <summary>Ticks in a row with no move and no stamina lost (counts up to the regen delay).</summary>
        private int _restTicks;

        /// <summary>Regen owed but not yet paid, in sixtieths of a point.</summary>
        private int _regenSixtieths;

        /// <summary>Max Qi; 0 when Qi is off.</summary>
        public int MaxQi { get; }

        /// <summary>Current Qi, from 0 up to <see cref="MaxQi"/>.</summary>
        public int Qi { get; private set; }

        public int MaxHp { get; }

        /// <summary>Current HP, from <see cref="MaxHp"/> down to 0.</summary>
        public int Hp { get; private set; }

        /// <summary>True once HP reaches 0.</summary>
        public bool IsDown => Hp == 0;

        /// <summary>
        /// Where the fighter stands on the floor, in floor units (1/60 cm). Set by whoever moves the
        /// fighter: the <see cref="Combat.Duel"/>.
        /// </summary>
        public FloorPoint Position { get; set; }

        /// <summary>Where the fighter stands along X, in whole centimetres (rounded). Setting it keeps Z.</summary>
        public int XCm
        {
            get => Position.XCm;
            set => Position = new FloorPoint(value * FloorPoint.UnitsPerCm, Position.Z);
        }

        /// <summary>Where the fighter stands along Z, in whole centimetres (rounded). Setting it keeps X.</summary>
        public int ZCm
        {
            get => Position.ZCm;
            set => Position = new FloorPoint(Position.X, value * FloorPoint.UnitsPerCm);
        }

        /// <summary>
        /// Which way the fighter faces: a direction on the floor (any length, never zero). The
        /// <see cref="Combat.Duel"/> keeps it on the other fighter (auto lock-on), except during a move,
        /// which keeps the aim it took on the tick it started: a strike goes where it was aimed.
        /// </summary>
        public FloorPoint Facing
        {
            get => _facing;
            set => _facing = value.IsZero ? _facing : value;
        }

        private FloorPoint _facing = new FloorPoint(1, 0);

        /// <summary>True once the current move's hit has connected. A move hits at most once.</summary>
        public bool HitLanded { get; private set; }

        public FighterState State { get; private set; } = FighterState.Idle;

        /// <summary>The technique being performed, or null when idle or stunned.</summary>
        public Technique? Technique { get; private set; }

        /// <summary>The slot the current technique was started from.</summary>
        public Slot? Slot { get; private set; }

        /// <summary>Frame within the current technique, 0-based. -1 when not in a move.</summary>
        public int MoveFrame { get; private set; } = -1;

        /// <summary>
        /// The move ends (back to idle) when <see cref="MoveFrame"/> reaches this: the technique's total frames,
        /// or fewer once a hit with hit_recovery lands (<see cref="CutRecoveryOnHit"/>). -1 when not in a move.
        /// </summary>
        public int MoveEndFrame { get; private set; } = -1;

        /// <summary>Frames of stun left. 0 when not stunned.</summary>
        public int StunFramesLeft { get; private set; }

        public bool CanAct => State == FighterState.Idle;

        /// <summary>How many ticks an unused press is kept for.</summary>
        public int InputBufferFrames { get; }

        /// <summary>The newest press not used yet, or <see cref="SlotInput.None"/>.</summary>
        public SlotInput Buffered { get; private set; } = SlotInput.None;

        /// <summary>Ticks the buffered press has left. 0 when nothing is buffered.</summary>
        public int BufferedTicksLeft { get; private set; }

        /// <summary>True while the current move is its Qi-empowered version (spent its empower_qi when it started).</summary>
        public bool Empowered { get; private set; }

        /// <summary>
        /// True while hits can't interrupt this fighter: an empowered move with hyper armor, in its startup
        /// or active frames. Hits still deal damage. Parries and clashes still work on it.
        /// </summary>
        public bool HasArmor =>
            Empowered && Technique!.EmpowerArmor && (State == FighterState.Startup || State == FighterState.Active);

        /// <summary>Which hit of the current string this is: 1 for a fresh start, 2 after one chain, and so on. 0 when not in a move.</summary>
        public int StringHit { get; private set; }

        /// <summary>
        /// True on recovery frames where pressing <see cref="Slot"/> starts the next hit of the string.
        /// </summary>
        public bool InChainWindow =>
            State == FighterState.Recovery
            && Technique!.HasNext
            && MoveFrame - Technique.StartupFrames - Technique.ActiveFrames < Technique.ChainWindowFrames;

        /// <summary>
        /// The state the fighter will be in on the next tick before any input is used: what
        /// <see cref="Tick"/> would leave them in if nothing were pressed and nothing buffered.
        /// Lets an AI decide this tick's press from what the fighter will be doing.
        /// </summary>
        public FighterState NextState()
        {
            if (IsHolding) return State;
            if (State == FighterState.KnockedDown) return State;
            if (State == FighterState.Rising || State == FighterState.Rolling)
                return GetUpFrame + 1 >= GetUpTotalFrames ? FighterState.Idle : State;
            if (State == FighterState.QuickStep)
                return QuickStepFrame + 1 >= QuickStepRules!.TotalFrames ? FighterState.Idle : FighterState.QuickStep;
            if (State.IsInMove())
                return MoveFrame + 1 >= MoveEndFrame ? FighterState.Idle : PhaseAt(Technique!, MoveFrame + 1);
            if (State.IsStunned())
                return StunFramesLeft <= 1 ? FighterState.Idle : State;
            return State;
        }

        /// <summary>
        /// Advance one frame, then act on this tick's slot presses (or a buffered one) if free to, then
        /// start a quick step if asked (or one is buffered) and still free, then regain stamina.
        /// <paramref name="walking"/> says the fighter is being told to walk this tick, which only matters
        /// for the regen rate. <paramref name="stick"/> is the stick held this tick, which aims a 轻功 dash started on it.
        /// </summary>
        public void Tick(SlotInput input, bool walking = false, StepDirection quickStep = StepDirection.None, MoveInput stick = default)
        {
            _stick = stick;
            HealedThisTick = 0;
            BuffedThisTick = false;
            ThrewThisTick = false;
            GrabStrikeThisTick = false;
            GrabBreakWith = null;
            if (SealedFrames > 0) SealedFrames--;
            if (CrippledFrames > 0) CrippledFrames--;
            if (BuffFramesLeft > 0 && --BuffFramesLeft == 0) BuffDamagePct = 100;
            _held = Slot != null && input.IsHeld(Slot.Value);
            Advance();
            TryAutoChain();

            if (State == FighterState.KnockedDown)
            {
                TryGetUp(input);
                Regen(walking);
                return;
            }

            if (TryAct(input) || TryAct(Buffered))
                ClearBuffer();
            else
                KeepBuffered(input);

            if (TryQuickStep(quickStep) || TryQuickStep(BufferedStep))
            {
                BufferedStep = StepDirection.None;
                _bufferedStepTicksLeft = 0;
            }
            else if (quickStep != StepDirection.None && InputBufferFrames > 0)
            {
                BufferedStep = quickStep;
                _bufferedStepTicksLeft = InputBufferFrames;
            }
            else if (_bufferedStepTicksLeft > 0 && --_bufferedStepTicksLeft == 0)
            {
                BufferedStep = StepDirection.None;
            }

            Regen(walking);
        }

        /// <summary>无相劫指: on the first chain frame of an auto-chained hit, the next hit starts by itself.</summary>
        private void TryAutoChain()
        {
            if (State != FighterState.Recovery || !Technique!.AutoChain || !Technique.HasNext) return;
            if (MoveFrame != Technique.StartupFrames + Technique.ActiveFrames || Loadout.Book == null) return;
            var next = Loadout.Book.NextOf(Technique);
            if (next == null || !CanUse(next)) return;
            int hit = StringHit + 1;
            int landed = StringHitsLanded;
            Start(Slot!.Value, next);
            StringHit = hit;
            StringHitsLanded = landed;
        }

        /// <summary>On the floor: roll if a direction is held, rise on any button, or rise by itself after down_max.</summary>
        private void TryGetUp(SlotInput input)
        {
            if (!CanGetUp) return;
            if (_stick.IsWalking)
            {
                State = FighterState.Rolling;
                RollForward = _stick.Forward;
                RollRight = _stick.Right;
            }
            else if (input.Any || DownFrame >= DownRules!.MaxFrames)
            {
                State = FighterState.Rising;
            }
            else return;
            DownFrame = -1;
            GetUpFrame = 0;
            ClearBuffer();
        }

        /// <summary>True if a quick step could start now: free, able to quick-step, and not out of breath for it.</summary>
        public bool CanQuickStep =>
            CanAct && QuickStepRules != null && (!IsExhausted || QuickStepRules.Stamina == 0) && !IsCrippled;

        private bool TryQuickStep(StepDirection direction)
        {
            if (direction == StepDirection.None || !CanQuickStep) return false;
            LoseStamina(QuickStepRules!.Stamina);
            State = FighterState.QuickStep;
            QuickStepDirection = direction;
            QuickStepFrame = 0;
            return true;
        }

        private void KeepBuffered(SlotInput input)
        {
            if (input.Any && InputBufferFrames > 0)
            {
                Buffered = input;
                BufferedTicksLeft = InputBufferFrames;
            }
            else if (BufferedTicksLeft > 0 && --BufferedTicksLeft == 0)
            {
                Buffered = SlotInput.None;
            }
        }

        /// <summary>Use <paramref name="input"/> if it can start something this tick.</summary>
        private bool TryAct(SlotInput input)
        {
            if (!input.Any) return false;

            // 龙爪手: each tap of the grab's slot while holding strikes and restarts the hold.
            if (IsHolding && Technique!.IsMultiGrab && Slot != null)
            {
                if (!input.IsPressed(Slot.Value) || _grabStrikeCooldown > 0 || GrabHitsDone >= Technique.GrabHits) return false;
                GrabHitsDone++;
                GrabStrikeThisTick = true;
                _grabStrikeCooldown = GrabStrikeGap;
                HoldFramesLeft = GrabHitsDone >= Technique.GrabHits ? 0 : Technique.HoldFrames;
                return true;
            }

            // Held by a grab: a grab of your own breaks out, if the Duel says it is still in time.
            if (State == FighterState.Grabbed)
            {
                foreach (var slot in Slots.All)
                {
                    if (!input.IsPressed(slot)) continue;
                    var grab = Loadout[slot];
                    if (grab == null || !grab.IsGrab || !CanUse(grab)) continue;
                    GrabBreakWith = grab;
                    return true;
                }
                return false;
            }

            if (InChainWindow && input.IsPressed(Slot!.Value))
            {
                if (Technique!.AutoChain) return false;
                if (!CanUse(Technique!.Next)) return false;
                var book = Loadout.Book
                    ?? throw new InvalidOperationException($"{Technique!.Id} chains into '{Technique.Next}', but the loadout has no technique book to find it in");
                int hit = StringHit + 1;
                int landed = StringHitsLanded;
                Start(Slot.Value, book.NextOf(Technique!)!);
                StringHit = hit;
                StringHitsLanded = landed;
                return true;
            }

            if (State == FighterState.Hitstun)
            {
                // Only an escape move (the default 轻功 dash) can break out of hitstun.
                foreach (var slot in Slots.All)
                {
                    if (!input.IsPressed(slot)) continue;
                    var escape = Loadout[slot];
                    if (escape == null || !escape.CanEscape || !CanUse(escape)) continue;
                    StunFramesLeft = 0;
                    Start(slot, escape);
                    EscapedHitstun = true;
                    return true;
                }
                return false;
            }

            if (!CanAct) return false;
            foreach (var slot in Slots.All)
            {
                if (!input.IsPressed(slot)) continue;
                var technique = Loadout[slot];
                if (technique == null || !CanUse(technique)) continue;
                Start(slot, technique);
                return true;
            }
            return false;
        }

        /// <summary>True if <paramref name="technique"/> can be started as far as stamina and Qi go (a guard never while exhausted).</summary>
        public bool CanAfford(Technique technique) =>
            (!IsExhausted || technique.StaminaCost == 0 && !technique.IsGuard) && Qi >= technique.QiCost;

        /// <summary>
        /// Gain <paramref name="amount"/> Qi, never above <see cref="MaxQi"/>. Returns how much was
        /// actually gained (less than asked when the bar fills; 0 when Qi is off).
        /// </summary>
        public int GainQi(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Qi gains cannot be negative");
            int gained = Math.Min(amount, MaxQi - Qi);
            Qi += gained;
            return gained;
        }

        private bool CanUse(string nextId)
        {
            var next = Loadout.Book?.Find(nextId);
            return next == null || CanUse(next);
        }

        /// <summary>
        /// Lose <paramref name="amount"/> stamina (never below 0). Reaching 0 exhausts the fighter.
        /// Losing any stamina restarts the regen delay. Does nothing when stamina is off.
        /// </summary>
        public void LoseStamina(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "use GainStamina to add stamina");
            if (!UsesStamina || amount == 0) return;
            Stamina = amount >= Stamina ? 0 : Stamina - amount;
            if (Stamina == 0) IsExhausted = true;
            _restTicks = 0;
            _regenSixtieths = 0;
        }

        /// <summary>
        /// Gain <paramref name="amount"/> stamina (never above max). An exhausted fighter recovers
        /// once stamina is back up to exhausted_until. Does nothing when stamina is off.
        /// </summary>
        public void GainStamina(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "use LoseStamina to take stamina");
            if (!UsesStamina || amount == 0) return;
            Stamina = Math.Min(MaxStamina, Stamina + amount);
            if (IsExhausted && Stamina >= StaminaRules!.ExhaustedUntil) IsExhausted = false;
        }

        /// <summary>The damage this fighter takes from a hit of <paramref name="damage"/>: more while exhausted (rounded half up).</summary>
        public int DamageTaken(int damage) =>
            IsExhausted ? (damage * StaminaRules!.ExhaustedDamagePct + 50) / 100 : damage;

        private void Regen(bool walking)
        {
            if (!UsesStamina) return;
            if (State.IsInMove() || State == FighterState.QuickStep)
            {
                _restTicks = 0;
                _regenSixtieths = 0;
                return;
            }
            if (_restTicks < StaminaRules!.RegenDelayFrames)
            {
                _restTicks++;
                return;
            }
            bool still = State == FighterState.Idle && !walking;
            _regenSixtieths += still ? StaminaRules.RegenStillPerSecond : StaminaRules.RegenPerSecond;
            int points = _regenSixtieths / Sim.TicksPerSecond;
            _regenSixtieths %= Sim.TicksPerSecond;
            GainStamina(points);
        }

        private void ClearBuffer()
        {
            Buffered = SlotInput.None;
            BufferedTicksLeft = 0;
        }

        /// <summary>
        /// Stop whatever the fighter is doing and hold them in a stun state for
        /// <paramref name="frames"/> frames, counted from this tick.
        /// </summary>
        public void Stun(FighterState state, int frames)
        {
            if (!state.IsStunned()) throw new ArgumentException($"{state} is not a stun state", nameof(state));
            if (frames < 1) throw new ArgumentOutOfRangeException(nameof(frames), frames, "a stun lasts at least 1 frame");
            EndMove();
            EndQuickStep();
            DownFrame = -1;
            GetUpFrame = -1;
            State = state;
            StunFramesLeft = frames;
        }

        /// <summary>
        /// Take a hit on this tick: lose <paramref name="damage"/> HP (never below 0), then,
        /// if <paramref name="hitstunFrames"/> is above 0, be held in hitstun for that many
        /// frames counted from this tick. A new hit during hitstun restarts the count.
        /// </summary>
        public void TakeHit(int damage, int hitstunFrames)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage cannot be negative");
            if (hitstunFrames < 0) throw new ArgumentOutOfRangeException(nameof(hitstunFrames), hitstunFrames, "hitstun cannot be negative");
            Hp = damage >= Hp ? 0 : Hp - damage;
            if (hitstunFrames > 0) Stun(FighterState.Hitstun, hitstunFrames);
        }

        /// <summary>Record that the current move's hit connected, so its later active frames don't hit again.</summary>
        public void MarkHitLanded()
        {
            if (!State.IsInMove()) throw new InvalidOperationException("only a fighter in a move can land a hit");
            HitLanded = true;
        }

        /// <summary>
        /// The current move's hit just landed (damage, not blocked, parried or dodged): if <paramref name="move"/> is
        /// still the move under way and has hit_recovery, it ends that many frames from now instead of running its
        /// full recovery. Never makes a move longer.
        /// </summary>
        public void CutRecoveryOnHit(Technique move)
        {
            if (!State.IsInMove() || Technique != move || move.HitRecoveryFrames <= 0) return;
            MoveEndFrame = Math.Min(MoveEndFrame, MoveFrame + move.HitRecoveryFrames);
        }

        private void Start(Slot slot, Technique technique)
        {
            EscapedHitstun = false;
            LoseStamina(technique.StaminaCost);
            Qi -= technique.QiCost;
            Empowered = technique.CanEmpower && Qi >= technique.EmpowerQi;
            if (Empowered) Qi -= technique.EmpowerQi;
            Technique = technique;
            Slot = slot;
            MoveFrame = 0;
            MoveEndFrame = technique.TotalFrames;
            HitLanded = false;
            StringHit = 1;
            StringHitsLanded = 0;
            GrabHitsDone = 0;
            _grabStrikeCooldown = 0;
            State = PhaseAt(technique, 0);
            if (technique.IsDash)
            {
                // No direction held: straight at the other fighter.
                bool held = _stick.IsWalking;
                DashForward = held ? _stick.Forward : MoveInput.Full;
                DashRight = held ? _stick.Right : 0;
            }
        }

        private void Advance()
        {
            if (State == FighterState.KnockedDown)
            {
                DownFrame++;
            }
            else if (State == FighterState.Rising || State == FighterState.Rolling)
            {
                GetUpFrame++;
                if (GetUpFrame >= GetUpTotalFrames)
                {
                    GetUpFrame = -1;
                    RollForward = 0;
                    RollRight = 0;
                    State = FighterState.Idle;
                }
            }
            else if (State == FighterState.QuickStep)
            {
                QuickStepFrame++;
                if (QuickStepFrame >= QuickStepRules!.TotalFrames)
                {
                    EndQuickStep();
                    State = FighterState.Idle;
                }
            }
            else if (IsHolding)
            {
                // A grab holding its catch: the move waits on this frame until the throw (or, for a multi-strike grab,
                // until the held fighter breaks free).
                if (_grabStrikeCooldown > 0) _grabStrikeCooldown--;
                HoldFramesLeft--;
                if (HoldFramesLeft == 0) ThrewThisTick = true;
            }
            else if (State.IsInMove() && Technique!.IsGuard && MoveFrame == Technique.StartupFrames + Technique.ActiveFrames - 1
                && _held && !IsExhausted)
            {
                // 硬气功: held, the guard stays up on its last active frame.
            }
            else if (State.IsInMove())
            {
                MoveFrame++;
                if (MoveFrame >= MoveEndFrame)
                {
                    EndMove();
                    State = FighterState.Idle;
                }
                else
                {
                    var t = Technique!;
                    State = PhaseAt(t, MoveFrame);
                    // A 炼气 heal lands as its channel (the active frames) ends.
                    if (t.IsHeal && MoveFrame == t.StartupFrames + t.ActiveFrames && !IsDown)
                    {
                        int healed = Math.Min(t.Heal, MaxHp - Hp);
                        Hp += healed;
                        HealedThisTick = healed;
                    }
                    // A 易筋经 buff takes hold as its channel ends.
                    if (t.IsBuff && MoveFrame == t.StartupFrames + t.ActiveFrames && !IsDown)
                    {
                        BuffFramesLeft = t.BuffFrames;
                        BuffDamagePct = t.BuffDamagePct;
                        BuffedThisTick = true;
                    }
                }
            }
            else if (State.IsStunned())
            {
                StunFramesLeft--;
                if (StunFramesLeft <= 0)
                {
                    StunFramesLeft = 0;
                    State = FighterState.Idle;
                }
            }
        }

        private void EndQuickStep()
        {
            QuickStepDirection = StepDirection.None;
            QuickStepFrame = -1;
        }

        private void EndMove()
        {
            Technique = null;
            Slot = null;
            MoveFrame = -1;
            MoveEndFrame = -1;
            HitLanded = false;
            StringHit = 0;
            Empowered = false;
            EscapedHitstun = false;
            DashForward = 0;
            DashRight = 0;
            HoldFramesLeft = 0;
            StringHitsLanded = 0;
            GrabHitsDone = 0;
            _grabStrikeCooldown = 0;
        }

        /// <summary>Which phase frame <paramref name="frame"/> of a technique is in.</summary>
        public static FighterState PhaseAt(Technique technique, int frame)
        {
            if (frame < technique.StartupFrames) return FighterState.Startup;
            if (frame < technique.StartupFrames + technique.ActiveFrames) return FighterState.Active;
            return FighterState.Recovery;
        }

        public override string ToString() =>
            State == FighterState.KnockedDown ? $"KnockedDown f{DownFrame}"
            : State == FighterState.Rising || State == FighterState.Rolling ? $"{State} f{GetUpFrame}"
            : IsHolding ? $"Holding {Technique!.Id} ({HoldFramesLeft} left)"
            : State == FighterState.QuickStep ? $"QuickStep {QuickStepDirection} f{QuickStepFrame}"
            : State.IsInMove() ? $"{State} {Technique!.Id} f{MoveFrame}"
            : State.IsStunned() ? $"{State} ({StunFramesLeft} left)"
            : State.ToString();
    }
}
