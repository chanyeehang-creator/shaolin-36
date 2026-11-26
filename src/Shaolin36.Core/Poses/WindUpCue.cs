using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// What an attack's wind-up tells the other fighter: how heavy it is (and so what beats it in a clash), or that it is a
    /// grab, which no parry or guard stops (the game shows grabs flashing).
    /// </summary>
    public enum WindUpCue
    {
        /// <summary>Not winding up an attack (idle, a parry or guard, a dash, a heal, or past the startup).</summary>
        None,
        Light,
        Medium,
        Heavy,
        /// <summary>Pure energy (狮吼功): no physical move clashes it, and a parry only blocks it, never perfectly.</summary>
        Energy,
        /// <summary>A 擒拿 grab: goes through parries and guards. Strike it or step back.</summary>
        Grab,
    }

    public static class WindUpCues
    {
        /// <summary>The cue for a technique's wind-up, or None if it isn't an attack.</summary>
        public static WindUpCue Of(Technique t)
        {
            if (t.IsGrab) return WindUpCue.Grab;
            if (t.ReachCm <= 0 || t.IsGuard || t.Category == Category.Fangshen || t.IsDash) return WindUpCue.None;
            switch (t.Weight)
            {
                case Weight.Medium: return WindUpCue.Medium;
                case Weight.Heavy: return WindUpCue.Heavy;
                case Weight.Energy: return WindUpCue.Energy;
                default: return WindUpCue.Light;
            }
        }

        /// <summary>The cue a fighter shows this frame: only while in the startup of an attack.</summary>
        public static WindUpCue Of(Fighter f) =>
            f.State == FighterState.Startup && f.Technique != null ? Of(f.Technique) : WindUpCue.None;

        /// <summary>True for cues the game flashes: moves that can't be parried or guarded.</summary>
        public static bool Flashes(WindUpCue cue) => cue == WindUpCue.Grab;

        /// <summary>
        /// How far through the wind-up the fighter is, 0-1000 (1000 on the last startup frame), so the glow can build
        /// up to the strike. 0 when not in a startup.
        /// </summary>
        public static int Progress(Fighter f)
        {
            if (f.State != FighterState.Startup || f.Technique == null) return 0;
            int s = f.Technique.StartupFrames;
            return s <= 0 ? 1000 : (f.MoveFrame + 1) * 1000 / s;
        }
    }
}
