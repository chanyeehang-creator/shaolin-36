namespace Shaolin36.Core.Data
{
    /// <summary>
    /// One row of the technique sheet. Frames are sim ticks (see <see cref="Sim"/>).
    ///
    /// Timeline of a technique, frame index starting at 0:
    /// <code>
    ///   [ startup ........ ][ active ][ recovery ...... ]
    ///          [ clash window ]   ← the last ClashWindowFrames frames of startup
    ///          [perfect]          ← the first PerfectClashFrames of that window
    /// </code>
    /// For a 化劲 parry the active frames are the parry window: EarlyBlockFrames of plain block first,
    /// then PerfectParryFrames of perfect parry, then plain block to the end of the window.
    ///
    /// Empowered version (optional, <see cref="EmpowerQi"/> above 0): if the fighter has at least
    /// EmpowerQi Qi when the technique starts, it spends that Qi and the move is empowered: its hit
    /// deals <see cref="EmpowerDamagePct"/> of its damage and, with <see cref="EmpowerArmor"/>, it has
    /// hyper armor through its startup and active frames.
    /// </summary>
    public sealed class Technique
    {
        public Technique(
            string id,
            string name,
            Category category,
            Weight weight,
            bool isWeapon,
            int startupFrames,
            int activeFrames,
            int recoveryFrames,
            int clashWindowFrames,
            int perfectClashFrames,
            int staminaCost,
            int qiCost,
            int damage,
            bool clashable,
            int hitstunFrames = 0,
            int reachCm = 0,
            string next = "",
            int chainWindowFrames = 0,
            int perfectParryFrames = 0,
            int empowerQi = 0,
            int empowerDamagePct = 100,
            bool empowerArmor = false,
            int knockbackCm = 0,
            int hitAngle = FullCircle,
            int advanceCm = 0,
            int dashCm = 0,
            int invincibleFrames = 0,
            bool canEscape = false,
            int sealFrames = 0,
            int heal = 0,
            int lungeCm = 0,
            int holdFrames = 0,
            bool knockdown = false,
            bool hitsDown = false,
            int earlyBlockFrames = 0,
            int hitRecoveryFrames = 0,
            bool jump = false,
            int crippleFrames = 0,
            bool autoChain = false,
            bool guard = false,
            int grabHits = 0,
            int buffFrames = 0,
            int buffDamagePct = 100)
        {
            Jump = jump;
            CrippleFrames = crippleFrames;
            AutoChain = autoChain;
            Guard = guard;
            GrabHits = grabHits;
            BuffFrames = buffFrames;
            BuffDamagePct = buffDamagePct;
            EarlyBlockFrames = earlyBlockFrames;
            HitRecoveryFrames = hitRecoveryFrames;
            Knockdown = knockdown;
            HitsDown = hitsDown;
            HoldFrames = holdFrames;
            LungeCm = lungeCm;
            SealFrames = sealFrames;
            Heal = heal;
            CanEscape = canEscape;
            DashCm = dashCm;
            InvincibleFrames = invincibleFrames;
            KnockbackCm = knockbackCm;
            AdvanceCm = advanceCm;
            HitAngle = hitAngle;
            // Worked out once from whole degrees and rounded to 1/1,000,000, so the arc is the same on every machine.
            _cosHalfAngle = System.Math.Round(System.Math.Cos(hitAngle * System.Math.PI / 360.0), 6);
            EmpowerQi = empowerQi;
            EmpowerDamagePct = empowerDamagePct;
            EmpowerArmor = empowerArmor;
            Id = id;
            Name = name;
            Category = category;
            Weight = weight;
            IsWeapon = isWeapon;
            StartupFrames = startupFrames;
            ActiveFrames = activeFrames;
            RecoveryFrames = recoveryFrames;
            ClashWindowFrames = clashWindowFrames;
            PerfectClashFrames = perfectClashFrames;
            StaminaCost = staminaCost;
            QiCost = qiCost;
            Damage = damage;
            Clashable = clashable;
            HitstunFrames = hitstunFrames;
            ReachCm = reachCm;
            Next = next;
            ChainWindowFrames = chainWindowFrames;
            PerfectParryFrames = perfectParryFrames;
        }

        public string Id { get; }
        public string Name { get; }
        public Category Category { get; }
        public Weight Weight { get; }
        public bool IsWeapon { get; }
        public int StartupFrames { get; }
        public int ActiveFrames { get; }
        public int RecoveryFrames { get; }
        public int ClashWindowFrames { get; }
        public int PerfectClashFrames { get; }
        public int StaminaCost { get; }
        public int QiCost { get; }
        public int Damage { get; }

        /// <summary>False for 擒拿 grabs, unblockable moves and 点穴 Qi strikes.</summary>
        public bool Clashable { get; }

        /// <summary>How long the defender is frozen after this hit lands, counted from the hit frame.</summary>
        public int HitstunFrames { get; }

        /// <summary>How far the hit reaches, in whole centimetres.</summary>
        public int ReachCm { get; }

        /// <summary>Id of the next hit in the string, or empty if this hit ends it.</summary>
        public string Next { get; }

        /// <summary>The first N recovery frames in which pressing again starts <see cref="Next"/>.</summary>
        public int ChainWindowFrames { get; }

        /// <summary>化劲 only: N active frames that count as a perfect parry, right after the <see cref="EarlyBlockFrames"/>.</summary>
        public int PerfectParryFrames { get; }

        /// <summary>
        /// 化劲 only: the first N active frames are a plain block, before the perfect parry starts. Leeway for a parry
        /// pressed a little early: it still blocks. 0 = the window opens straight into the perfect parry.
        /// </summary>
        public int EarlyBlockFrames { get; }

        /// <summary>
        /// If above 0: once this move's hit lands (damage, not blocked, parried or dodged), the attacker can act again
        /// this many frames later, cutting the rest of the move short. A miss keeps the full recovery.
        /// 0 = the recovery is the same either way.
        /// </summary>
        public int HitRecoveryFrames { get; }

        /// <summary>Qi needed (and spent) for the empowered version when the move starts; 0 means it has none.</summary>
        public int EmpowerQi { get; }

        /// <summary>Damage of the empowered version as a percentage of <see cref="Damage"/> (200 = double).</summary>
        public int EmpowerDamagePct { get; }

        /// <summary>True if the empowered version has hyper armor: hits still deal damage but don't interrupt it.</summary>
        public bool EmpowerArmor { get; }

        /// <summary>How far a landed hit pushes the defender back, in whole centimetres (slid over knockback_frames).</summary>
        public int KnockbackCm { get; }

        /// <summary>
        /// How far the attacker steps forward during the move, in whole centimetres: along the aim, slid over the
        /// startup frames so the step is done as the hit comes out. The step stops at min_distance like walking.
        /// </summary>
        public int AdvanceCm { get; }

        /// <summary>
        /// 轻功 dash: how far the move carries the fighter, in whole centimetres, slid over its startup and active
        /// frames. The way it goes is the stick held on the tick it starts (relative to the lock-on), or straight at
        /// the other fighter when no direction is held. 0 = not a dash.
        /// </summary>
        public int DashCm { get; }

        /// <summary>Invincible frames: for the first N frames of the move, strikes pass through the fighter. 0 = none.</summary>
        public int InvincibleFrames { get; }

        public bool IsDash => DashCm > 0;

        /// <summary>
        /// True if this move can be started while in hitstun, to get out of a combo (the default 轻功 dash). It is the
        /// only kind of move that can: everything else waits for the hitstun to end.
        /// </summary>
        public bool CanEscape { get; }

        /// <summary>
        /// 点穴: when this hit lands, the defender can't start 外功 (waigong) techniques for this many frames, counted
        /// from the hit frame (0 = no seal). A blocked, parried or dodged hit seals nothing.
        /// </summary>
        public int SealFrames { get; }

        /// <summary>
        /// 炼气: HP restored when the move finishes its active frames (the channel), never above max HP (0 = no heal).
        /// Interrupted before then, it heals nothing and the Qi it cost is gone.
        /// </summary>
        public int Heal { get; }

        public bool IsHeal => Heal > 0;

        /// <summary>
        /// Lunge: how far the attacker's body surges forward along its aim as the hit comes out, in whole centimetres,
        /// slid over the active frames (the hitbox goes with the body, so it connects farther out). Unlike
        /// <see cref="AdvanceCm"/> (a step during the wind-up) it starts on the first active frame: wind-up, then lunge and hit.
        /// Stops at min_distance like walking. 0 = no lunge.
        /// </summary>
        public int LungeCm { get; }

        /// <summary>
        /// 擒拿 grab: how long the attacker holds the defender once the grab catches them, in frames, before the throw
        /// lands its damage, hitstun and knockback. 0 = not a grab. A grab can't be parried or clashed, goes through
        /// hyper armor, and can't catch a fighter in hitstun (no grabbing out of a combo). While holding, the attacker's
        /// move is paused on the frame it caught, so the hold adds to the move's length.
        /// </summary>
        public int HoldFrames { get; }

        public bool IsGrab => HoldFrames > 0;

        /// <summary>
        /// True if a landed hit (or a grab's throw) knocks the defender down (<see cref="Fighters.FighterState.KnockedDown"/>)
        /// instead of putting them in hitstun. Hyper armor still holds its ground.
        /// </summary>
        public bool Knockdown { get; }

        /// <summary>
        /// True if this strike can hit a fighter lying knocked down (damage only: they stay down). False for every move so
        /// far: a downed fighter can't be hit.
        /// </summary>
        public bool HitsDown { get; }

        /// <summary>
        /// 轻功 jump (纵云梯): a dash that leaps. While it slides (its startup and active frames) the fighter is in the air:
        /// it passes over the other fighter (no min_distance) and so can land behind them. Its way is fixed on the floor
        /// when it starts (it doesn't curve round the lock-on). Its invincible frames never give a just dodge.
        /// </summary>
        public bool Jump { get; }

        public bool IsJump => Jump && DashCm > 0;

        /// <summary>
        /// 点穴 (无相劫指): when this hit lands and every earlier hit of its string landed too, the defender can't quick step or
        /// use 轻功 for this many frames, and walks at cripple_walk_pct speed. 0 = none.
        /// </summary>
        public int CrippleFrames { get; }

        /// <summary>
        /// True: once this hit's active frames end, the next hit of the string starts by itself on the first chain frame,
        /// with no press (无相劫指's double strike). Needs <see cref="Next"/>.
        /// </summary>
        public bool AutoChain { get; }

        /// <summary>
        /// 防身 guard (硬气功): press and hold. It guards from its first frame (no wind-up); while its slot is held it stays on
        /// its last active frame, and once let go it plays its recovery. Strikes from inside its <see cref="HitAngle"/>
        /// (around where it was aimed when it started) are blocked: light and medium deal nothing, heavy and energy deal
        /// block_heavy_damage_pct, and no 点穴 debuff or knockdown gets through. Each block costs block_stamina_* stamina;
        /// at 0 stamina the guard drops. It can't be started while exhausted, and it never parries. Grabs go through it.
        /// </summary>
        public bool Guard { get; }

        public bool IsGuard => Guard && Category == Category.Fangshen;

        /// <summary>True on the frames this guard blocks: from frame 0 to the end of its active frames.</summary>
        public bool IsGuarding(int frame) => IsGuard && frame >= 0 && frame < StartupFrames + ActiveFrames;

        /// <summary>
        /// 擒拿 (龙爪手): once the grab catches, each press of its slot during the hold strikes the held fighter (its damage,
        /// no hitstun) and restarts the hold, up to this many strikes; the last one knocks down (with knockdown) and ends
        /// the hold. If the hold runs out first, the held fighter breaks free. 0 = an ordinary grab with one throw.
        /// </summary>
        public int GrabHits { get; }

        public bool IsMultiGrab => IsGrab && GrabHits > 0;

        /// <summary>炼气 (易筋经): when the channel finishes, every hit the fighter deals does <see cref="BuffDamagePct"/> damage for this many frames.</summary>
        public int BuffFrames { get; }

        /// <summary>Damage of the fighter's hits while the buff runs, as a percentage (150 = 1.5x).</summary>
        public int BuffDamagePct { get; }

        public bool IsBuff => BuffFrames > 0;

        /// <summary>Pure energy (狮吼功): see <see cref="Weight.Energy"/>.</summary>
        public bool IsEnergy => Weight == Weight.Energy;

        /// <summary>The farthest this strike can connect when started: its reach plus any step-in and lunge.</summary>
        public int FullReachCm => ReachCm + AdvanceCm + LungeCm;

        /// <summary>Frames a dash slides over: its startup and active frames.</summary>
        public int DashFrames => StartupFrames + ActiveFrames;

        public bool IsInvincible(int frame) => frame >= 0 && frame < InvincibleFrames;

        /// <summary>A hit angle this wide covers every direction: no angle check.</summary>
        public const int FullCircle = 360;

        private readonly double _cosHalfAngle;

        /// <summary>
        /// How wide the hit is, in degrees, centred on where the move is aimed (90 = 45 degrees to either side).
        /// A target outside it is missed even inside the reach, so a step to the side can slip a strike.
        /// <see cref="FullCircle"/> (360) hits all round.
        /// </summary>
        public int HitAngle { get; }

        /// <summary>
        /// True if a target at <paramref name="offset"/> from the attacker is inside the hit angle of a move
        /// aimed along <paramref name="facing"/>. Reach is checked separately. A target on the very same spot is covered.
        /// </summary>
        public bool Covers(FloorPoint facing, FloorPoint offset)
        {
            if (HitAngle >= FullCircle || offset.IsZero || facing.IsZero) return true;
            double dot = FloorPoint.Dot(facing, offset);
            return dot >= _cosHalfAngle * facing.Length * offset.Length;
        }

        public bool CanEmpower => EmpowerQi > 0;

        public bool HasNext => Next.Length > 0;

        public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

        /// <summary>First frame of the clash window (inclusive).</summary>
        public int ClashWindowStart => StartupFrames - ClashWindowFrames;

        public bool IsInClashWindow(int frame) =>
            Clashable && ClashWindowFrames > 0 && frame >= ClashWindowStart && frame < StartupFrames;

        public bool IsInPerfectClash(int frame) =>
            IsInClashWindow(frame) && frame < ClashWindowStart + PerfectClashFrames;

        public bool IsActive(int frame) => frame >= StartupFrames && frame < StartupFrames + ActiveFrames;

        /// <summary>A parry deflects strikes during its active frames.</summary>
        public bool IsParrying(int frame) => Category == Category.Fangshen && PerfectParryFrames > 0 && IsActive(frame);

        /// <summary>First frame of the perfect parry (inclusive).</summary>
        public int PerfectParryStart => StartupFrames + EarlyBlockFrames;

        public bool IsInPerfectParry(int frame) =>
            IsParrying(frame) && frame >= PerfectParryStart && frame < PerfectParryStart + PerfectParryFrames;

        /// <summary>
        /// How many of the chain window's frames give a true combo into <paramref name="next"/>:
        /// chaining on recovery frame c lands the next hit (ActiveFrames + c + next.StartupFrames)
        /// frames after this one, which must be before this hit's hitstun ends.
        /// 0 means the string never links, so the defender always gets a gap.
        /// </summary>
        public int LinkFramesInto(Technique next)
        {
            int frames = HitstunFrames - ActiveFrames - next.StartupFrames;
            if (frames < 0) frames = 0;
            return frames < ChainWindowFrames ? frames : ChainWindowFrames;
        }

        /// <summary>
        /// For a string starting with this technique: the farthest away the defender can stand when it starts
        /// for every hit to stay in reach, given each hit's step forward (advance or lunge) and each landed hit's knockback (which
        /// has finished sliding by the next hit when knockback_frames is shorter than the gaps). Assumes the
        /// defender stands still on the line of the aim. <paramref name="find"/> looks up the next hits by id.
        /// Hits whose next id is missing end the string.
        /// </summary>
        public int StringReachFrom(System.Func<string, Technique?> find)
        {
            int best = int.MaxValue, closed = 0, guard = 0;
            for (Technique? t = this; t != null && guard < 32; t = t.HasNext ? find(t.Next) : null, guard++)
            {
                closed += t.AdvanceCm + t.LungeCm;
                best = System.Math.Min(best, t.ReachCm + closed);
                closed -= t.KnockbackCm;
            }
            return best;
        }

        public override string ToString() => $"{Id} ({Name})";
    }
}
