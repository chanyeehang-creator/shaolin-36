namespace Shaolin36.Core.Fighters
{
    /// <summary>What a fighter is doing on this tick.</summary>
    public enum FighterState
    {
        /// <summary>Free to act: a slot press starts its technique this tick.</summary>
        Idle,
        /// <summary>Winding up a technique; the clash window is the end of this phase.</summary>
        Startup,
        /// <summary>The technique's hit (or parry window) is live.</summary>
        Active,
        /// <summary>Finishing a technique; can't act until it ends.</summary>
        Recovery,
        /// <summary>Frozen after being hit.</summary>
        Hitstun,
        /// <summary>Broken out of a move by a lost clash or a perfect parry; open to punishment.</summary>
        Stagger,
        /// <summary>Knocked back by a same-weight clash or a won perfect clash, or recovering from a successful parry.</summary>
        Recoil,
        /// <summary>Doing a quick step (the short slide, then its few recovery frames). Can be hit like any other time.</summary>
        QuickStep,
        /// <summary>Caught by a 擒拿 grab: held until the throw lands, unable to act, escape or be grabbed again.</summary>
        Grabbed,
    }

    public static class FighterStates
    {
        /// <summary>True for the three states where the fighter is performing a technique.</summary>
        public static bool IsInMove(this FighterState s) =>
            s == FighterState.Startup || s == FighterState.Active || s == FighterState.Recovery;

        /// <summary>True for the states where something was done to the fighter and they wait it out.</summary>
        public static bool IsStunned(this FighterState s) =>
            s == FighterState.Hitstun || s == FighterState.Stagger || s == FighterState.Recoil || s == FighterState.Grabbed;
    }
}
