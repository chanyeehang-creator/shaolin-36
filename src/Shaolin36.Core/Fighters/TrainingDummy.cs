using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// The playtest opponent: it stands where it is and throws the string on one slot over
    /// and over. Before its first string, and after each one, it waits <see cref="PauseFrames"/> idle frames, then starts
    /// again. In a string it chains on chain-window frame <see cref="ChainFrame"/> (0 is the
    /// first frame, a true combo; a later frame leaves a gap you can parry into). Being hit
    /// resets the pause. A parried hit ends the string. Deterministic: the same fight always gets the same presses.
    ///
    /// Call <see cref="Think"/> once per tick, before the duel steps, and pass what it returns
    /// as the dummy's input.
    /// </summary>
    public sealed class TrainingDummy
    {
        private int _idleFrames;

        public TrainingDummy(Slot slot, int pauseFrames, int chainFrame)
        {
            if (!Slots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, "not one of the 8 slots");
            if (pauseFrames < 0) throw new ArgumentOutOfRangeException(nameof(pauseFrames), pauseFrames, "the pause cannot be negative");
            if (chainFrame < 0) throw new ArgumentOutOfRangeException(nameof(chainFrame), chainFrame, "the chain frame cannot be negative");
            Slot = slot;
            PauseFrames = pauseFrames;
            ChainFrame = chainFrame;
        }

        /// <summary>A dummy using the rules sheet's dummy_pause and dummy_chain_frame.</summary>
        public static TrainingDummy FromRules(Rules rules, Slot slot) =>
            new TrainingDummy(slot, rules.DummyPause, rules.DummyChainFrame);

        public Slot Slot { get; }
        public int PauseFrames { get; }
        public int ChainFrame { get; }

        /// <summary>False makes it stand still and press nothing, for practising on it.</summary>
        public bool Attacks { get; set; } = true;

        /// <summary>
        /// This tick's input for <paramref name="me"/>, the dummy's own fighter. It never presses
        /// when the press would only be buffered, so a chain frame past the window just ends the string.
        /// </summary>
        public SlotInput Think(Fighter me)
        {
            var next = me.NextState();
            if (!Attacks || next != FighterState.Idle && !next.IsInMove())
            {
                _idleFrames = 0;
                return SlotInput.None;
            }

            if (next == FighterState.Idle)
            {
                if (_idleFrames < PauseFrames)
                {
                    _idleFrames++;
                    return SlotInput.None;
                }
                _idleFrames = 0;
                return SlotInput.Press(Slot);
            }

            _idleFrames = 0;
            var t = me.Technique!;
            if (next == FighterState.Recovery && t.HasNext && !me.ChainBlocked && me.Slot == Slot && ChainFrame < t.ChainWindowFrames)
            {
                int recoveryFrame = me.MoveFrame + 1 - t.StartupFrames - t.ActiveFrames;
                if (recoveryFrame == ChainFrame) return SlotInput.Press(Slot);
            }
            return SlotInput.None;
        }
    }
}
