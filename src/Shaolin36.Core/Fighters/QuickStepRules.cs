using System;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// The quick step's numbers (from the rules sheet): a short, fast step in one of four directions that
    /// anyone can do at any time they're free. Not a roll or a dash: no invincible frames and no perfect-timing
    /// dodge, just a quick way to change distance or get off the line of a strike.
    /// </summary>
    public sealed class QuickStepRules
    {
        public QuickStepRules(int distanceCm, int moveFrames, int recoveryFrames, int stamina)
        {
            if (distanceCm < 0) throw new ArgumentOutOfRangeException(nameof(distanceCm), distanceCm, "the distance cannot be negative");
            if (moveFrames < 1) throw new ArgumentOutOfRangeException(nameof(moveFrames), moveFrames, "the step takes at least 1 frame");
            if (recoveryFrames < 0) throw new ArgumentOutOfRangeException(nameof(recoveryFrames), recoveryFrames, "recovery cannot be negative");
            if (stamina < 0) throw new ArgumentOutOfRangeException(nameof(stamina), stamina, "the cost cannot be negative");
            DistanceCm = distanceCm;
            MoveFrames = moveFrames;
            RecoveryFrames = recoveryFrames;
            Stamina = stamina;
        }

        /// <summary>The rules sheet's quick_step_distance, quick_step_frames, quick_step_recovery and quick_step_stamina.</summary>
        public static QuickStepRules FromRules(Rules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new QuickStepRules(rules.QuickStepDistance, rules.QuickStepFrames, rules.QuickStepRecovery, rules.QuickStepStamina);
        }

        /// <summary>How far the step goes, in centimetres.</summary>
        public int DistanceCm { get; }

        /// <summary>Frames the step slides over, from the tick it starts.</summary>
        public int MoveFrames { get; }

        /// <summary>Frames after the slide before the fighter is free again.</summary>
        public int RecoveryFrames { get; }

        /// <summary>Stamina paid when the step starts.</summary>
        public int Stamina { get; }

        public int TotalFrames => MoveFrames + RecoveryFrames;
    }
}
