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

    /// <summary>Where a 擒拿 grab is up to in a <see cref="HitEvent"/>.</summary>
    public enum GrabPhase
    {
        /// <summary>Not a grab.</summary>
        None = 0,
        /// <summary>The grab caught the defender: no damage yet, the hold starts.</summary>
        Caught,
        /// <summary>The hold ran out and the throw landed its damage, hitstun and knockback.</summary>
        Thrown,
        /// <summary>龙爪手: the grabber tapped and struck the held fighter (damage; the last strike knocks down and ends the hold).</summary>
        Strike,
        /// <summary>A multi-strike grab's hold ran out with no tap: the held fighter broke free by themselves. No damage.</summary>
        Released,
        /// <summary>
        /// The held fighter broke out with a grab of their own, in time (<see cref="Fighter.HoldCanBeBroken"/>): both recoil
        /// and are pushed apart. On this event the "attacker" is the one who broke out and the technique is their grab.
        /// </summary>
        Broken,
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
            int knockbackCm = 0, bool justDodged = false, int sealFrames = 0, GrabPhase grab = GrabPhase.None,
            bool knockedDown = false, bool guarded = false, int crippleFrames = 0)
        {
            Guarded = guarded;
            CrippleFrames = crippleFrames;
            KnockedDown = knockedDown;
            Grab = grab;
            SealFrames = sealFrames;
            JustDodged = justDodged;
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

        /// <summary>
        /// True when the defender just-dodged the strike: it reached them during a 轻功 dash's invincible frames (a dash
        /// started from a normal state, not out of hitstun). No damage; the defender gained just_dodge_qi.
        /// </summary>
        public bool JustDodged { get; }

        /// <summary>Frames the defender's 外功 is sealed by this 点穴 hit (0 if it seals nothing or didn't land).</summary>
        public int SealFrames { get; }

        /// <summary>
        /// 擒拿: <see cref="GrabPhase.Caught"/> when a grab caught the defender (no damage yet; the hold starts),
        /// <see cref="GrabPhase.Thrown"/> when the throw landed. <see cref="GrabPhase.None"/> for every other strike.
        /// </summary>
        public GrabPhase Grab { get; }

        /// <summary>True when this hit (or throw) knocked the defender down.</summary>
        public bool KnockedDown { get; }

        /// <summary>
        /// True when a 硬气功 guard blocked the strike: no hitstun, knockback or debuff. Light and medium deal nothing; heavy and
        /// energy deal block_heavy_damage_pct (in <see cref="Damage"/>). The guard paid block_stamina_* for it.
        /// </summary>
        public bool Guarded { get; }

        /// <summary>Frames this 无相劫指 hit locked the defender's movement for (0 if it didn't: both hits must land).</summary>
        public int CrippleFrames { get; }

        /// <summary>True when the strike dealt its damage and hitstun (a grab's catch doesn't; its throw does).</summary>
        public bool Landed => Grab != GrabPhase.Caught && Grab != GrabPhase.Released && Grab != GrabPhase.Broken && !JustDodged && !Guarded
            && Parry == ParryOutcome.NotParried && (ClashEffect == null || ClashEffect.StrikerHitLands);

        public override string ToString() =>
            Grab == GrabPhase.Caught ? $"t{Tick} {Attacker} grabs {Defender} with {Technique.Id}"
            : Grab == GrabPhase.Released ? $"t{Tick} {Defender} slips free of {Attacker}'s {Technique.Id}"
            : Grab == GrabPhase.Broken ? $"t{Tick} {Attacker} breaks out of the grab with {Technique.Id}"
            : Guarded ? $"t{Tick} {Defender} guards {Attacker}'s {Technique.Id}: {Damage} damage"
            : JustDodged ? $"t{Tick} {Defender} just-dodged {Attacker}'s {Technique.Id}"
            : Parry != ParryOutcome.NotParried ? $"t{Tick} {Attacker}'s {Technique.Id} is {Parry}"
            : Clash != ClashOutcome.NoClash && !Landed ? $"t{Tick} {Attacker}'s {Technique.Id} clashes: {Clash}"
            : $"t{Tick} {Attacker} hits with {Technique.Id}: {Damage} damage, {HitstunFrames}f hitstun, {DefenderHpAfter} HP left";
    }

    /// <summary>
    /// Two fighters on a square floor (see <see cref="FloorPoint"/>), advanced together one tick
    /// at a time. Each tick:
    /// <list type="number">
    /// <item>Both fighters advance a frame and act on their slot presses.</item>
    /// <item>Both aim (auto lock-on): a fighter not in a move faces the other; an attack started this tick
    /// from free takes its aim at where the other stands now, and its whole string keeps that aim.</item>
    /// <item>Both move: walking, then knockback (see below), then min_distance and the arena edges.</item>
    /// <item>Each fighter in the active frames of a move that hasn't hit yet checks reach and angle:
    /// if the other fighter stands within the technique's reach across the floor (centre to centre)
    /// and inside its hit_angle around the aim, the hit connects. A move hits at most once, on its
    /// first active frame that finds the target; a target who walks into it later in the active frames
    /// still gets hit, and one who stepped out of the angle during the wind-up is missed.</item>
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
    /// Knockback: a landed hit pushes the defender straight away from the attacker by the technique's
    /// knockback, and a same-weight clash pushes both apart by clash_recoil_push. The push is slid over
    /// knockback_frames ticks, starting the tick after the strike, in whole centimetres (larger steps
    /// first); a new push adds to what is left and restarts the slide. Hyper armor holds its ground
    /// (no knockback), and the arena ends stop a push.
    ///
    /// Stepping in: a move with an advance (such as each hit of 罗汉拳) steps its fighter forward along
    /// its aim by that many centimetres, slid over its startup frames from the tick it starts (whole centimetres,
    /// larger steps first), so the step is done as the hit comes out. It stops at min_distance like walking,
    /// and a hit that knocks the fighter out of the move stops it.
    ///
    /// Lunge: a move with a lunge (such as 点穴 封穴) surges its fighter forward along its aim from its first active frame,
    /// slid over the active frames, so the hit connects farther out (see <see cref="LungeStep"/>). It stops at min_distance.
    ///
    /// Quick step: a fighter asked for one (see <see cref="MoveInput.QuickStep"/>) who is free slides
    /// quick_step_distance forward or back, or quick_step_side_distance left or right, over quick_step_frames, relative to the lock-on (see
    /// <see cref="QuickStepStep"/>), then recovers for quick_step_recovery frames. It costs quick_step_stamina.
    /// No invincible frames: a strike that reaches a stepping fighter lands as normal. Its use is getting out of
    /// reach, closing in, or getting off the line of a strike's hit angle before it comes out. It stops at
    /// min_distance like walking.
    ///
    /// 轻功 dash: a technique with a dash (see <see cref="Technique.DashCm"/>) slides its fighter that far over its
    /// startup and active frames (whole centimetres, larger steps first), the way the stick was held when it started,
    /// relative to the lock-on each tick (so a sideways dash curves round the other fighter), or straight at them
    /// when no direction was held. It stops at min_distance like walking, so a dash in closes to touching range.
    /// For its invincible frames (<see cref="Technique.InvincibleFrames"/>, from frame 0) strikes pass through:
    /// the strike is spent (it can't hit later in its active frames). From a dash started in a normal state that is a
    /// <b>just dodge</b>: a <see cref="HitEvent"/> with <see cref="HitEvent.JustDodged"/>, no damage, and just_dodge_qi Qi for
    /// the dodger. A dash started out of hitstun (a combo escape) still passes through strikes but never earns a just dodge,
    /// so mashing the dash while being hit gives nothing extra. A dashing fighter keeps facing the other fighter.
    /// A strike that reaches it after the invincible frames lands as normal and stops the dash.
    ///
    /// 点穴 seal: a landed hit of a technique with a seal (see <see cref="Technique.SealFrames"/>) stops the defender starting 外功
    /// techniques for that many frames from the hit (see <see cref="Fighter.Seal"/>). Parried, dodged and clash-lost strikes seal nothing.
    ///
    /// 擒拿 grab (a technique with <see cref="Technique.HoldFrames"/>): it connects like a strike (reach and angle) but can't
    /// catch a fighter in hitstun or already grabbed. When it catches, a parry doesn't stop it, it can't be clashed and it goes
    /// through hyper armor: a <see cref="HitEvent"/> with <see cref="GrabPhase.Caught"/> and no damage, the defender is
    /// <see cref="FighterState.Grabbed"/> and the grabber holds (its move paused) for hold frames. Any knockback still sliding
    /// on the defender stops. On the tick the hold runs out the throw lands: damage, hitstun and knockback like a normal hit,
    /// qi_per_hit for the grabber, in a <see cref="GrabPhase.Thrown"/> event. A 轻功 dash's invincible frames slip a grab
    /// (a just dodge). If a strike and a grab connect on the same tick, the strike wins and the grab is spent; two grabs on the
    /// same tick both come to nothing.
    ///
    /// Knockdown: a landed hit or throw of a technique with <see cref="Technique.Knockdown"/> (the 擒拿 throw) knocks the defender
    /// down instead of putting them in hitstun (hyper armor still holds). A knocked-down fighter can only be hit by a move with
    /// <see cref="Technique.HitsDown"/> (none yet), which deals its damage and leaves them down; every other strike passes over
    /// them without being spent. Grabs can't catch them. Getting up (see <see cref="Fighter"/>): a quick rise or a roll away,
    /// both invincible (a strike passing through is spent, but it's no just dodge). The roll slides roll_distance over
    /// roll_frames, the way the stick was held relative to the lock-on (see <see cref="RollStep"/>).
    ///
    /// 炼气 heal: see <see cref="Fighter"/>. A heal has no reach, so it never hits; hitting the healer during its wind-up or
    /// channel knocks it out of the move like any other, and the heal is lost.
    ///
    /// Walking: each tick, a fighter who is idle walks the way their <see cref="MoveInput"/> says,
    /// relative to the other fighter (forward is straight at them, right is round their side), at the
    /// rules sheet's walk_speed scaled by how far the stick is pushed. Speeds in cm per second are
    /// exactly floor units per tick, so the same inputs always give the same positions. An exhausted
    /// fighter walks at exhausted_walk_pct of the speed. A fighter stops at the edges of the square
    /// arena (arena_width each way, centred on 0) and can't come closer than min_distance to the other
    /// or pass through them: whoever moved gives ground along the line between them (split when both
    /// did), so walking round someone up close slides you round them.
    /// </summary>
    public sealed class Duel
    {
        private readonly Fighter[] _fighters;
        private readonly List<HitEvent> _hits = new List<HitEvent>();
        private readonly int[] _pushLeft = new int[2];
        private readonly int[] _pushTicks = new int[2];
        private readonly int[] _advanceLeft = new int[2];
        private readonly int[] _advanceTicks = new int[2];
        private readonly int[] _quickStepLeft = new int[2];
        private readonly int[] _quickStepTicks = new int[2];
        private readonly int[] _dashLeft = new int[2];
        private readonly int[] _dashTicks = new int[2];
        private readonly int[] _lungeLeft = new int[2];
        private readonly int[] _lungeTicks = new int[2];
        private readonly int[] _rollLeft = new int[2];
        private readonly int[] _rollTicks = new int[2];
        private readonly FloorPoint[] _jumpWay = new FloorPoint[2];
        private readonly bool[] _airborne = new bool[2];

        /// <param name="oneQi">Qi fighter One starts with; null for the rules sheet's qi_start. A run passes what it carried over.</param>
        /// <param name="twoQi">Qi fighter Two starts with; null for qi_start.</param>
        public Duel(Rules rules, Loadout one, Loadout two, int onePositionCm, int twoPositionCm, int? oneQi = null, int? twoQi = null)
            : this(rules, one, two, onePositionCm, twoPositionCm, FighterStats.FromRules(rules, oneQi), FighterStats.FromRules(rules, twoQi))
        {
        }

        /// <summary>A duel where each fighter brings its own max HP, stamina and Qi and what it has left (a run's fights).</summary>
        public Duel(Rules rules, Loadout one, Loadout two, int onePositionCm, int twoPositionCm, FighterStats oneStats, FighterStats twoStats)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (oneStats == null) throw new ArgumentNullException(nameof(oneStats));
            if (twoStats == null) throw new ArgumentNullException(nameof(twoStats));
            Rules = rules;
            _fighters = new[]
            {
                new Fighter(one, oneStats.MaxHp, onePositionCm, rules.InputBuffer, StaminaRules.FromRules(rules).WithMax(oneStats.MaxStamina),
                    oneStats.MaxQi, oneStats.Qi, QuickStepRules.FromRules(rules), DownRules.FromRules(rules), oneStats.Hp),
                new Fighter(two, twoStats.MaxHp, twoPositionCm, rules.InputBuffer, StaminaRules.FromRules(rules).WithMax(twoStats.MaxStamina),
                    twoStats.MaxQi, twoStats.Qi, QuickStepRules.FromRules(rules), DownRules.FromRules(rules), twoStats.Hp),
            };
            One.GrabStrikeGap = Two.GrabStrikeGap = rules.GrabStrikeGap;
            One.Facing = Two.Position - One.Position;
            Two.Facing = One.Position - Two.Position;
            if (One.Facing.IsZero) Two.Facing = new FloorPoint(-1, 0);
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

        /// <summary>A duel with the fighters start_distance apart, each with its own <see cref="FighterStats"/>.</summary>
        public static Duel Start(Rules rules, Loadout one, Loadout two, FighterStats oneStats, FighterStats twoStats)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int left = -(rules.StartDistance / 2);
            return new Duel(rules, one, two, left, left + rules.StartDistance, oneStats, twoStats);
        }

        /// <summary>Ticks run so far. The next tick is numbered this.</summary>
        public int Tick { get; private set; }

        public Fighter One => _fighters[0];
        public Fighter Two => _fighters[1];
        public Fighter this[Side side] => _fighters[(int)side];

        /// <summary>Distance between the two fighters across the floor, in whole centimetres (rounded).</summary>
        public int DistanceCm => FloorPoint.DistanceCm(One.Position, Two.Position);

        public bool IsOver => One.IsDown || Two.IsDown;

        /// <summary>Every hit that has landed, in order.</summary>
        public IReadOnlyList<HitEvent> Hits => _hits;

        /// <summary>Run one tick and return the hits that landed on it (empty, one, or two for a trade).</summary>
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two) => Step(one, two, MoveInput.None, MoveInput.None);

        /// <summary>
        /// Run one tick with movement: <paramref name="moveOne"/> and <paramref name="moveTwo"/> say which way
        /// each fighter walks, relative to the lock-on. Returns the hits that landed on this tick.
        /// </summary>
        public IReadOnlyList<HitEvent> Step(SlotInput one, SlotInput two, MoveInput moveOne, MoveInput moveTwo)
        {
            if (IsOver) return Array.Empty<HitEvent>();

            One.Tick(one, moveOne.IsWalking, moveOne.QuickStep, moveOne);
            Two.Tick(two, moveTwo.IsWalking, moveTwo.QuickStep, moveTwo);
            // Held by a grab and pressed a grab of your own, in time: break out (before anything else this tick).
            var broken = new List<HitEvent>(1);
            BreakOut(Side.One, broken);
            BreakOut(Side.Two, broken);
            KeepLock(One, moveOne);
            KeepLock(Two, moveTwo);
            Aim(One, Two);
            Aim(Two, One);
            var stepOne = WalkStep(One, Two, moveOne) + AdvanceStep(Side.One) + LungeStep(Side.One) + DashStep(Side.One) + RollStep(Side.One) + QuickStepStep(Side.One) + PushStep(Side.One);
            var stepTwo = WalkStep(Two, One, moveTwo) + AdvanceStep(Side.Two) + LungeStep(Side.Two) + DashStep(Side.Two) + RollStep(Side.Two) + QuickStepStep(Side.Two) + PushStep(Side.Two);
            Move(stepOne, stepTwo);
            LandJump(Side.One);
            LandJump(Side.Two);

            // Find both hits before applying either, so a same-tick trade doesn't depend on order.
            // Strikes pass through a fighter in invincible frames (a 轻功 dash's start), and are spent.
            bool oneReaches = Reaches(One, Two), twoReaches = Reaches(Two, One);
            bool oneDodged = oneReaches && Two.IsInvincible, twoDodged = twoReaches && One.IsInvincible;
            bool oneHits = oneReaches && !oneDodged, twoHits = twoReaches && !twoDodged;
            Technique? oneMove = One.Technique, twoMove = Two.Technique;
            // A strike beats a grab on the same tick, and two grabs cancel out: the grab is spent and catches nothing.
            bool oneGrabBroken = oneHits && oneMove!.IsGrab && twoHits;
            bool twoGrabBroken = twoHits && twoMove!.IsGrab && oneHits;
            if (oneGrabBroken) { One.MarkHitLanded(); oneHits = false; }
            if (twoGrabBroken) { Two.MarkHitLanded(); twoHits = false; }
            // Two pure energy strikes going off into each other on the same tick meet once: one cancel for both.
            if (oneHits && twoHits && oneMove!.IsEnergy && twoMove!.IsEnergy) { Two.MarkHitLanded(); twoHits = false; }
            var dodges = broken;
            dodges.Capacity = Math.Max(dodges.Capacity, 2);
            if (oneDodged) { One.MarkHitLanded(); JustDodge(Side.One, oneMove!, One.Empowered, Two, dodges); }
            if (twoDodged) { Two.MarkHitLanded(); JustDodge(Side.Two, twoMove!, Two.Empowered, One, dodges); }
            bool oneEmpowered = One.Empowered, twoEmpowered = Two.Empowered;
            bool oneArmored = One.HasArmor, twoArmored = Two.HasArmor;
            if (oneHits) One.MarkHitLanded();
            if (twoHits) Two.MarkHitLanded();

            // Note what each defender was doing before either hit changes it.
            int oneFrame = One.MoveFrame, twoFrame = Two.MoveFrame;
            var landed = dodges;
            HitEvent? oneLand = oneHits ? Land(Side.One, oneMove!, oneEmpowered, Two, twoMove, twoFrame, twoArmored) : null;
            HitEvent? twoLand = twoHits ? Land(Side.Two, twoMove!, twoEmpowered, One, oneMove, oneFrame, oneArmored) : null;
            if (oneLand != null) landed.Add(oneLand);
            if (twoLand != null) landed.Add(twoLand);
            // A hit that lands cuts a hit_recovery move short (点穴 封穴: long recovery only on a miss). After both
            // lands, so a fighter hit on the same tick (out of their move) is left alone.
            if (oneLand != null && oneLand.Landed) One.CutRecoveryOnHit(oneMove!);
            if (twoLand != null && twoLand.Landed) Two.CutRecoveryOnHit(twoMove!);

            // A hold that ran out this tick throws (a multi-strike grab lets go instead).
            if (One.ThrewThisTick) landed.Add(Throw(Side.One));
            if (Two.ThrewThisTick) landed.Add(Throw(Side.Two));
            // 龙爪手: a tap while holding strikes.
            if (One.GrabStrikeThisTick && One.State.IsInMove() && Two.State == FighterState.Grabbed) landed.Add(GrabStrike(Side.One));
            if (Two.GrabStrikeThisTick && Two.State.IsInMove() && One.State == FighterState.Grabbed && !IsOver) landed.Add(GrabStrike(Side.Two));

            // Hit, grabbed or knocked down: a fighter who lost you finds you again (and turns back).
            foreach (var e in landed)
                if (e.Landed || e.Grab == GrabPhase.Caught || e.KnockedDown) this[e.Defender].Relock();

            _hits.AddRange(landed);
            Tick++;
            return landed;
        }

        /// <summary>
        /// True if <paramref name="attacker"/>'s move connects with <paramref name="target"/> this tick (see <see cref="Connects"/>);
        /// a grab also needs a target it can catch: not in hitstun and not already grabbed.
        /// </summary>
        private static bool Reaches(Fighter attacker, Fighter target) =>
            Connects(attacker, target.Position)
            && (target.State != FighterState.KnockedDown || attacker.Technique!.HitsDown)
            && (!attacker.Technique!.IsGrab || CanBeGrabbed(target));

        /// <summary>True if a 擒拿 grab can catch <paramref name="f"/>: anything but hitstun (no grabbing out of a combo) or already held.</summary>
        public static bool CanBeGrabbed(Fighter f) =>
            f.State != FighterState.Hitstun && f.State != FighterState.Grabbed && !f.State.IsGroundState();

        /// <summary>A grab caught <paramref name="defender"/>: hold them. No damage until the throw.</summary>
        private HitEvent Catch(Side attacker, Technique move, Fighter defender, Technique? defenderMove, int defenderFrame)
        {
            var grabber = this[attacker];
            int d = (int)OtherSide(attacker);
            _pushLeft[d] = 0;
            _pushTicks[d] = 0;
            // Held one tick past the hold, so the throw (which puts them in hitstun) finds them still grabbed.
            defender.Stun(FighterState.Grabbed, move.HoldFrames + 1);
            grabber.Hold(move.HoldFrames);
            return new HitEvent(Tick, attacker, move, 0, 0, defender.Hp, defenderMove, defenderFrame, grab: GrabPhase.Caught);
        }

        /// <summary>
        /// The held fighter on <paramref name="side"/> pressed a grab of their own (<see cref="Fighter.GrabBreakWith"/>): if the
        /// grabber's hold can still be broken, both recoil grab_break_recoil frames and are pushed grab_break_push apart, and the
        /// breaker pays their grab's stamina. Too late (龙爪手's last window, or the throw already landing), nothing happens.
        /// </summary>
        private void BreakOut(Side side, List<HitEvent> into)
        {
            var held = this[side];
            var grabber = this[OtherSide(side)];
            var with = held.GrabBreakWith;
            if (with == null || held.State != FighterState.Grabbed || !grabber.HoldCanBeBroken) return;
            var grab = grabber.Technique;
            int frame = grabber.MoveFrame;
            held.LoseStamina(with.StaminaCost);
            held.Stun(FighterState.Recoil, Rules.GrabBreakRecoil);
            grabber.Stun(FighterState.Recoil, Rules.GrabBreakRecoil);
            StartPush(side, Rules.GrabBreakPush);
            StartPush(OtherSide(side), Rules.GrabBreakPush);
            into.Add(new HitEvent(Tick, side, with, 0, 0, grabber.Hp, grab, frame, grab: GrabPhase.Broken, knockbackCm: Rules.GrabBreakPush));
        }

        /// <summary>
        /// 龙爪手: the grabber on <paramref name="attacker"/> tapped while holding: the strike deals its damage (no hitstun; the
        /// held fighter stays held). The last strike knocks down (with knockdown) and pushes them away by its knockback.
        /// It can't be blocked or dodged: the held fighter can only break out before it, in time.
        /// </summary>
        private HitEvent GrabStrike(Side attacker)
        {
            var grabber = this[attacker];
            var defender = this[OtherSide(attacker)];
            var move = grabber.Technique!;
            bool last = grabber.GrabHitsDone >= move.GrabHits;
            bool exhausted = defender.IsExhausted;
            int dealt = defender.DamageTaken(grabber.Boost(move.Damage));
            bool down = last && move.Knockdown && dealt < defender.Hp;
            defender.TakeHit(dealt, 0);
            int hitstun = 0;
            if (!defender.IsDown)
            {
                if (down) defender.KnockDown();
                else if (!last) defender.Stun(FighterState.Grabbed, move.HoldFrames + 1);
                else if (move.HitstunFrames > 0) defender.Stun(FighterState.Hitstun, hitstun = move.HitstunFrames);
                else defender.Release();
            }
            int qi = grabber.GainQi(Rules.QiPerHit);
            int knockback = last ? move.KnockbackCm : 0;
            StartPush(OtherSide(attacker), knockback);
            return new HitEvent(Tick, attacker, move, dealt, hitstun, defender.Hp,
                defenderExhausted: exhausted, attackerQiGained: qi, knockbackCm: knockback, grab: GrabPhase.Strike, knockedDown: down);
        }

        /// <summary>The hold ran out: the throw lands its damage, hitstun and knockback.</summary>
        private HitEvent Throw(Side attacker)
        {
            var grabber = this[attacker];
            var defender = this[OtherSide(attacker)];
            var move = grabber.Technique!;
            if (move.IsMultiGrab)
            {
                // 龙爪手 with no tap in time: the held fighter slips free; the grabber plays out its recovery.
                defender.Release();
                return new HitEvent(Tick, attacker, move, 0, 0, defender.Hp, grab: GrabPhase.Released);
            }
            bool exhausted = defender.IsExhausted;
            int dealt = defender.DamageTaken(grabber.Boost(StrikeDamage(move, false)));
            bool down = move.Knockdown && dealt < defender.Hp;
            int hitstun = down ? 0 : move.HitstunFrames;
            defender.TakeHit(dealt, hitstun);
            if (down) defender.KnockDown();
            int qi = grabber.GainQi(Rules.QiPerHit);
            StartPush(OtherSide(attacker), move.KnockbackCm);
            return new HitEvent(Tick, attacker, move, dealt, hitstun, defender.Hp,
                defenderExhausted: exhausted, attackerQiGained: qi, knockbackCm: move.KnockbackCm, grab: GrabPhase.Thrown,
                knockedDown: down);
        }

        /// <summary>
        /// True if <paramref name="attacker"/>'s move connects this tick with a target standing at
        /// <paramref name="target"/>: in its active frames, not hit yet, the target within reach
        /// (centre to centre) and inside the hit angle around the move's aim.
        /// </summary>
        public static bool Connects(Fighter attacker, FloorPoint target)
        {
            if (attacker.State != FighterState.Active || attacker.HitLanded) return false;
            var t = attacker.Technique!;
            if (t.ReachCm <= 0) return false;
            var offset = target - attacker.Position;
            long reach = (long)t.ReachCm * FloorPoint.UnitsPerCm;
            return offset.LengthSquared <= reach * reach && t.Covers(attacker.Facing, offset);
        }

        /// <summary>The damage <paramref name="move"/> deals, before the exhausted bonus.</summary>
        public static int StrikeDamage(Technique move, bool empowered) =>
            empowered ? (move.Damage * move.EmpowerDamagePct + 50) / 100 : move.Damage;

        private HitEvent Land(Side attacker, Technique move, bool empowered, Fighter defender, Technique? defenderMove,
            int defenderFrame, bool defenderArmored)
        {
            var striker = this[attacker];

            // 擒拿: a grab goes through parries, guards, clashes and hyper armor.
            if (move.IsGrab) return Catch(attacker, move, defender, defenderMove, defenderFrame);

            // 硬气功: a strike from inside the guard's arc is blocked.
            if (defenderMove != null && defenderMove.IsGuarding(defenderFrame)
                && defenderMove.Covers(defender.Facing, striker.Position - defender.Position))
            {
                int blocked = defender.DamageTaken(BlockedDamage(striker, move, empowered));
                defender.TakeHit(blocked, 0);
                defender.LoseStamina(Rules.BlockStamina(move.Weight));
                return new HitEvent(Tick, attacker, move, blocked, 0, defender.Hp, defenderMove, defenderFrame,
                    empowered: empowered, guarded: true);
            }

            if (defenderMove != null)
            {
                var parry = Combat.Parry.Resolve(defenderMove, defenderFrame, move);
                if (parry != ParryOutcome.NotParried)
                {
                    var e = Combat.Parry.Effect(parry, defenderMove, Rules);
                    defender.Stun(FighterState.Recoil, e.DefenderRecoveryFrames);
                    if (e.AttackerStaggerFrames > 0) striker.Stun(FighterState.Stagger, e.AttackerStaggerFrames);
                    defender.GainStamina(e.DefenderStaminaRefund);
                    striker.LoseStamina(e.AttackerStaminaLoss);
                    int parryQi = defender.GainQi(e.DefenderQiGain);
                    // Every block works the same (Chan): a plain 化绵掌 block, like 硬气功, still lets block_heavy_damage_pct of a
                    // heavy or energy strike through. A perfect parry deflects it all.
                    int through = parry == ParryOutcome.Parried ? BlockedDamage(striker, move, empowered) : 0;
                    int blockedDealt = defender.DamageTaken(through);
                    defender.TakeHit(blockedDealt, 0);
                    return new HitEvent(Tick, attacker, move, blockedDealt, 0, defender.Hp, defenderMove, defenderFrame, parry, e,
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
                        int damage = defender.DamageTaken(striker.Boost(StrikeDamage(move, empowered)));
                        defender.TakeHit(damage, 0);
                        defender.LoseStamina(e.Target.StaminaLoss);
                        int frames = Math.Max(move.HitstunFrames, e.Target.StunFrames);
                        if (frames > 0 && !defender.IsDown) defender.Stun(FighterState.Stagger, frames);
                        int breakQi = striker.GainQi(Rules.QiPerHit);
                        StartPush(OtherSide(attacker), move.KnockbackCm);
                        defender.Seal(move.SealFrames);
                        int locked = Cripple(striker, move, defender);
                        return new HitEvent(Tick, attacker, move, damage, frames, defender.Hp, defenderMove, defenderFrame,
                            clash: clash, clashEffect: e, defenderExhausted: tired, attackerQiGained: breakQi, empowered: empowered,
                            knockbackCm: move.KnockbackCm, sealFrames: move.SealFrames, crippleFrames: locked);
                    }
                    defender.LoseStamina(e.Target.StaminaLoss);
                    ApplyClashSide(striker, e.Striker, clash == ClashOutcome.TargetWins ? FighterState.Stagger : FighterState.Recoil);
                    ApplyClashSide(defender, e.Target, clash == ClashOutcome.PerfectClash ? FighterState.Stagger : FighterState.Recoil);
                    int strikerQi = striker.GainQi(e.Striker.QiGain);
                    int targetQi = defender.GainQi(e.Target.QiGain);
                    int recoilPush = clash == ClashOutcome.BothRecoil || clash == ClashOutcome.EnergyCancel ? Rules.ClashRecoilPush : 0;
                    StartPush(attacker, recoilPush);
                    StartPush(OtherSide(attacker), recoilPush);
                    return new HitEvent(Tick, attacker, move, 0, 0, defender.Hp, defenderMove, defenderFrame,
                        clash: clash, clashEffect: e, attackerQiGained: strikerQi, defenderQiGained: targetQi, empowered: empowered,
                        knockbackCm: recoilPush);
                }
            }

            bool exhausted = defender.IsExhausted;
            int dealt = defender.DamageTaken(striker.Boost(StrikeDamage(move, empowered)));
            // A hit on a fighter lying down (a hits_down move): damage only, they stay down.
            bool wasDown = defender.State == FighterState.KnockedDown;
            // Knockdown: on the floor instead of hitstun (not through hyper armor, and not on the KO hit).
            bool down = !wasDown && move.Knockdown && !defenderArmored && dealt < defender.Hp;
            // Hyper armor: the damage goes through, but the defender's move isn't interrupted.
            int hitstun = defenderArmored || wasDown || down ? 0 : move.HitstunFrames;
            defender.TakeHit(dealt, hitstun);
            if (down) defender.KnockDown();
            int hitQi = striker.GainQi(Rules.QiPerHit);
            // Hyper armor holds its ground.
            int knockback = defenderArmored ? 0 : move.KnockbackCm;
            StartPush(OtherSide(attacker), knockback);
            // 点穴: the defender's 外功 is sealed, hyper armor or not.
            defender.Seal(move.SealFrames);
            int crippled = Cripple(striker, move, defender);
            return new HitEvent(Tick, attacker, move, dealt, hitstun, defender.Hp, defenderMove, defenderFrame,
                defenderExhausted: exhausted, attackerQiGained: hitQi, empowered: empowered, defenderArmored: defenderArmored,
                knockbackCm: knockback, sealFrames: move.SealFrames, knockedDown: down, crippleFrames: crippled);
        }

        /// <summary>
        /// What a blocked strike still deals (before the exhausted bonus), the same for every block (化绵掌 or 硬气功): nothing from
        /// light and medium, block_heavy_damage_pct from heavy and pure energy.
        /// </summary>
        private int BlockedDamage(Fighter striker, Technique move, bool empowered) =>
            move.Weight >= Weight.Heavy ? (striker.Boost(StrikeDamage(move, empowered)) * Rules.BlockHeavyDamagePct + 50) / 100 : 0;

        /// <summary>
        /// A hit of <paramref name="move"/> landed: count it on the striker's string, and if it locks movement (无相劫指) and every
        /// hit of the string so far landed, lock the defender's movement. Returns the frames locked (0 if not).
        /// </summary>
        private static int Cripple(Fighter striker, Technique move, Fighter defender)
        {
            if (striker.Technique == move) striker.CountHitLanded();
            if (move.CrippleFrames <= 0 || striker.Technique != move || !striker.WholeStringLanded || defender.IsDown) return 0;
            defender.Cripple(move.CrippleFrames);
            return move.CrippleFrames;
        }

        /// <summary>A strike passed through <paramref name="dodger"/>'s invincible frames: a just dodge, unless the dash was a combo escape.</summary>
        private void JustDodge(Side attacker, Technique move, bool empowered, Fighter dodger, List<HitEvent> into)
        {
            // A dash out of hitstun, a rise or roll from a knockdown, or a quick step, slips the strike but earns nothing.
            // 纵云梯's leap has invincible frames but no just dodge.
            if (dodger.EscapedHitstun || !dodger.State.IsInMove() || dodger.Technique!.IsJump) return;
            int qi = dodger.GainQi(Rules.JustDodgeQi);
            into.Add(new HitEvent(Tick, attacker, move, 0, 0, dodger.Hp, dodger.Technique, dodger.MoveFrame,
                defenderQiGained: qi, empowered: empowered, justDodged: true));
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

        /// <summary>Centimetres <paramref name="side"/>'s current move still has to step forward.</summary>
        public int AdvanceLeftCm(Side side) => _advanceLeft[(int)side];

        /// <summary>
        /// This tick's share of <paramref name="side"/>'s step forward: a move with advance starts it on its first
        /// frame and slides it over its startup frames (whole centimetres, larger steps first) along its aim.
        /// Being knocked out of the move (hitstun, stagger, recoil) stops the step where it is.
        /// </summary>
        private FloorPoint AdvanceStep(Side side)
        {
            int i = (int)side;
            var f = this[side];
            if (!f.State.IsInMove() || f.IsHolding)
            {
                _advanceLeft[i] = 0;
                return FloorPoint.Zero;
            }
            if (f.MoveFrame == 0)
            {
                _advanceLeft[i] = f.Technique!.AdvanceCm;
                _advanceTicks[i] = f.Technique.StartupFrames;
            }
            if (_advanceLeft[i] <= 0 || _advanceTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_advanceLeft[i] + _advanceTicks[i] - 1) / _advanceTicks[i];
            _advanceLeft[i] -= step;
            _advanceTicks[i]--;
            return f.Facing.WithLength(step * FloorPoint.UnitsPerCm);
        }

        /// <summary>Centimetres <paramref name="side"/>'s lunge still has to slide.</summary>
        public int LungeLeftCm(Side side) => _lungeLeft[(int)side];

        /// <summary>
        /// This tick's share of <paramref name="side"/>'s lunge: a move with a lunge (such as 封穴) surges forward along its aim
        /// from its first active frame, slid over the active frames (whole centimetres, larger steps first). The hit is
        /// checked after the move each tick, so the hitbox goes with the body. Being knocked out of the move stops it.
        /// </summary>
        private FloorPoint LungeStep(Side side)
        {
            int i = (int)side;
            var f = this[side];
            if (!f.State.IsInMove() || f.IsHolding)
            {
                _lungeLeft[i] = 0;
                return FloorPoint.Zero;
            }
            if (f.MoveFrame == f.Technique!.StartupFrames)
            {
                _lungeLeft[i] = f.Technique.LungeCm;
                _lungeTicks[i] = f.Technique.ActiveFrames;
            }
            if (_lungeLeft[i] <= 0 || _lungeTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_lungeLeft[i] + _lungeTicks[i] - 1) / _lungeTicks[i];
            _lungeLeft[i] -= step;
            _lungeTicks[i]--;
            return f.Facing.WithLength(step * FloorPoint.UnitsPerCm);
        }

        /// <summary>Centimetres <paramref name="side"/>'s 轻功 dash still has to slide.</summary>
        public int DashLeftCm(Side side) => _dashLeft[(int)side];

        /// <summary>
        /// This tick's share of <paramref name="side"/>'s 轻功 dash: its dash distance slid over its startup and active
        /// frames from the tick it starts (whole centimetres, larger steps first), the way it was aimed relative to where
        /// the other fighter is now. Being knocked out of the move stops it where it is.
        /// </summary>
        private FloorPoint DashStep(Side side)
        {
            int i = (int)side;
            var f = this[side];
            if (!f.IsDashing)
            {
                _dashLeft[i] = 0;
                return FloorPoint.Zero;
            }
            if (f.MoveFrame == 0)
            {
                _dashLeft[i] = f.Technique!.DashCm;
                _dashTicks[i] = f.Technique.DashFrames;
            }
            if (_dashLeft[i] <= 0 || _dashTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_dashLeft[i] + _dashTicks[i] - 1) / _dashTicks[i];
            _dashLeft[i] -= step;
            _dashTicks[i]--;

            var toward = Forward(f, this[OtherSide(side)]);
            double len = toward.Length;
            double fx = toward.X / len, fz = toward.Z / len; // forward
            double rx = -fz, rz = fx;                          // right
            double wx = f.DashForward * fx + f.DashRight * rx, wz = f.DashForward * fz + f.DashRight * rz;
            // A jump (纵云梯) keeps the way it took off on: it flies straight over and past the other fighter.
            if (f.Technique!.IsJump)
            {
                if (f.MoveFrame == 0) _jumpWay[i] = new FloorPoint(FloorPoint.Round(wx * 1000), FloorPoint.Round(wz * 1000));
                return _jumpWay[i].IsZero ? FloorPoint.Zero : _jumpWay[i].WithLength(step * FloorPoint.UnitsPerCm);
            }
            double wlen = Math.Sqrt(wx * wx + wz * wz);
            if (wlen == 0) return FloorPoint.Zero;
            double units = step * (double)FloorPoint.UnitsPerCm / wlen;
            return new FloorPoint(FloorPoint.Round(wx * units), FloorPoint.Round(wz * units));
        }

        /// <summary>
        /// After moving: a fighter whose 纵云梯 leap just ended (landed, or knocked out of it) on top of the other fighter is set
        /// down min_distance away from them, on its own side of them, so nobody is left standing inside anybody.
        /// </summary>
        private void LandJump(Side side)
        {
            int i = (int)side;
            var f = this[side];
            bool air = f.IsAirborne;
            if (_airborne[i] && !air)
            {
                var other = this[OtherSide(side)];
                // Landed behind a fighter who can lose you (纵云梯 over their head): they lose the lock-on.
                if (other.CanLoseLock && !other.IsDown && FloorPoint.Dot(other.Facing, f.Position - other.Position) < 0) other.LoseLock();
                var apart = f.Position - other.Position;
                long min = (long)Rules.MinDistance * FloorPoint.UnitsPerCm;
                if (apart.LengthSquared < min * min)
                {
                    if (apart.IsZero) apart = _jumpWay[i].IsZero ? f.Facing : _jumpWay[i];
                    f.Position = ClampToArena(other.Position + apart.WithLength(min));
                }
            }
            _airborne[i] = air;
        }

        /// <summary>Centimetres <paramref name="side"/>'s roll away still has to slide.</summary>
        public int RollLeftCm(Side side) => _rollLeft[(int)side];

        /// <summary>
        /// This tick's share of <paramref name="side"/>'s roll away from a knockdown: roll_distance slid over roll_frames from the tick
        /// it starts (whole centimetres, larger steps first), the way the stick was held relative to where the other fighter is now.
        /// </summary>
        private FloorPoint RollStep(Side side)
        {
            int i = (int)side;
            var f = this[side];
            if (f.State != FighterState.Rolling)
            {
                _rollLeft[i] = 0;
                return FloorPoint.Zero;
            }
            if (f.GetUpFrame == 0)
            {
                _rollLeft[i] = f.DownRules!.RollDistanceCm;
                _rollTicks[i] = f.DownRules.RollFrames;
            }
            if (_rollLeft[i] <= 0 || _rollTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_rollLeft[i] + _rollTicks[i] - 1) / _rollTicks[i];
            _rollLeft[i] -= step;
            _rollTicks[i]--;

            var toward = Forward(f, this[OtherSide(side)]);
            double len = toward.Length;
            double fx = toward.X / len, fz = toward.Z / len; // forward
            double rx = -fz, rz = fx;                          // right
            double wx = f.RollForward * fx + f.RollRight * rx, wz = f.RollForward * fz + f.RollRight * rz;
            double wlen = Math.Sqrt(wx * wx + wz * wz);
            if (wlen == 0) return FloorPoint.Zero;
            double units = step * (double)FloorPoint.UnitsPerCm / wlen;
            return new FloorPoint(FloorPoint.Round(wx * units), FloorPoint.Round(wz * units));
        }

        /// <summary>Centimetres <paramref name="side"/>'s quick step still has to slide.</summary>
        public int QuickStepLeftCm(Side side) => _quickStepLeft[(int)side];

        /// <summary>
        /// This tick's share of <paramref name="side"/>'s quick step: quick_step_distance slid over quick_step_frames
        /// from the tick it starts (whole centimetres, larger steps first). Each tick goes the way the step was asked
        /// relative to where the other fighter is now, so a step to the side curves round them like a circling walk.
        /// Being hit stops it where it is.
        /// </summary>
        private FloorPoint QuickStepStep(Side side)
        {
            int i = (int)side;
            var f = this[side];
            if (f.State != FighterState.QuickStep)
            {
                _quickStepLeft[i] = 0;
                return FloorPoint.Zero;
            }
            if (f.QuickStepFrame == 0)
            {
                bool sideways = f.QuickStepDirection == StepDirection.Left || f.QuickStepDirection == StepDirection.Right;
                _quickStepLeft[i] = sideways ? f.QuickStepRules!.SideDistanceCm : f.QuickStepRules!.DistanceCm;
                _quickStepTicks[i] = f.QuickStepRules.MoveFrames;
            }
            if (_quickStepLeft[i] <= 0 || _quickStepTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_quickStepLeft[i] + _quickStepTicks[i] - 1) / _quickStepTicks[i];
            _quickStepLeft[i] -= step;
            _quickStepTicks[i]--;

            var forward = Forward(f, this[OtherSide(side)]);
            FloorPoint way;
            switch (f.QuickStepDirection)
            {
                case StepDirection.Forward: way = forward; break;
                case StepDirection.Back: way = -forward; break;
                case StepDirection.Right: way = forward.RightTurn; break;
                case StepDirection.Left: way = -forward.RightTurn; break;
                default: return FloorPoint.Zero;
            }
            return way.WithLength(step * FloorPoint.UnitsPerCm);
        }

        /// <summary>This tick's share of <paramref name="side"/>'s push (whole centimetres, larger steps first), away from the other fighter.</summary>
        private FloorPoint PushStep(Side side)
        {
            int i = (int)side;
            if (_pushLeft[i] <= 0 || _pushTicks[i] <= 0) return FloorPoint.Zero;
            int step = (_pushLeft[i] + _pushTicks[i] - 1) / _pushTicks[i];
            _pushLeft[i] -= step;
            _pushTicks[i]--;

            var away = this[side].Position - this[OtherSide(side)].Position;
            // If they stand on the same spot, One goes toward -X and Two toward +X.
            if (away.IsZero) away = new FloorPoint(side == Side.One ? -1 : 1, 0);
            return away.WithLength(step * FloorPoint.UnitsPerCm);
        }

        private static void ApplyClashSide(Fighter f, ClashSide side, FighterState state)
        {
            if (side.MoveCancelled && side.StunFrames > 0) f.Stun(state, side.StunFrames);
        }

        /// <summary>
        /// Lock-on: a fighter who isn't in a move faces the other fighter. An attack takes its aim on the
        /// tick it starts from free, and the whole string keeps that aim: follow-up hits don't turn to
        /// track a target who stepped aside. Turning resumes once the fighter is free (or stunned) again.
        /// </summary>
        private void Aim(Fighter me, Fighter other)
        {
            // Lost you (a 纵云梯 over their head): no turning until they find you again. While you are in the air over a fighter
            // who can lose you, they don't track you either.
            // (Landing is settled after the move, so the tick they land counts as still in the air here.)
            bool otherInAir = other.IsAirborne || _airborne[other == One ? 0 : 1];
            if (!me.IsLockedOn || me.CanLoseLock && otherInAir) return;
            // A dash keeps the lock-on: it has no strike to aim.
            if (me.State.IsInMove() && !me.IsDashing && (me.MoveFrame > 0 || me.StringHit > 1)) return;
            var target = other.Position - me.Position;
            if (me.IsTurning && !target.IsZero)
            {
                // Found you again: turn round at relock_turn_frames per half turn, not all at once.
                double have = Math.Atan2(me.Facing.Z, me.Facing.X), want = Math.Atan2(target.Z, target.X);
                double diff = want - have;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff < -Math.PI) diff += 2 * Math.PI;
                double step = Math.PI / Math.Max(1, Rules.RelockTurnFrames);
                if (Math.Abs(diff) > step)
                {
                    double a = have + Math.Sign(diff) * step;
                    me.Facing = new FloorPoint(FloorPoint.Round(Math.Cos(a) * 100000), FloorPoint.Round(Math.Sin(a) * 100000));
                    return;
                }
                me.FinishTurning();
            }
            me.Facing = target;
        }

        /// <summary>
        /// A fighter who lost the lock-on finds you again once they have stood still (idle, no walking or quick step) for their
        /// relock delay: at once with a delay of 0, otherwise after that many frames.
        /// </summary>
        private static void KeepLock(Fighter f, MoveInput input)
        {
            if (f.IsLockedOn) return;
            bool still = f.State == FighterState.Idle && !input.IsWalking && input.QuickStep == StepDirection.None;
            f.CountStill(still);
        }

        /// <summary>Which way is "forward" for <paramref name="f"/>: at the other fighter, or the way they face while they've lost you or are turning back.</summary>
        private static FloorPoint Forward(Fighter f, Fighter other)
        {
            if (!f.IsLockedOn || f.IsTurning) return f.Facing;
            var toward = other.Position - f.Position;
            return toward.IsZero ? f.Facing : toward;
        }

        /// <summary>The floor units <paramref name="f"/> walks this tick (zero unless idle and told to walk).</summary>
        private FloorPoint WalkStep(Fighter f, Fighter other, MoveInput input)
        {
            if (!input.IsWalking || !f.CanAct) return FloorPoint.Zero;
            // Speeds are cm per second, which is exactly floor units per tick.
            double speed = f.IsExhausted ? Rules.WalkSpeed * Rules.ExhaustedWalkPct / 100.0 : Rules.WalkSpeed;
            // 无相劫指's movement lock slows walking too.
            if (f.IsCrippled) speed = speed * Rules.CrippleWalkPct / 100.0;
            double tilt = Math.Sqrt((double)input.Forward * input.Forward + (double)input.Right * input.Right);
            double scale = speed * Math.Min(tilt, MoveInput.Full) / MoveInput.Full / tilt;

            var toward = Forward(f, other);
            double len = toward.Length;
            double fx = toward.X / len, fz = toward.Z / len; // forward
            double rx = -fz, rz = fx;                          // right
            return new FloorPoint(
                FloorPoint.Round((input.Forward * fx + input.Right * rx) * scale),
                FloorPoint.Round((input.Forward * fz + input.Right * rz) * scale));
        }

        /// <summary>
        /// Move both fighters by this tick's steps. If that leaves them closer than min_distance (and closer
        /// than they already were), whoever moved gives ground along the line between them, split if both
        /// moved, but never more than they moved this tick; so walking into the other fighter stops you at
        /// min_distance, and walking around them slides you round. The arena edges stop each step first, and again after.
        /// </summary>
        private void Move(FloorPoint stepOne, FloorPoint stepTwo)
        {
            if (stepOne.IsZero && stepTwo.IsZero) return;
            var a0 = One.Position;
            var b0 = Two.Position;
            // The arena edges stop the steps first, so a fighter pinned at the edge can't be walked into (a long 虎形拳 leap).
            var a = ClampToArena(a0 + stepOne);
            var b = ClampToArena(b0 + stepTwo);

            double before = (a0 - b0).Length;
            double after = (a - b).Length;
            double floor = Math.Min(Rules.MinDistance * (double)FloorPoint.UnitsPerCm, before);
            // A fighter in the air (纵云梯) passes over the other: no min_distance until it lands.
            bool flying = One.IsAirborne || Two.IsAirborne;
            if (after < floor && !flying)
            {
                double overlap = floor - after;
                double capOne = (a - a0).Length, capTwo = (b - b0).Length;
                double backOne, backTwo;
                if (capOne > 0 && capTwo > 0)
                {
                    backOne = Math.Min(capOne, overlap / 2);
                    backTwo = Math.Min(capTwo, overlap - backOne);
                    backOne = Math.Min(capOne, overlap - backTwo);
                }
                else
                {
                    backOne = Math.Min(capOne, overlap);
                    backTwo = Math.Min(capTwo, overlap);
                }
                var apart = a - b;
                if (apart.IsZero) apart = a0 - b0;
                if (apart.IsZero) apart = new FloorPoint(-1, 0);
                a = ClampToArena(a + apart.WithLength(backOne));
                b = ClampToArena(b - apart.WithLength(backTwo));
                // A fighter at the arena edge can't give its share: whoever else moved gives the rest (still no more than they moved).
                double gap = floor - (a - b).Length;
                if (gap > 0.5)
                {
                    a = ClampToArena(a + apart.WithLength(Math.Min(gap, Math.Max(0, capOne - backOne))));
                    gap = floor - (a - b).Length;
                    if (gap > 0.5) b = ClampToArena(b - apart.WithLength(Math.Min(gap, Math.Max(0, capTwo - backTwo))));
                }
                // Still too close (both pressed in, one against the edge): cancel a step, or both, so nobody ever ends up inside the other.
                if (floor - (a - b).Length > 0.5)
                {
                    if ((a0 - b).Length >= floor - 0.5) a = a0;
                    else if ((a - b0).Length >= floor - 0.5) b = b0;
                    else { a = a0; b = b0; }
                }
            }

            One.Position = a;
            Two.Position = b;
        }

        private FloorPoint ClampToArena(FloorPoint p)
        {
            int half = Rules.ArenaWidth * FloorPoint.UnitsPerCm / 2;
            return new FloorPoint(Clamp(p.X, -half, half), Clamp(p.Z, -half, half));
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
