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
    /// </summary>
    public sealed class Fighter
    {
        /// <param name="loadout">What sits on the 8 slots; empty if null.</param>
        /// <param name="maxHp">Starting and max HP. A <see cref="Combat.Duel"/> passes the rules sheet's hp_max.</param>
        /// <param name="positionCm">Where the fighter stands on the line, in whole centimetres.</param>
        /// <param name="inputBufferFrames">How long an unused press is kept. A <see cref="Combat.Duel"/> passes the rules sheet's input_buffer.</param>
        public Fighter(Loadout? loadout = null, int maxHp = 1, int positionCm = 0, int inputBufferFrames = 0)
        {
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "a fighter has at least 1 HP");
            if (inputBufferFrames < 0) throw new ArgumentOutOfRangeException(nameof(inputBufferFrames), inputBufferFrames, "the input buffer cannot be negative");
            InputBufferFrames = inputBufferFrames;
            Loadout = loadout ?? new Loadout();
            MaxHp = maxHp;
            Hp = maxHp;
            PositionCm = positionCm;
        }

        public Loadout Loadout { get; }

        public int MaxHp { get; }

        /// <summary>Current HP, from <see cref="MaxHp"/> down to 0.</summary>
        public int Hp { get; private set; }

        /// <summary>True once HP reaches 0.</summary>
        public bool IsDown => Hp == 0;

        /// <summary>
        /// Where the fighter stands on the fight line, in whole centimetres. Set by whoever
        /// moves the fighter (the arena for now; movement rules come later).
        /// </summary>
        public int PositionCm { get; set; }

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
            if (State.IsInMove())
                return MoveFrame + 1 >= Technique!.TotalFrames ? FighterState.Idle : PhaseAt(Technique, MoveFrame + 1);
            if (State.IsStunned())
                return StunFramesLeft <= 1 ? FighterState.Idle : State;
            return State;
        }

        /// <summary>Advance one frame, then act on this tick's slot presses (or a buffered one) if free to.</summary>
        public void Tick(SlotInput input)
        {
            Advance();

            if (TryAct(input) || TryAct(Buffered))
            {
                ClearBuffer();
                return;
            }

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
                var book = Loadout.Book
                    ?? throw new InvalidOperationException($"{Technique!.Id} chains into '{Technique.Next}', but the loadout has no technique book to find it in");
                int hit = StringHit + 1;
                Start(Slot.Value, book.NextOf(Technique!)!);
                StringHit = hit;
                return true;
            }

            if (!CanAct) return false;
            foreach (var slot in Slots.All)
            {
                if (!input.IsPressed(slot)) continue;
                var technique = Loadout[slot];
                if (technique == null) continue;
                Start(slot, technique);
                return true;
            }
            return false;
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
            Technique = technique;
            Slot = slot;
            MoveFrame = 0;
            HitLanded = false;
            StringHit = 1;
            State = PhaseAt(technique, 0);
        }

        private void Advance()
        {
            if (State.IsInMove())
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

        private void EndMove()
        {
            Technique = null;
            Slot = null;
            MoveFrame = -1;
            HitLanded = false;
            StringHit = 0;
        }

        /// <summary>Which phase frame <paramref name="frame"/> of a technique is in.</summary>
        public static FighterState PhaseAt(Technique technique, int frame)
        {
            if (frame < technique.StartupFrames) return FighterState.Startup;
            if (frame < technique.StartupFrames + technique.ActiveFrames) return FighterState.Active;
            return FighterState.Recovery;
        }

        public override string ToString() =>
            State.IsInMove() ? $"{State} {Technique!.Id} f{MoveFrame}"
            : State.IsStunned() ? $"{State} ({StunFramesLeft} left)"
            : State.ToString();
    }
}
