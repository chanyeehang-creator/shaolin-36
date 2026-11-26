using Shaolin36.Core.Data;

namespace Shaolin36.Core.Combat
{
    /// <summary>What happens when a striker's hit meets a target who is winding up a move.</summary>
    public enum ClashOutcome
    {
        /// <summary>Not a clash: the target was outside its window, or a move can't clash. The hit is resolved normally.</summary>
        NoClash,
        /// <summary>Same weight: both recoil, lose a little stamina, neutral reset.</summary>
        BothRecoil,
        /// <summary>Striker is heavier: the target's move is interrupted and they stagger; the striker's continues.</summary>
        StrikerWins,
        /// <summary>Striker is lighter: the striker is interrupted.</summary>
        TargetWins,
        /// <summary>Hit in the first frames of the target's window: target breaks whatever the weights, is open, striker gains Qi.</summary>
        PerfectClash,
        /// <summary>
        /// Two pure energy moves meet (狮吼功 into 狮吼功): both cancel out whatever their Qi cost or damage. No damage,
        /// both moves stop and both recoil, pushed apart like a same-weight clash.
        /// </summary>
        EnergyCancel,
    }

    /// <summary>
    /// The clash table (拼招) from the concept doc. <see cref="Resolve"/> decides who wins;
    /// <see cref="Effect"/> turns that into frames, stamina and Qi from the rules sheet.
    /// Pure functions, no state, so every row is unit tested.
    /// </summary>
    public static class Clash
    {
        /// <param name="striker">The move whose hit is landing this frame.</param>
        /// <param name="target">The move the other fighter is performing.</param>
        /// <param name="targetFrame">The target's current frame within its move (0-based).</param>
        public static ClashOutcome Resolve(Technique striker, Technique target, int targetFrame)
        {
            // Pure energy is its own class: a physical move can't clash it, it clashes nothing physical, and two energy
            // moves cancel out when the strike meets the other in its clash window or while it is going off.
            if (striker.IsEnergy || target.IsEnergy)
            {
                bool meets = target.IsInClashWindow(targetFrame) || target.IsActive(targetFrame);
                return striker.IsEnergy && target.IsEnergy && meets ? ClashOutcome.EnergyCancel : ClashOutcome.NoClash;
            }
            if (!striker.Clashable || !target.IsInClashWindow(targetFrame)) return ClashOutcome.NoClash;
            if (target.IsInPerfectClash(targetFrame)) return ClashOutcome.PerfectClash;

            int s = EffectiveWeight(striker, target);
            int t = EffectiveWeight(target, striker);
            if (s == t) return ClashOutcome.BothRecoil;
            return s > t ? ClashOutcome.StrikerWins : ClashOutcome.TargetWins;
        }

        /// <summary>Weapon techniques count one step heavier against bare hands.</summary>
        public static int EffectiveWeight(Technique self, Technique other) =>
            (int)self.Weight + (self.IsWeapon && !other.IsWeapon ? 1 : 0);

        /// <summary>What each fighter gets from a clash result, with the numbers from the rules sheet.</summary>
        public static ClashEffect Effect(ClashOutcome outcome, Rules rules)
        {
            switch (outcome)
            {
                case ClashOutcome.EnergyCancel:
                    var cancel = new ClashSide(true, rules.ClashRecoilFrames, 0, 0);
                    return new ClashEffect(false, cancel, cancel);
                case ClashOutcome.BothRecoil:
                    var recoil = new ClashSide(true, rules.ClashRecoilFrames, rules.ClashRecoilStamina, 0);
                    return new ClashEffect(false, recoil, recoil);
                case ClashOutcome.StrikerWins:
                    return new ClashEffect(true, ClashSide.Unaffected,
                        new ClashSide(true, rules.ClashLoseStagger, rules.ClashLoseStamina, 0));
                case ClashOutcome.TargetWins:
                    return new ClashEffect(false,
                        new ClashSide(true, rules.ClashLoseStagger, rules.ClashLoseStamina, 0), ClashSide.Unaffected);
                case ClashOutcome.PerfectClash:
                    return new ClashEffect(false,
                        new ClashSide(true, rules.ClashPerfectRecovery, 0, rules.ClashPerfectQi),
                        new ClashSide(true, rules.ClashPerfectStagger, 0, 0));
                default:
                    return new ClashEffect(true, ClashSide.Unaffected, ClashSide.Unaffected);
            }
        }
    }

    /// <summary>What a clash does to one fighter.</summary>
    public sealed class ClashSide
    {
        public static readonly ClashSide Unaffected = new ClashSide(false, 0, 0, 0);

        public ClashSide(bool moveCancelled, int stunFrames, int staminaLoss, int qiGain)
        {
            MoveCancelled = moveCancelled;
            StunFrames = stunFrames;
            StaminaLoss = staminaLoss;
            QiGain = qiGain;
        }

        /// <summary>The fighter's current move stops; they spend <see cref="StunFrames"/> unable to act.</summary>
        public bool MoveCancelled { get; }

        /// <summary>Recoil, stagger or recovery frames after the move is cancelled.</summary>
        public int StunFrames { get; }
        public int StaminaLoss { get; }
        public int QiGain { get; }
    }

    /// <summary>The full result of a clash: whether the striker's hit still deals its damage, and each side's effect.</summary>
    public sealed class ClashEffect
    {
        public ClashEffect(bool strikerHitLands, ClashSide striker, ClashSide target)
        {
            StrikerHitLands = strikerHitLands;
            Striker = striker;
            Target = target;
        }

        /// <summary>True when the striker's hit deals its normal damage and hitstun.</summary>
        public bool StrikerHitLands { get; }
        public ClashSide Striker { get; }
        public ClashSide Target { get; }
    }
}
