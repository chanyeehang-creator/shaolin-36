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
        public HitEvent(int tick, Side attacker, Technique technique, int damage, int hitstunFrames, int defenderHpAfter)
        {
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
    /// </summary>
    public sealed class Duel
    {
        private readonly Fighter[] _fighters;
        private readonly List<HitEvent> _hits = new List<HitEvent>();

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
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two)
        {
            if (IsOver) return Array.Empty<HitEvent>();

            One.Tick(one);
            Two.Tick(two);

            // Find both hits before applying either, so a same-tick trade doesn't depend on order.
            bool oneHits = Connects(One, DistanceCm);
            bool twoHits = Connects(Two, DistanceCm);
            Technique? oneMove = One.Technique, twoMove = Two.Technique;
            if (oneHits) One.MarkHitLanded();
            if (twoHits) Two.MarkHitLanded();

            var landed = new List<HitEvent>(2);
            if (oneHits) landed.Add(Land(Side.One, oneMove!, Two));
            if (twoHits) landed.Add(Land(Side.Two, twoMove!, One));

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

        private HitEvent Land(Side attacker, Technique move, Fighter defender)
        {
            defender.TakeHit(move.Damage, move.HitstunFrames);
            return new HitEvent(Tick, attacker, move, move.Damage, move.HitstunFrames, defender.Hp);
        }
    }
}
