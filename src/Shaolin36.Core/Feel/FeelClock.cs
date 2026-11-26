using System;
using System.Collections.Generic;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Feel
{
    /// <summary>
    /// What the screen and speakers should do for one strike: which kind it was, and that kind's row
    /// of the feel sheet. The engine side turns it into sparks, sound and camera moves; the core only
    /// decides which row applies and how long the fight freezes.
    /// </summary>
    public sealed class FeelCue
    {
        public FeelCue(HitEvent hit, FeelRow row)
        {
            Hit = hit;
            Row = row;
        }

        public HitEvent Hit { get; }
        public FeelRow Row { get; }
        public FeelEvent Event => Row.Event;

        /// <summary>Which kind of strike <paramref name="hit"/> was, for the feel sheet.</summary>
        public static FeelEvent Classify(HitEvent hit)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (hit.JustDodged) return FeelEvent.JustDodge;
            if (hit.Grab == GrabPhase.Caught) return FeelEvent.Grab;
            if (hit.Grab == GrabPhase.Broken || hit.Grab == GrabPhase.Released) return FeelEvent.GrabBreak;
            if (hit.Guarded) return hit.DefenderHpAfter == 0 ? FeelEvent.Ko : FeelEvent.Block;
            switch (hit.Parry)
            {
                case ParryOutcome.PerfectParry: return FeelEvent.PerfectParry;
                case ParryOutcome.Parried: return FeelEvent.Block;
            }
            switch (hit.Clash)
            {
                case ClashOutcome.BothRecoil: return FeelEvent.ClashRecoil;
                case ClashOutcome.TargetWins: return FeelEvent.ClashLost;
                case ClashOutcome.PerfectClash: return FeelEvent.PerfectClash;
                case ClashOutcome.EnergyCancel: return FeelEvent.EnergyCancel;
            }
            if (hit.Landed && hit.DefenderHpAfter == 0) return FeelEvent.Ko;
            if (hit.Grab == GrabPhase.Thrown || hit.Grab == GrabPhase.Strike && hit.KnockedDown) return FeelEvent.Throw;
            if (hit.Clash == ClashOutcome.StrikerWins) return FeelEvent.ClashBreak;
            if (hit.Empowered) return FeelEvent.EmpoweredHit;
            if (hit.DefenderArmored) return FeelEvent.ArmorHit;
            return hit.Technique.Weight >= Weight.Heavy ? FeelEvent.HeavyHit : FeelEvent.Hit;
        }
    }

    /// <summary>
    /// Drives a <see cref="Duel"/> with hit-stop. Call <see cref="Step"/> once per 60 Hz tick instead of
    /// <see cref="Duel.Step(SlotInput, SlotInput, MoveInput, MoveInput)"/>.
    /// <para>
    /// After a tick with strikes, the whole fight freezes for the largest hitstop of those strikes
    /// (feel sheet). While frozen, the duel doesn't advance at all: no frames, no stamina regen, no
    /// walking. So every number in the technique and rules sheets means exactly what it did before:
    /// hit-stop only adds still ticks between them. Slot presses (and a quick step asked for) during a freeze are kept and
    /// given to the duel on the first tick after it, so a chain pressed during the freeze isn't lost.
    /// </para>
    /// A brain (the dummy, later the AI) shouldn't think during a freeze: check <see cref="IsFrozen"/>
    /// before asking it, so its own timers stay in step with the fight.
    /// </summary>
    public sealed class FeelClock
    {
        private static readonly IReadOnlyList<FeelCue> NoCues = Array.Empty<FeelCue>();
        private byte _heldOne, _heldTwo;
        private StepDirection _heldStepOne, _heldStepTwo;

        public FeelClock(Duel duel, FeelSheet sheet)
        {
            Duel = duel ?? throw new ArgumentNullException(nameof(duel));
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        }

        public Duel Duel { get; }
        public FeelSheet Sheet { get; }

        /// <summary>False turns hit-stop off (strikes still give cues). The playtest's F3 toggles it.</summary>
        public bool HitStopOn { get; set; } = true;

        /// <summary>Frozen ticks still to come.</summary>
        public int FrozenFor { get; private set; }

        public bool IsFrozen => FrozenFor > 0;

        /// <summary>Ticks this clock has run, frozen ones included. <see cref="Duel.Tick"/> counts only the unfrozen ones.</summary>
        public int Ticks { get; private set; }

        /// <summary>The cues for the strikes of the last <see cref="Step"/> (empty if none, or frozen).</summary>
        public IReadOnlyList<FeelCue> Cues { get; private set; } = NoCues;

        /// <summary>Run one 60 Hz tick: a frozen one, or one tick of the duel. Returns the duel's hits (none while frozen).</summary>
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two, MoveInput moveOne = default, MoveInput moveTwo = default)
        {
            Ticks++;
            if (FrozenFor > 0)
            {
                FrozenFor--;
                _heldOne |= one.Bits;
                _heldTwo |= two.Bits;
                if (moveOne.QuickStep != StepDirection.None) _heldStepOne = moveOne.QuickStep;
                if (moveTwo.QuickStep != StepDirection.None) _heldStepTwo = moveTwo.QuickStep;
                Cues = NoCues;
                return Array.Empty<HitEvent>();
            }

            var inOne = new SlotInput((byte)(one.Bits | _heldOne), one.HeldBits);
            var inTwo = new SlotInput((byte)(two.Bits | _heldTwo), two.HeldBits);
            _heldOne = _heldTwo = 0;
            if (moveOne.QuickStep == StepDirection.None && _heldStepOne != StepDirection.None) moveOne = moveOne.WithQuickStep(_heldStepOne);
            if (moveTwo.QuickStep == StepDirection.None && _heldStepTwo != StepDirection.None) moveTwo = moveTwo.WithQuickStep(_heldStepTwo);
            _heldStepOne = _heldStepTwo = StepDirection.None;

            var hits = Duel.Step(inOne, inTwo, moveOne, moveTwo);
            if (hits.Count == 0)
            {
                Cues = NoCues;
                return hits;
            }

            var cues = new List<FeelCue>(hits.Count);
            int freeze = 0;
            foreach (var hit in hits)
            {
                var row = Sheet[FeelCue.Classify(hit)];
                cues.Add(new FeelCue(hit, row));
                freeze = Math.Max(freeze, row.HitStopFrames);
            }
            Cues = cues;
            FrozenFor = HitStopOn ? freeze : 0;
            return hits;
        }
    }
}
