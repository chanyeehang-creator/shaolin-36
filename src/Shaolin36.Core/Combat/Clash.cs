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
    }

    /// <summary>
    /// The clash table (拼招) from the concept doc. Starting values to tune; Stage 1
    /// reviews the numbers. Pure function, no state, so every row is unit tested.
    /// </summary>
    public static class Clash
    {
        /// <param name="striker">The move whose hit is landing this frame.</param>
        /// <param name="target">The move the other fighter is performing.</param>
        /// <param name="targetFrame">The target's current frame within its move (0-based).</param>
        public static ClashOutcome Resolve(Technique striker, Technique target, int targetFrame)
        {
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
    }
}
