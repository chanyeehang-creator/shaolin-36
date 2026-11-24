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

    /// <summary>
    /// A strike that reached the other fighter on a tick. Most deal their damage, but a strike
    /// can also be deflected by a 化绵掌 parry or meet the defender's wind-up in a clash
    /// (<see cref="Landed"/> tells which).
    /// </summary>
    public sealed class HitEvent
    {
        public HitEvent(int tick, Side attacker, Technique technique, int damage, int hitstunFrames, int defenderHpAfter,
            Technique? defenderTechnique = null, int defenderMoveFrame = -1,
            ParryOutcome parry = ParryOutcome.NotParried, ParryEffect? parryEffect = null,
            ClashOutcome clash = ClashOutcome.NoClash, ClashEffect? clashEffect = null, bool defenderExhausted = false,
            int attackerQiGained = 0, int defenderQiGained = 0, bool empowered = false, bool defenderArmored = false,
            int knockbackCm = 0)
        {
            KnockbackCm = knockbackCm;
            Empowered = empowered;
            DefenderArmored = defenderArmored;
            AttackerQiGained = attackerQiGained;
            DefenderQiGained = defenderQiGained;
            DefenderExhausted = defenderExhausted;
            DefenderTechnique = defenderTechnique;
            DefenderMoveFrame = defenderTechnique == null ? -1 : defenderMoveFrame;
            Tick = tick;
            Attacker = attacker;
            Technique = technique;
            Damage = damage;
            HitstunFrames = hitstunFrames;
            DefenderHpAfter = defenderHpAfter;
            Parry = parry;
            ParryEffect = parryEffect;
            Clash = clash;
            ClashEffect = clashEffect;
        }

        /// <summary>The duel tick the hit landed on (the first tick is 0).</summary>
        public int Tick { get; }
        public Side Attacker { get; }
        public Side Defender => Attacker == Side.One ? Side.Two : Side.One;
        public Technique Technique { get; }
        /// <summary>HP the defender lost. 0 when the strike was parried or lost its clash.</summary>
        public int Damage { get; }

        /// <summary>Frames the defender is frozen from this tick. 0 when the strike didn't land.</summary>
        public int HitstunFrames { get; }
        public int DefenderHpAfter { get; }

        /// <summary>What the defender was doing when the hit landed (null if idle or stunned).</summary>
        public Technique? DefenderTechnique { get; }

        /// <summary>The defender's frame within <see cref="DefenderTechnique"/> when the hit landed; -1 if none.</summary>
        public int DefenderMoveFrame { get; }

        /// <summary>How a 化绵掌 parry dealt with the strike; <see cref="ParryOutcome.NotParried"/> if it didn't.</summary>
        public ParryOutcome Parry { get; }

        /// <summary>The parry's numbers (stamina and Qi already applied); null if not parried.</summary>
        public ParryEffect? ParryEffect { get; }

        /// <summary>The clash the strike caused against the defender's wind-up; <see cref="ClashOutcome.NoClash"/> if none.</summary>
        public ClashOutcome Clash { get; }

        /// <summary>The clash's numbers (stamina and Qi already applied); null if no clash.</summary>
        public ClashEffect? ClashEffect { get; }

        /// <summary>True when the defender was exhausted as the strike landed, so a landed hit dealt extra damage.</summary>
        public bool DefenderExhausted { get; }

        /// <summary>Qi the attacker gained from this strike (qi_per_hit if it landed, clash_perfect_qi for a perfect clash), after the cap.</summary>
        public int AttackerQiGained { get; }

        /// <summary>Qi the defender gained from this strike (parry_perfect_qi for a perfect parry), after the cap.</summary>
        public int DefenderQiGained { get; }

        /// <summary>True when the strike came from a Qi-empowered move (its damage is already multiplied).</summary>
        public bool Empowered { get; }

        /// <summary>True when the defender's hyper armor took the hit: damage dealt, but no hitstun.</summary>
        public bool DefenderArmored { get; }

        /// <summary>
        /// How far this strike pushes the defender back, in centimetres (the technique's knockback for a landed
        /// hit, clash_recoil_push for a same-weight clash, which pushes both). 0 for hyper armor, parries and lost clashes.
        /// </summary>
        public int KnockbackCm { get; }

        /// <summary>True when the strike dealt its damage and hitstun.</summary>
        public bool Landed => Parry == ParryOutcome.NotParried && (ClashEffect == null || ClashEffect.StrikerHitLands);

        public override string ToString() =>
            Parry != ParryOutcome.NotParried ? $"t{Tick} {Attacker}'s {Technique.Id} is {Parry}"
            : Clash != ClashOutcome.NoClash && !Landed ? $"t{Tick} {Attacker}'s {Technique.Id} clashes: {Clash}"
            : $"t{Tick} {Attacker} hits with {Technique.Id}: {Damage} damage, {HitstunFrames}f hitstun, {DefenderHpAfter} HP left";
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
    /// A technique with reach 0 (such as the 化绵掌 parry) never hits.
    /// Once a fighter is down the duel is over and ticks do nothing.
    ///
    /// What a connecting strike does depends on what the defender was doing on that tick
    /// (all stun frames count from this tick, like hitstun):
    /// <list type="bullet">
    /// <item><b>Parry</b> (化劲, see <see cref="Combat.Parry"/>): if the defender's parry is in its
    /// window, the strike deals nothing. The defender's parry ends and they recover
    /// (<see cref="FighterState.Recoil"/>) for parry_recovery frames, or parry_perfect_recovery
    /// in the perfect frames. A plain parry (a block) doesn't interrupt the attacker at all: their
    /// move and string carry on, so the defender has to tap again for the next hit. Only a
    /// perfect parry stops the attack: the attacker is staggered for parry_perfect_stagger frames. A parry pressed too early or too late does nothing,
    /// and the strike lands as normal.</item>
    /// <item><b>Clash</b> (拼招, see <see cref="Combat.Clash"/>): if the defender is in the clash window
    /// of a wind-up, the clash table decides. Same weight: both moves stop and both recoil.
    /// Heavier striker: the hit lands with full damage and the defender is staggered for the
    /// longer of the hitstun and clash_lose_stagger. Lighter striker: no damage, the striker's
    /// move stops and they stagger, the defender's move carries on. Perfect clash: no damage,
    /// the striker's move stops and they recover in clash_perfect_recovery frames
    /// (<see cref="FighterState.Recoil"/>), the defender's move stops and they stagger.</item>
    /// <item>Otherwise the hit lands: damage and hitstun.</item>
    /// </list>
    /// Stamina (see <see cref="Fighter"/> for costs, regen and exhaustion): every move pays its cost
    /// when it starts, so each 化绵掌 tap costs its stamina whether it blocks, misses or is perfect.
    /// A perfect parry refunds the defender that cost and drains the attacker parry_perfect_drain.
    /// Clashes take each side's clash stamina. A landed hit on an exhausted fighter deals
    /// exhausted_damage_pct of its damage.
    ///
    /// Qi (both fighters start a duel on qi_start, or on what they carried in; never above qi_max):
    /// a strike that deals damage (a normal hit, or a heavier move winning its clash) gives the
    /// attacker qi_per_hit; a perfect parry gives the defender parry_perfect_qi; a perfect clash gives
    /// the striker clash_perfect_qi. Blocks, lost clashes and same-weight recoils give nothing.
    /// What was gained is on the <see cref="HitEvent"/>.
    ///
    /// Empowered moves (see <see cref="Fighter.Empowered"/>): the hit deals empower_damage_pct of its
    /// damage (before the exhausted bonus). A defender with hyper armor (<see cref="Fighter.HasArmor"/>)
    /// takes the damage of a landed hit but no hitstun, so their move carries on. Armor doesn't stop
    /// parries or clashes: a perfect parry still staggers an armored attacker, and the clash table still
    /// applies to an armored wind-up.
    ///
    /// Knockback: a landed hit pushes the defender straight back from the attacker by the technique's
    /// knockback, and a same-weight clash pushes both apart by clash_recoil_push. The push is slid over
    /// knockback_frames ticks, starting the tick after the strike, in whole centimetres (larger steps
    /// first); a new push adds to what is left and restarts the slide. Hyper armor holds its ground
    /// (no knockback), and the arena ends stop a push.
    ///
    /// Walking: each tick, after acting, a fighter who is idle walks one step in the
    /// direction given (-1 left, +1 right, 0 stand) at the rules sheet's walk_speed. Speeds
    /// are whole centimetres per second, so each fighter carries the leftover fraction of
    /// a centimetre in sixtieths, and the same inputs always give the same positions. An exhausted
    /// fighter walks at exhausted_walk_pct of the speed. A fighter
    /// stops at the ends of the arena (arena_width, centred on 0) and can't come closer than
    /// min_distance to the other or pass through them; when both walk into each other, the
    /// overlap is split between them.
    /// </summary>
    public sealed class Duel
    {
        private readonly Fighter[] _fighters;
        private readonly List<HitEvent> _hits = new List<HitEvent>();
        private readonly int[] _walkRemainder = new int[2];
        private readonly int[] _pushLeft = new int[2];
        private readonly int[] _pushTicks = new int[2];

        /// <param name="oneQi">Qi fighter One starts with; null for the rules sheet's qi_start. A run passes what it carried over.</param>
        /// <param name="twoQi">Qi fighter Two starts with; null for qi_start.</param>
        public Duel(Rules rules, Loadout one, Loadout two, int onePositionCm, int twoPositionCm, int? oneQi = null, int? twoQi = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Rules = rules;
            _fighters = new[]
            {
                new Fighter(one, rules.HpMax, onePositionCm, rules.InputBuffer, StaminaRules.FromRules(rules),
                    rules.QiMax, oneQi ?? rules.QiStart),
                new Fighter(two, rules.HpMax, twoPositionCm, rules.InputBuffer, StaminaRules.FromRules(rules),
                    rules.QiMax, twoQi ?? rules.QiStart),
            };
        }

        public Rules Rules { get; }

        /// <summary>A duel with the fighters start_distance apart, centred on 0, One on the left.</summary>
        /// <param name="oneQi">Qi fighter One starts with; null for qi_start.</param>
        /// <param name="twoQi">Qi fighter Two starts with; null for qi_start.</param>
        public static Duel Start(Rules rules, Loadout one, Loadout two, int? oneQi = null, int? twoQi = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int left = -(rules.StartDistance / 2);
            return new Duel(rules, one, two, left, left + rules.StartDistance, oneQi, twoQi);
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

            One.Tick(one, walkOne != 0);
            Two.Tick(two, walkTwo != 0);
            Walk(Math.Sign(walkOne), Math.Sign(walkTwo));
            Push(Side.One);
            Push(Side.Two);

            // Find both hits before applying either, so a same-tick trade doesn't depend on order.
            bool oneHits = Connects(One, DistanceCm);
            bool twoHits = Connects(Two, DistanceCm);
            Technique? oneMove = One.Technique, twoMove = Two.Technique;
            bool oneEmpowered = One.Empowered, twoEmpowered = Two.Empowered;
            bool oneArmored = One.HasArmor, twoArmored = Two.HasArmor;
            if (oneHits) One.MarkHitLanded();
            if (twoHits) Two.MarkHitLanded();

            // Note what each defender was doing before either hit changes it.
            int oneFrame = One.MoveFrame, twoFrame = Two.MoveFrame;
            var landed = new List<HitEvent>(2);
            if (oneHits) landed.Add(Land(Side.One, oneMove!, oneEmpowered, Two, twoMove, twoFrame, twoArmored));
            if (twoHits) landed.Add(Land(Side.Two, twoMove!, twoEmpowered, One, oneMove, oneFrame, oneArmored));

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

        /// <summary>The damage <paramref name="move"/> deals, before the exhausted bonus.</summary>
        public static int StrikeDamage(Technique move, bool empowered) =>
            empowered ? (move.Damage * move.EmpowerDamagePct + 50) / 100 : move.Damage;

        private HitEvent Land(Side attacker, Technique move, bool empowered, Fighter defender, Technique? defenderMove,
            int defenderFrame, bool defenderArmored)
        {
            var striker = this[attacker];

            if (defenderMove != null)
            {
                var parry = Combat.Parry.Resolve(defenderMove, defenderFrame);
                if (parry != ParryOutcome.NotParried)
                {
                    var e = Combat.Parry.Effect(parry, defenderMove, Rules);
                    defender.Stun(FighterState.Recoil, e.DefenderRecoveryFrames);
                    if (e.AttackerStaggerFrames > 0) striker.Stun(FighterState.Stagger, e.AttackerStaggerFrames);
                    defender.GainStamina(e.DefenderStaminaRefund);
                    striker.LoseStamina(e.AttackerStaminaLoss);
                    int parryQi = defender.GainQi(e.DefenderQiGain);
                    return new HitEvent(Tick, attacker, move, 0, 0, defender.Hp, defenderMove, defenderFrame, parry, e,
                        defenderQiGained: parryQi, empowered: empowered);
                }

                var clash = Combat.Clash.Resolve(move, defenderMove, defenderFrame);
                if (clash != ClashOutcome.NoClash)
                {
                    var e = Combat.Clash.Effect(clash, Rules);
                    striker.LoseStamina(e.Striker.StaminaLoss);
                    if (e.StrikerHitLands)
                    {
                        // The heavier move breaks through: full damage, and the defender stays
                        // frozen for the longer of the hitstun and the clash stagger.
                        bool tired = defender.IsExhausted;
                        int damage = defender.DamageTaken(StrikeDamage(move, empowered));
                        defender.TakeHit(damage, 0);
                        defender.LoseStamina(e.Target.StaminaLoss);
                        int frames = Math.Max(move.HitstunFrames, e.Target.StunFrames);
                        if (frames > 0 && !defender.IsDown) defender.Stun(FighterState.Stagger, frames);
                        int breakQi = striker.GainQi(Rules.QiPerHit);
                        StartPush(OtherSide(attacker), move.KnockbackCm);
                        return new HitEvent(Tick, attacker, move, damage, frames, defender.Hp, defenderMove, defenderFrame,
                            clash: clash, clashEffect: e, defenderExhausted: tired, attackerQiGained: breakQi, empowered: empowered,
                            knockbackCm: move.KnockbackCm);
                    }
                    defender.LoseStamina(e.Target.StaminaLoss);
                    ApplyClashSide(striker, e.Striker, clash == ClashOutcome.TargetWins ? FighterState.Stagger : FighterState.Recoil);
                    ApplyClashSide(defender, e.Target, clash == ClashOutcome.PerfectClash ? FighterState.Stagger : FighterState.Recoil);
                    int strikerQi = striker.GainQi(e.Striker.QiGain);
                    int targetQi = defender.GainQi(e.Target.QiGain);
                    int recoilPush = clash == ClashOutcome.BothRecoil ? Rules.ClashRecoilPush : 0;
                    StartPush(attacker, recoilPush);
                    StartPush(OtherSide(attacker), recoilPush);
                    return new HitEvent(Tick, attacker, move, 0, 0, defender.Hp, defenderMove, defenderFrame,
                        clash: clash, clashEffect: e, attackerQiGained: strikerQi, defenderQiGained: targetQi, empowered: empowered,
                        knockbackCm: recoilPush);
                }
            }

            bool exhausted = defender.IsExhausted;
            int dealt = defender.DamageTaken(StrikeDamage(move, empowered));
            // Hyper armor: the damage goes through, but the defender's move isn't interrupted.
            int hitstun = defenderArmored ? 0 : move.HitstunFrames;
            defender.TakeHit(dealt, hitstun);
            int hitQi = striker.GainQi(Rules.QiPerHit);
            // Hyper armor holds its ground.
            int knockback = defenderArmored ? 0 : move.KnockbackCm;
            StartPush(OtherSide(attacker), knockback);
            return new HitEvent(Tick, attacker, move, dealt, hitstun, defender.Hp, defenderMove, defenderFrame,
                defenderExhausted: exhausted, attackerQiGained: hitQi, empowered: empowered, defenderArmored: defenderArmored,
                knockbackCm: knockback);
        }

        private static Side OtherSide(Side side) => side == Side.One ? Side.Two : Side.One;

        /// <summary>Centimetres of knockback <paramref name="side"/> still has to slide.</summary>
        public int PushLeftCm(Side side) => _pushLeft[(int)side];

        private void StartPush(Side side, int cm)
        {
            if (cm <= 0) return;
            _pushLeft[(int)side] += cm;
            _pushTicks[(int)side] = Rules.KnockbackFrames;
        }

        /// <summary>Slide <paramref name="side"/> one tick's worth of its push, away from the other fighter.</summary>
        private void Push(Side side)
        {
            int i = (int)side;
            if (_pushLeft[i] <= 0 || _pushTicks[i] <= 0) return;
            int step = (_pushLeft[i] + _pushTicks[i] - 1) / _pushTicks[i];
            _pushLeft[i] -= step;
            _pushTicks[i]--;

            var me = this[side];
            var other = this[OtherSide(side)];
            // Away from the other fighter; if they stand on the same spot, One goes left and Two right.
            int dir = me.PositionCm != other.PositionCm ? Math.Sign(me.PositionCm - other.PositionCm) : (side == Side.One ? -1 : 1);
            int half = Rules.ArenaWidth / 2;
            me.PositionCm = Clamp(me.PositionCm + dir * step, -half, half);
        }

        private static void ApplyClashSide(Fighter f, ClashSide side, FighterState state)
        {
            if (side.MoveCancelled && side.StunFrames > 0) f.Stun(state, side.StunFrames);
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
            int speed = f.IsExhausted ? Rules.WalkSpeed * Rules.ExhaustedWalkPct / 100 : Rules.WalkSpeed;
            int sixtieths = _walkRemainder[index] + speed;
            _walkRemainder[index] = sixtieths % Sim.TicksPerSecond;
            return dir * (sixtieths / Sim.TicksPerSecond);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
