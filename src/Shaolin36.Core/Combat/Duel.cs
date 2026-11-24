using System;
using System.Collections.Generic;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Combat
{
    /// <summary>Which of the two fighters in a duel.</summary>
    public enum Side
    {
        /// <summary>The player (or the first fighter in a test).</summary>
        One = 0,
        /// <summary>The opponent.</summary>
        Two = 1,
    }

    /// <summary>A hit that connected on a tick.</summary>
    public sealed class HitEvent
    {
        public HitEvent(int tick, Side attacker, Technique technique, int damage, int hitstunFrames, int defenderHpAfter,
            Technique? defenderTechnique = null, int defenderMoveFrame = -1)
        {
            DefenderTechnique = defenderTechnique;
            DefenderMoveFrame = defenderTechnique == null ? -1 : defenderMoveFrame;
            Tick = tick;
            Attacker = attacker;
            Technique = technique;
            Damage = damage;
            HitstunFrames = hitstunFrames;
            DefenderHpAfter = defenderHpAfter;
        }

        /// <summary>The duel tick the hit landed on (the first tick is 0).</summary>
        public int Tick { get; }
        public Side Attacker { get; }
        public Side Defender => Attacker == Side.One ? Side.Two : Side.One;
        public Technique Technique { get; }
        public int Damage { get; }
        public int HitstunFrames { get; }
        public int DefenderHpAfter { get; }

        /// <summary>What the defender was doing when the hit landed (null if idle or stunned).</summary>
        public Technique? DefenderTechnique { get; }

        /// <summary>The defender's frame within <see cref="DefenderTechnique"/> when the hit landed; -1 if none.</summary>
        public int DefenderMoveFrame { get; }

        public override string ToString() =>
            $"t{Tick} {Attacker} hits with {Technique.Id}: {Damage} damage, {HitstunFrames}f hitstun, {DefenderHpAfter} HP left";
    }

    /// <summary>
    /// Two fighters on a line, positions in whole centimetres, advanced together one tick
    /// at a time. Each tick:
    /// <list type="number">
    /// <item>Both fighters advance a frame and act on their slot presses.</item>
    /// <item>Each fighter in the active frames of a move that hasn't hit yet checks reach:
    /// if the other fighter stands within the technique's reach (distance &lt;= reach),
    /// the hit connects. A move hits at most once, on its first active frame in reach;
    /// a target who walks into reach later in the active frames still gets hit.</item>
    /// <item>All hits found this tick land together, so two hits on the same tick trade:
    /// both deal damage and both fighters go into hitstun. Hitstun counts from this tick
    /// (see <see cref="Fighter"/>).</item>
    /// </list>
    /// A technique with reach 0 (such as the 化绵掌 parry) never hits. Clash and parry
    /// are not resolved here yet: every connecting hit deals its full damage and hitstun.
    /// Once a fighter is down the duel is over and ticks do nothing.
    ///
    /// Walking: each tick, after acting, a fighter who is idle walks one step in the
    /// direction given (-1 left, +1 right, 0 stand) at the rules sheet's walk_speed. Speeds
    /// are whole centimetres per second, so each fighter carries the leftover fraction of
    /// a centimetre in sixtieths, and the same inputs always give the same positions. A fighter
    /// stops at the ends of the arena (arena_width, centred on 0) and can't come closer than
    /// min_distance to the other or pass through them; when both walk into each other, the
    /// overlap is split between them.
    /// </summary>
    public sealed class Duel
    {
        private readonly Fighter[] _fighters;
        private readonly List<HitEvent> _hits = new List<HitEvent>();
        private readonly int[] _walkRemainder = new int[2];

        public Duel(Rules rules, Loadout one, Loadout two, int onePositionCm, int twoPositionCm)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Rules = rules;
            _fighters = new[]
            {
                new Fighter(one, rules.HpMax, onePositionCm, rules.InputBuffer),
                new Fighter(two, rules.HpMax, twoPositionCm, rules.InputBuffer),
            };
        }

        public Rules Rules { get; }

        /// <summary>A duel with the fighters start_distance apart, centred on 0, One on the left.</summary>
        public static Duel Start(Rules rules, Loadout one, Loadout two)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int left = -(rules.StartDistance / 2);
            return new Duel(rules, one, two, left, left + rules.StartDistance);
        }

        /// <summary>Ticks run so far. The next tick is numbered this.</summary>
        public int Tick { get; private set; }

        public Fighter One => _fighters[0];
        public Fighter Two => _fighters[1];
        public Fighter this[Side side] => _fighters[(int)side];

        /// <summary>Distance between the two fighters in centimetres.</summary>
        public int DistanceCm => Math.Abs(One.PositionCm - Two.PositionCm);

        public bool IsOver => One.IsDown || Two.IsDown;

        /// <summary>Every hit that has landed, in order.</summary>
        public IReadOnlyList<HitEvent> Hits => _hits;

        /// <summary>Run one tick and return the hits that landed on it (empty, one, or two for a trade).</summary>
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two) => Step(one, two, 0, 0);

        /// <summary>
        /// Run one tick with walking: <paramref name="walkOne"/> and <paramref name="walkTwo"/> are
        /// -1 (left), 0 (stand) or +1 (right). Returns the hits that landed on this tick.
        /// </summary>
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two, int walkOne, int walkTwo)
        {
            if (IsOver) return Array.Empty<HitEvent>();

            One.Tick(one);
            Two.Tick(two);
            Walk(Math.Sign(walkOne), Math.Sign(walkTwo));

            // Find both hits before applying either, so a same-tick trade doesn't depend on order.
            bool oneHits = Connects(One, DistanceCm);
            bool twoHits = Connects(Two, DistanceCm);
            Technique? oneMove = One.Technique, twoMove = Two.Technique;
            if (oneHits) One.MarkHitLanded();
            if (twoHits) Two.MarkHitLanded();

            // Note what each defender was doing before either hit changes it.
            int oneFrame = One.MoveFrame, twoFrame = Two.MoveFrame;
            var landed = new List<HitEvent>(2);
            if (oneHits) landed.Add(Land(Side.One, oneMove!, Two, twoMove, twoFrame));
            if (twoHits) landed.Add(Land(Side.Two, twoMove!, One, oneMove, oneFrame));

            _hits.AddRange(landed);
            Tick++;
            return landed;
        }

        /// <summary>True if <paramref name="attacker"/>'s move connects this tick at this distance.</summary>
        public static bool Connects(Fighter attacker, int distanceCm) =>
            attacker.State == FighterState.Active
            && !attacker.HitLanded
            && attacker.Technique!.ReachCm > 0
            && distanceCm <= attacker.Technique.ReachCm;

        private HitEvent Land(Side attacker, Technique move, Fighter defender, Technique? defenderMove, int defenderFrame)
        {
            defender.TakeHit(move.Damage, move.HitstunFrames);
            return new HitEvent(Tick, attacker, move, move.Damage, move.HitstunFrames, defender.Hp, defenderMove, defenderFrame);
        }

        private void Walk(int dirOne, int dirTwo)
        {
            int stepOne = WalkStep(0, One, dirOne);
            int stepTwo = WalkStep(1, Two, dirTwo);
            if (stepOne == 0 && stepTwo == 0) return;

            int half = Rules.ArenaWidth / 2;
            int a = Clamp(One.PositionCm + stepOne, -half, half);
            int b = Clamp(Two.PositionCm + stepTwo, -half, half);

            // Keep them at least min_distance apart without swapping sides.
            bool oneOnLeft = One.PositionCm <= Two.PositionCm;
            int gap = oneOnLeft ? b - a : a - b;
            int overlap = Rules.MinDistance - gap;
            if (overlap > 0)
            {
                // Only a fighter who walked toward the other gives ground; if both did, they split it.
                bool oneClosed = oneOnLeft ? stepOne > 0 : stepOne < 0;
                bool twoClosed = oneOnLeft ? stepTwo < 0 : stepTwo > 0;
                int backOne = oneClosed && twoClosed ? (overlap + 1) / 2 : oneClosed ? overlap : 0;
                int backTwo = twoClosed ? overlap - backOne : 0;
                // Never push a fighter back past where they stood before this tick.
                backOne = Math.Min(backOne, Math.Abs(stepOne));
                backTwo = Math.Min(backTwo, Math.Abs(stepTwo));
                a += oneOnLeft ? -backOne : backOne;
                b += oneOnLeft ? backTwo : -backTwo;
            }

            One.PositionCm = a;
            Two.PositionCm = b;
        }

        /// <summary>How many whole centimetres <paramref name="f"/> walks this tick, carrying the fraction.</summary>
        private int WalkStep(int index, Fighter f, int dir)
        {
            if (dir == 0 || !f.CanAct)
            {
                _walkRemainder[index] = 0;
                return 0;
            }
            int sixtieths = _walkRemainder[index] + Rules.WalkSpeed;
            _walkRemainder[index] = sixtieths % Sim.TicksPerSecond;
            return dir * (sixtieths / Sim.TicksPerSecond);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
