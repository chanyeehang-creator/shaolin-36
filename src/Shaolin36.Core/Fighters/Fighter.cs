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
    /// Presses made while busy are ignored for now; the input buffer comes with strings.
    /// </summary>
    public sealed class Fighter
    {
        public Fighter(Loadout? loadout = null) => Loadout = loadout ?? new Loadout();

        public Loadout Loadout { get; }

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

        /// <summary>Advance one frame, then act on this tick's slot presses if free to.</summary>
        public void Tick(SlotInput input)
        {
            Advance();
            if (!CanAct || !input.Any) return;

            foreach (var slot in Slots.All)
            {
                if (!input.IsPressed(slot)) continue;
                var technique = Loadout[slot];
                if (technique == null) continue;
                Start(slot, technique);
                return;
            }
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

        private void Start(Slot slot, Technique technique)
        {
            Technique = technique;
            Slot = slot;
            MoveFrame = 0;
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
