using System;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Core.Brains
{
    /// <summary>One tick of what a brain presses: its slot buttons and its stick.</summary>
    public readonly struct BrainInput
    {
        public BrainInput(SlotInput slots, MoveInput move)
        {
            Slots = slots;
            Move = move;
        }

        public static BrainInput None => default;

        public SlotInput Slots { get; }
        public MoveInput Move { get; }

        public override string ToString() => $"press {Slots}, {Move}";
    }

    /// <summary>What a brain is up to, for the playtest screen.</summary>
    public enum BrainIntent
    {
        /// <summary>Switched off, or nothing to do.</summary>
        Still,
        /// <summary>Walking (or quick-stepping) in to get in reach.</summary>
        Closing,
        /// <summary>In reach, waiting for its moment.</summary>
        Waiting,
        /// <summary>In reach, circling round the other fighter.</summary>
        Circling,
        /// <summary>Attacking (or carrying on a string).</summary>
        Attacking,
        /// <summary>Attacking because it saw the other fighter open.</summary>
        Punishing,
        /// <summary>Saw a strike coming and is timing its parry for it.</summary>
        Parrying,
        /// <summary>Threw its parry on a guess that you were about to strike.</summary>
        Guessing,
        /// <summary>Saw a strike coming and quick-stepped to the side.</summary>
        Dodging,
        /// <summary>Low on stamina: backing off to breathe.</summary>
        Resting,
        /// <summary>Caught in a combo: dashed out of the hitstun.</summary>
        Escaping,
        /// <summary>Out of reach and hurt: channelling a 气功 heal.</summary>
        Healing,
    }

    /// <summary>
    /// The opponent brain: it plays a fighter through the same slot buttons and stick a player has, so it
    /// follows every rule the player does (stamina, Qi, frame data, no reading inputs). How it plays is set by a
    /// <see cref="BrainStyle"/> row of the brain sheet.
    ///
    /// It only sees the other fighter's moves <see cref="BrainStyle.Reaction"/> frames late, like a person
    /// reacting. Each tick it:
    /// <list type="number">
    /// <item>carries on its own string when a chain window opens (<see cref="BrainStyle.Chain"/>);</item>
    /// <item>answers a strike it sees coming: a 化劲 parry timed for the strike's hit frame (<see cref="BrainStyle.Parry"/>,
    /// aiming at the perfect part with <see cref="BrainStyle.Perfect"/>), or else a step aside (<see cref="BrainStyle.Step"/>): with a 轻功 dash
    /// on a slot it dashes to the side timed so the strike's hit frame falls in its invincible frames, otherwise it quick-steps;</item>
    /// <item>caught in a combo, once it has been in hitstun for its reaction time, dashes back out of it with its 轻功 dash
    /// (<see cref="BrainStyle.Step"/> chance, once per combo);</item>
    /// <item>hurt by at least a heal's worth, with a 气功 heal it can pay for, and at least <see cref="HealMarginCm"/> beyond the other
    /// fighter's longest reach while they aren't in a move, heals (<see cref="BrainStyle.Heal"/> chance, rolled every think_every frames);</item>
    /// <item>backs off to breathe when its stamina is under <see cref="BrainStyle.RestBelow"/>;</item>
    /// <item>punishes when it sees the other fighter recovering, staggered, recoiling or winding up / channelling a 气功 heal in reach (<see cref="BrainStyle.Punish"/>);</item>
    /// <item>otherwise walks in until its attacks reach (with <see cref="BrainStyle.Step"/> chance per choice it dashes in from far
    /// off, or quick-steps in), then every <see cref="BrainStyle.ThinkEvery"/> frames
    /// attacks (<see cref="BrainStyle.Aggression"/>), parries on a guess (<see cref="BrainStyle.Guess"/>),
    /// circles (<see cref="BrainStyle.Circle"/>) or waits.</item>
    /// </list>
    /// It does nothing for the first opponent_ready ticks of a duel (rules.csv), so a new duel never opens with a hit.
    /// Every chance is rolled on a seeded <see cref="Dice"/>, so the same seed and the same fight give the same
    /// choices. Call <see cref="Think"/> once per sim tick, before the duel steps (not during hit-stop).
    /// </summary>
    public sealed class Brain
    {
        /// <summary>Centimetres short of an attack's full reach (reach plus step-in) where the brain stops walking in.</summary>
        public const int EngageMarginCm = 10;

        /// <summary>A strike counts as coming at the brain if it is this close to reaching (reach plus step-in plus this).</summary>
        public const int ThreatMarginCm = 20;

        /// <summary>While resting, it backs away until it is this far off.</summary>
        public const int RestDistanceCm = 250;

        /// <summary>It only starts a 气功 heal this far beyond the other fighter's longest reach (reach plus step-in).</summary>
        public const int HealMarginCm = 80;

        private struct Seen
        {
            public FighterState State;
            public Technique? Technique;
            public int StartTick; // the tick the move (or the stun) started on
        }

        private readonly Seen[] _history = new Seen[BrainStyle.MaxReaction + 1];
        private int _recorded;
        private readonly Dice _dice;

        private int _decidedAttack = int.MinValue; // start tick of the last strike it chose an answer for
        private int _plannedParryTick = -1;        // tick to press the parry on, or -1
        private int _plannedAgainst = int.MinValue;
        private int _plannedDashTick = -1;         // tick to press the dash on, or -1
        private int _plannedDashRight;             // -100 left or 100 right
        private int _hitstunTicks;                 // ticks in a row it has been in hitstun
        private bool _escapeDecided;               // rolled for a combo escape in this run of hitstun
        private int _decidedOpening = int.MinValue;
        private int _decidedLink = -1;
        private int _choiceTicksLeft;
        private int _healTicksLeft;
        private int _circle; // -1 left, 1 right, 0 none
        private int _lastFoeStateTick;
        private FighterState _lastFoeState = FighterState.Idle;

        public Brain(BrainStyle style, int seed)
        {
            Style = style ?? throw new ArgumentNullException(nameof(style));
            Seed = seed;
            _dice = new Dice(seed);
        }

        public BrainStyle Style { get; }
        public int Seed { get; }

        /// <summary>False makes it stand still and press nothing, for practising on it.</summary>
        public bool Active { get; set; } = true;

        /// <summary>What it is doing right now.</summary>
        public BrainIntent Intent { get; private set; } = BrainIntent.Still;

        /// <summary>This tick's buttons and stick for the fighter on <paramref name="side"/> of <paramref name="duel"/>.</summary>
        public BrainInput Think(Duel duel, Side side)
        {
            if (duel == null) throw new ArgumentNullException(nameof(duel));
            var me = duel[side];
            var foe = duel[side == Side.One ? Side.Two : Side.One];
            Record(foe, duel.Tick);

            if (!Active || duel.IsOver || duel.Tick < duel.Rules.OpponentReady)
            {
                _plannedParryTick = -1;
                Intent = BrainIntent.Still;
                return BrainInput.None;
            }

            var next = me.NextState();
            int distance = duel.DistanceCm;

            // 0. Caught in a combo: once it has noticed (its reaction time), maybe dash back out of it.
            if (me.State == FighterState.Hitstun)
            {
                _hitstunTicks++;
                if (!_escapeDecided && _hitstunTicks >= Math.Max(1, Style.Reaction) && next == FighterState.Hitstun)
                {
                    _escapeDecided = true;
                    var escape = EscapeSlot(me);
                    if (escape != null && me.CanUse(me.Loadout[escape.Value]!) && _dice.Chance(Style.Step))
                        return Dash(escape.Value, MoveInput.WalkBack, BrainIntent.Escaping);
                }
                return BrainInput.None;
            }
            _hitstunTicks = 0;
            _escapeDecided = false;

            // 1. In its own string: carry on when the chain window opens.
            if (next.IsInMove())
            {
                var t = me.Technique!;
                if (next == FighterState.Recovery && t.HasNext && me.Slot != null && me.Loadout.Book != null)
                {
                    int recoveryFrame = me.MoveFrame + 1 - t.StartupFrames - t.ActiveFrames;
                    int link = duel.Tick;
                    if (recoveryFrame == 0 && _decidedLink != link)
                    {
                        _decidedLink = link;
                        var following = me.Loadout.Book.NextOf(t);
                        if (following != null && me.CanUse(following) && _dice.Chance(Style.Chain))
                            return Press(me.Slot.Value, BrainIntent.Attacking);
                    }
                }
                return BrainInput.None;
            }

            // 2. A parry or dash already timed for a strike: wait for its frame, then press.
            if (_plannedDashTick >= 0)
            {
                bool sameStrike = foe.Technique != null && foe.State.IsInMove() && MoveStart(foe, duel.Tick) == _plannedAgainst;
                var dashSlot = DashSlot(me);
                if (!sameStrike || dashSlot == null) _plannedDashTick = -1;
                else if (duel.Tick >= _plannedDashTick)
                {
                    _plannedDashTick = -1;
                    if (next == FighterState.Idle) return Dash(dashSlot.Value, new MoveInput(0, _plannedDashRight), BrainIntent.Dodging);
                }
                else
                {
                    Intent = BrainIntent.Dodging;
                    return BrainInput.None;
                }
            }

            if (_plannedParryTick >= 0)
            {
                bool sameStrike = foe.Technique != null && foe.State.IsInMove() && MoveStart(foe, duel.Tick) == _plannedAgainst;
                if (!sameStrike) _plannedParryTick = -1;
                else if (duel.Tick >= _plannedParryTick)
                {
                    _plannedParryTick = -1;
                    var parrySlot = ParrySlot(me);
                    if (parrySlot != null && next == FighterState.Idle) return Press(parrySlot.Value, BrainIntent.Parrying);
                }
                else
                {
                    Intent = BrainIntent.Parrying;
                    return BrainInput.None;
                }
            }

            if (next != FighterState.Idle) return BrainInput.None;

            var seen = SeenNow();

            // 3. A strike it can see coming: parry it or step out of the way.
            if (seen.Technique != null && seen.State == FighterState.Startup && seen.Technique.ReachCm > 0
                && seen.StartTick != _decidedAttack)
            {
                var t = seen.Technique;
                int hitTick = seen.StartTick + t.StartupFrames;
                bool comingAtMe = distance <= t.ReachCm + t.AdvanceCm + ThreatMarginCm;
                if (comingAtMe && hitTick > duel.Tick)
                {
                    _decidedAttack = seen.StartTick;
                    var parrySlot = ParrySlot(me);
                    if (parrySlot != null && me.CanUse(me.Loadout[parrySlot.Value]!) && _dice.Chance(Style.Parry))
                    {
                        var parry = me.Loadout[parrySlot.Value]!;
                        int aim = AimFrame(parry);
                        int pressTick = hitTick - parry.StartupFrames - aim;
                        if (pressTick <= duel.Tick) return Press(parrySlot.Value, BrainIntent.Parrying);
                        _plannedParryTick = pressTick;
                        _plannedAgainst = seen.StartTick;
                        Intent = BrainIntent.Parrying;
                        return BrainInput.None;
                    }
                    var dodgeSlot = DashSlot(me);
                    if (dodgeSlot != null && me.CanUse(me.Loadout[dodgeSlot.Value]!) && _dice.Chance(Style.Step))
                    {
                        // Dash to the side so the hit frame falls in the middle of the invincible frames.
                        var dash = me.Loadout[dodgeSlot.Value]!;
                        int right = _dice.Chance(50) ? -MoveInput.Full : MoveInput.Full;
                        int pressTick = hitTick - dash.InvincibleFrames / 2;
                        if (pressTick <= duel.Tick) return Dash(dodgeSlot.Value, new MoveInput(0, right), BrainIntent.Dodging);
                        _plannedDashTick = pressTick;
                        _plannedDashRight = right;
                        _plannedAgainst = seen.StartTick;
                        Intent = BrainIntent.Dodging;
                        return BrainInput.None;
                    }
                    if (dodgeSlot == null && me.CanQuickStep && _dice.Chance(Style.Step))
                    {
                        Intent = BrainIntent.Dodging;
                        return new BrainInput(SlotInput.None, MoveInput.Step(_dice.Chance(50) ? StepDirection.Left : StepDirection.Right));
                    }
                }
            }

            // 3b. Hurt, with Qi for a 气功 heal, and well out of the other fighter's reach: maybe heal.
            if (--_healTicksLeft <= 0)
            {
                _healTicksLeft = Style.ThinkEvery;
                var healSlot = HealSlot(me);
                if (healSlot != null)
                {
                    var heal = me.Loadout[healSlot.Value]!;
                    bool hurt = me.MaxHp - me.Hp >= heal.Heal;
                    bool safe = distance >= LongestReach(foe) + HealMarginCm && !foe.State.IsInMove();
                    if (hurt && safe && me.CanUse(heal) && _dice.Chance(Style.Heal)) return Press(healSlot.Value, BrainIntent.Healing);
                }
            }

            // 4. Out of breath: back off and let stamina come back. It still parries (above) if it can pay.
            if (me.IsExhausted || me.UsesStamina && me.Stamina < Style.RestBelow)
            {
                Intent = BrainIntent.Resting;
                _circle = 0;
                return distance < RestDistanceCm ? Walk(MoveInput.WalkBack) : BrainInput.None;
            }

            // 5. The other fighter is open (recovering, staggered or recoiling): punish.
            // A 气功 heal in its wind-up or channel is open too: hit it before the heal lands.
            bool healing = seen.Technique != null && seen.Technique.IsHeal && seen.State != FighterState.Recovery;
            bool open = healing || seen.State == FighterState.Recovery || seen.State.IsStunned() && seen.State != FighterState.Hitstun;
            if (open && seen.StartTick != _decidedOpening)
            {
                var punisher = PickAttack(me, distance);
                if (punisher != null)
                {
                    _decidedOpening = seen.StartTick;
                    if (_dice.Chance(Style.Punish)) return Press(punisher.Value, BrainIntent.Punishing);
                }
            }

            // 6. Neutral: walk in, then attack, circle or wait.
            int engage = EngageDistance(me);
            if (engage > 0 && distance > engage)
            {
                _circle = 0;
                if (--_choiceTicksLeft <= 0)
                {
                    _choiceTicksLeft = Style.ThinkEvery;
                    var dashIn = DashSlot(me);
                    if (dashIn != null && me.CanUse(me.Loadout[dashIn.Value]!)
                        && distance - me.Loadout[dashIn.Value]!.DashCm / 2 >= engage && _dice.Chance(Style.Step))
                        return Dash(dashIn.Value, MoveInput.None, BrainIntent.Closing); // no direction: straight in
                    var steps = me.QuickStepRules;
                    if (steps != null && me.CanQuickStep && distance - steps.DistanceCm >= engage - EngageMarginCm
                        && _dice.Chance(Style.Step))
                    {
                        Intent = BrainIntent.Closing;
                        return new BrainInput(SlotInput.None, MoveInput.Step(StepDirection.Forward));
                    }
                }
                Intent = BrainIntent.Closing;
                return Walk(MoveInput.WalkForward);
            }
            if (Intent == BrainIntent.Closing || Intent == BrainIntent.Still) _choiceTicksLeft = 0;

            if (--_choiceTicksLeft <= 0)
            {
                _choiceTicksLeft = Style.ThinkEvery;
                _circle = 0;
                var attack = PickAttack(me, distance);
                if (attack != null && _dice.Chance(Style.Aggression)) return Press(attack.Value, BrainIntent.Attacking);
                var guessSlot = ParrySlot(me);
                int foeReach = EngageDistance(foe);
                if (guessSlot != null && foeReach > 0 && distance <= foeReach + EngageMarginCm && foe.State == FighterState.Idle
                    && me.CanUse(me.Loadout[guessSlot.Value]!) && _dice.Chance(Style.Guess))
                    return Press(guessSlot.Value, BrainIntent.Guessing);
                if (_dice.Chance(Style.Circle)) _circle = _dice.Chance(50) ? -1 : 1;
            }

            if (_circle != 0)
            {
                Intent = BrainIntent.Circling;
                return Walk(_circle < 0 ? MoveInput.WalkLeft : MoveInput.WalkRight);
            }
            Intent = BrainIntent.Waiting;
            return BrainInput.None;
        }

        private BrainInput Press(Slot slot, BrainIntent intent)
        {
            Intent = intent;
            _circle = 0;
            return new BrainInput(SlotInput.Press(slot), MoveInput.None);
        }

        /// <summary>Press the dash slot with the stick held <paramref name="way"/> (none: straight at the other fighter).</summary>
        private BrainInput Dash(Slot slot, MoveInput way, BrainIntent intent)
        {
            Intent = intent;
            _circle = 0;
            return new BrainInput(SlotInput.Press(slot), way);
        }

        /// <summary>The first slot holding a move that can break out of hitstun, or null.</summary>
        private static Slot? EscapeSlot(Fighter me)
        {
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (t != null && t.CanEscape) return s;
            }
            return null;
        }

        /// <summary>The first slot holding a 气功 heal, or null.</summary>
        private static Slot? HealSlot(Fighter me)
        {
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (t != null && t.IsHeal) return s;
            }
            return null;
        }

        /// <summary>The farthest any of <paramref name="f"/>'s attacks reaches (reach plus step-in), or 0 with none.</summary>
        public static int LongestReach(Fighter f)
        {
            int best = 0;
            foreach (var s in Slots.All)
            {
                var t = f.Loadout[s];
                if (IsAttack(t)) best = Math.Max(best, t!.ReachCm + t.AdvanceCm);
            }
            return best;
        }

        /// <summary>The first slot holding a 轻功 dash, or null.</summary>
        private static Slot? DashSlot(Fighter me)
        {
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (t != null && t.IsDash) return s;
            }
            return null;
        }

        private static BrainInput Walk(MoveInput move) => new BrainInput(SlotInput.None, move);

        /// <summary>The tick the fighter's current move started on, as seen before the tick numbered <paramref name="tick"/> runs.</summary>
        private static int MoveStart(Fighter f, int tick) => tick - 1 - f.MoveFrame;

        private void Record(Fighter foe, int tick)
        {
            if (foe.State != _lastFoeState)
            {
                _lastFoeState = foe.State;
                _lastFoeStateTick = tick - 1;
            }
            var seen = new Seen
            {
                State = foe.State,
                Technique = foe.State.IsInMove() ? foe.Technique : null,
                StartTick = foe.State.IsInMove() ? MoveStart(foe, tick) : _lastFoeStateTick,
            };
            // A chained hit has a new start tick even if the state stays the same.
            _history[_recorded % _history.Length] = seen;
            _recorded++;
        }

        /// <summary>The other fighter as the brain sees them now: <see cref="BrainStyle.Reaction"/> ticks ago.</summary>
        private Seen SeenNow()
        {
            int back = Math.Min(Style.Reaction, _recorded - 1);
            return _history[(_recorded - 1 - back) % _history.Length];
        }

        /// <summary>Which frame of the parry's window to meet the strike on: in the perfect part, or in the plain block part.</summary>
        private int AimFrame(Technique parry)
        {
            int perfect = Math.Max(0, Math.Min(parry.PerfectParryFrames, parry.ActiveFrames));
            int plain = parry.ActiveFrames - perfect;
            if (perfect > 0 && (plain == 0 || _dice.Chance(Style.Perfect))) return _dice.Below(perfect);
            return perfect + _dice.Below(Math.Min(plain, 6));
        }

        /// <summary>The first slot holding a 化劲 parry, or null.</summary>
        private static Slot? ParrySlot(Fighter me)
        {
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (t != null && t.Category == Category.Huajin && t.PerfectParryFrames > 0) return s;
            }
            return null;
        }

        private static bool IsAttack(Technique? t) => t != null && t.ReachCm > 0;

        /// <summary>
        /// How close it walks in before it will attack: the shortest full reach (reach plus step-in) among its
        /// attacks, less <see cref="EngageMarginCm"/>, so every attack it has will reach. 0 if it has no attacks.
        /// </summary>
        public static int EngageDistance(Fighter me)
        {
            int best = int.MaxValue;
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (IsAttack(t)) best = Math.Min(best, t!.ReachCm + t.AdvanceCm);
            }
            return best == int.MaxValue ? 0 : Math.Max(1, best - EngageMarginCm);
        }

        /// <summary>
        /// An attack that reaches from <paramref name="distance"/> and it can pay for: a Qi-empowered one first,
        /// otherwise a heavy one with <see cref="BrainStyle.Heavy"/> chance, otherwise a lighter one.
        /// </summary>
        private Slot? PickAttack(Fighter me, int distance)
        {
            Slot? empowered = null;
            int heavyCount = 0, lightCount = 0;
            var heavy = new Slot[Slots.Count];
            var light = new Slot[Slots.Count];
            foreach (var s in Slots.All)
            {
                var t = me.Loadout[s];
                if (!IsAttack(t) || !me.CanUse(t!) || t!.ReachCm + t.AdvanceCm < distance) continue;
                if (empowered == null && t.CanEmpower && me.Qi - t.QiCost >= t.EmpowerQi) empowered = s;
                if (t.Weight == Weight.Heavy) heavy[heavyCount++] = s;
                else light[lightCount++] = s;
            }
            if (empowered != null) return empowered;
            if (heavyCount == 0 && lightCount == 0) return null;
            bool goHeavy = lightCount == 0 || heavyCount > 0 && _dice.Chance(Style.Heavy);
            return goHeavy ? heavy[_dice.Below(heavyCount)] : light[_dice.Below(lightCount)];
        }
    }
}
