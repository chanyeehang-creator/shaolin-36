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
            int maxQi = 0, int qi = 0, QuickStepRules? quickStep = null)
        {
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
        public bool IsInvincible => State.IsInMove() && Technique!.IsInvincible(MoveFrame);

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
            if (State == FighterState.QuickStep)
                return QuickStepFrame + 1 >= QuickStepRules!.TotalFrames ? FighterState.Idle : FighterState.QuickStep;
            if (State.IsInMove())
                return MoveFrame + 1 >= Technique!.TotalFrames ? FighterState.Idle : PhaseAt(Technique, MoveFrame + 1);
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
            Advance();

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

        /// <summary>True if a quick step could start now: free, able to quick-step, and not out of breath for it.</summary>
        public bool CanQuickStep =>
            CanAct && QuickStepRules != null && (!IsExhausted || QuickStepRules.Stamina == 0);

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

            if (InChainWindow && input.IsPressed(Slot!.Value))
            {
                if (!CanAfford(Technique!.Next)) return false;
                var book = Loadout.Book
                    ?? throw new InvalidOperationException($"{Technique!.Id} chains into '{Technique.Next}', but the loadout has no technique book to find it in");
                int hit = StringHit + 1;
                Start(Slot.Value, book.NextOf(Technique!)!);
                StringHit = hit;
                return true;
            }

            if (State == FighterState.Hitstun)
            {
                // Only an escape move (the default 轻功 dash) can break out of hitstun.
                foreach (var slot in Slots.All)
                {
                    if (!input.IsPressed(slot)) continue;
                    var escape = Loadout[slot];
                    if (escape == null || !escape.CanEscape || !CanAfford(escape)) continue;
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
                if (technique == null || !CanAfford(technique)) continue;
                Start(slot, technique);
                return true;
            }
            return false;
        }

        /// <summary>True if <paramref name="technique"/> can be started as far as stamina and Qi go.</summary>
        public bool CanAfford(Technique technique) =>
            (!IsExhausted || technique.StaminaCost == 0) && Qi >= technique.QiCost;

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

        private bool CanAfford(string nextId)
        {
            var next = Loadout.Book?.Find(nextId);
            return next == null || CanAfford(next);
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
            HitLanded = false;
            StringHit = 1;
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
            if (State == FighterState.QuickStep)
            {
                QuickStepFrame++;
                if (QuickStepFrame >= QuickStepRules!.TotalFrames)
                {
                    EndQuickStep();
                    State = FighterState.Idle;
                }
            }
            else if (State.IsInMove())
            {
                MoveFrame++;
                if (MoveFrame >= Technique!.TotalFrames)
                {
                    EndMove();
                    State = FighterState.Idle;
                }
                else
                {
                    State = PhaseAt(Technique, MoveFrame);
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
            HitLanded = false;
            StringHit = 0;
            Empowered = false;
            EscapedHitstun = false;
            DashForward = 0;
            DashRight = 0;
        }

        /// <summary>Which phase frame <paramref name="frame"/> of a technique is in.</summary>
        public static FighterState PhaseAt(Technique technique, int frame)
        {
            if (frame < technique.StartupFrames) return FighterState.Startup;
            if (frame < technique.StartupFrames + technique.ActiveFrames) return FighterState.Active;
            return FighterState.Recovery;
        }

        public override string ToString() =>
            State == FighterState.QuickStep ? $"QuickStep {QuickStepDirection} f{QuickStepFrame}"
            : State.IsInMove() ? $"{State} {Technique!.Id} f{MoveFrame}"
            : State.IsStunned() ? $"{State} ({StunFramesLeft} left)"
            : State.ToString();
    }
}
