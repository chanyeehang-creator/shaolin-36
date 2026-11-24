using Shaolin36.Core.Data;

namespace Shaolin36.Core.Combat
{
    /// <summary>What happens when a strike meets a 化劲 parry.</summary>
    public enum ParryOutcome
    {
        /// <summary>The parry was not in its window (or is not a parry): the hit is resolved normally.</summary>
        NotParried,
        /// <summary>Blocked: no damage, but the attacker isn't interrupted: their move and string carry on.</summary>
        Parried,
        /// <summary>Deflected in the first frames of the window: the attacker is staggered and drained.</summary>
        PerfectParry,
    }

    /// <summary>The timed parry rule. Pure functions over the technique and rules sheets.</summary>
    public static class Parry
    {
        /// <param name="parry">The defender's current technique.</param>
        /// <param name="parryFrame">The defender's current frame within it (0-based) when the strike lands.</param>
        public static ParryOutcome Resolve(Technique parry, int parryFrame)
        {
            if (!parry.IsParrying(parryFrame)) return ParryOutcome.NotParried;
            return parry.IsInPerfectParry(parryFrame) ? ParryOutcome.PerfectParry : ParryOutcome.Parried;
        }

        public static ParryEffect Effect(ParryOutcome outcome, Technique parry, Rules rules)
        {
            switch (outcome)
            {
                case ParryOutcome.Parried:
                    return new ParryEffect(true, rules.ParryRecovery, 0, 0, 0, 0);
                case ParryOutcome.PerfectParry:
                    return new ParryEffect(true, rules.ParryPerfectRecovery, parry.StaminaCost, rules.ParryPerfectQi,
                        rules.ParryPerfectStagger, rules.ParryPerfectDrain);
                default:
                    return new ParryEffect(false, 0, 0, 0, 0, 0);
            }
        }
    }

    public sealed class ParryEffect
    {
        public ParryEffect(bool deflected, int defenderRecoveryFrames, int defenderStaminaRefund, int defenderQiGain,
            int attackerStaggerFrames, int attackerStaminaLoss)
        {
            Deflected = deflected;
            DefenderRecoveryFrames = defenderRecoveryFrames;
            DefenderStaminaRefund = defenderStaminaRefund;
            DefenderQiGain = defenderQiGain;
            AttackerStaggerFrames = attackerStaggerFrames;
            AttackerStaminaLoss = attackerStaminaLoss;
        }

        /// <summary>The hit deals no damage. Only a perfect parry also stops the attacker (see <see cref="AttackerStaggerFrames"/>).</summary>
        public bool Deflected { get; }

        /// <summary>The rest of the parry is replaced by this many frames, then the defender can act.</summary>
        public int DefenderRecoveryFrames { get; }

        /// <summary>A perfect parry gives back the parry's whole stamina cost.</summary>
        public int DefenderStaminaRefund { get; }
        public int DefenderQiGain { get; }

        /// <summary>0 means the attacker isn't interrupted: a blocked string carries on.</summary>
        public int AttackerStaggerFrames { get; }
        public int AttackerStaminaLoss { get; }
    }
}
