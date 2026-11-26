using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// The opponent brain clashes too (拼招): when it sees a clashable strike coming in time, it times one of its own strikes to
/// land in the strike's clash window. Played on the real technique and rules sheets, plus one made-up pair for the plain frames.
/// </summary>
public class BrainClashTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static BrainStyle Style(int reaction = 12, int clash = 0, int perfect = 0, int parry = 0) =>
        new("test", "test", reaction, 120, 0, 0, 0, parry, perfect, 0, 0, 0, 0, clash: clash);

    /// <summary>The playtest loadout: 罗汉拳 on □, 铁砂掌 on △, 化绵掌 on L1, 封穴 on R1.</summary>
    private static Loadout Starter()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.Triangle, Book.Find("tiesha_zhang")!);
        l.Assign(Slot.L1, Book.Find("timed_parry")!);
        l.Assign(Slot.R1, Book.Find("fengxue")!);
        return l;
    }

    /// <summary>You (One) press <paramref name="slot"/> on tick 0 at <paramref name="apart"/> cm; the brain plays Two.</summary>
    private static Duel YouStrike(Brain brain, Slot slot, int apart = 110, int ticks = 60, Loadout? you = null, Loadout? it = null)
    {
        var d = new Duel(Rules, you ?? Starter(), it ?? Starter(), 0, apart);
        for (int i = 0; i < ticks && !d.IsOver; i++)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(i == 0 ? SlotInput.Press(slot) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
        }
        return d;
    }

    [Fact]
    public void It_perfect_clashes_your_iron_palm_with_its_luohan_jab()
    {
        // 铁砂掌: 36-frame wind-up, clash window frames 28-35, perfect 28-29. Seen at frame 12, its 罗汉拳 hit 1 (13 frames)
        // pressed on tick 15 or 16 lands on frame 28 or 29.
        var brain = new Brain(Style(clash: 100, perfect: 100), 1);
        var d = YouStrike(brain, Slot.Triangle);

        var hit = d.Hits.First();
        Assert.Equal(Side.Two, hit.Attacker);
        Assert.Equal("luohan_quan_1", hit.Technique.Id);
        Assert.Equal(ClashOutcome.PerfectClash, hit.Clash);
        Assert.InRange(hit.DefenderMoveFrame, 28, 29);
        Assert.Equal(Rules.ClashPerfectQi, hit.AttackerQiGained);
        Assert.Equal(0, hit.Damage);
    }

    [Fact]
    public void It_shows_clashing_while_it_waits_for_its_moment()
    {
        var brain = new Brain(Style(clash: 100, perfect: 100), 1);
        var intents = new List<BrainIntent>();
        var d = new Duel(Rules, Starter(), Starter(), 0, 110);
        for (int i = 0; i < 16; i++)
        {
            var b = brain.Think(d, Side.Two);
            intents.Add(brain.Intent);
            d.Step(i == 0 ? SlotInput.Press(Slot.Triangle) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
        }
        Assert.Contains(BrainIntent.Clashing, intents);
    }

    [Fact]
    public void With_nothing_heavy_enough_for_the_plain_frames_it_goes_for_perfect()
    {
        // Not aiming for perfect, its only move quick enough is 罗汉拳 (medium) against your heavy 铁砂掌: a plain clash would lose,
        // so it times the perfect frames instead.
        var brain = new Brain(Style(clash: 100, perfect: 0), 1);
        var d = YouStrike(brain, Slot.Triangle);
        Assert.Equal(ClashOutcome.PerfectClash, d.Hits.First().Clash);
    }

    [Fact]
    public void Too_late_for_perfect_and_too_light_for_plain_it_parries_instead()
    {
        // Reaction 20: it sees 铁砂掌 on frame 20, too late to press for frames 28-29 (presses 15-16). 罗汉拳 is too light for 30-35.
        var brain = new Brain(Style(reaction: 20, clash: 100, perfect: 100, parry: 100), 1);
        var d = YouStrike(brain, Slot.Triangle);
        Assert.All(d.Hits, h => Assert.Equal(ClashOutcome.NoClash, h.Clash));
        Assert.NotEqual(ParryOutcome.NotParried, d.Hits.First().Parry);
    }

    [Fact]
    public void A_heavier_move_breaks_through_in_the_plain_frames()
    {
        // Made-up pair: your slow medium strike (30-frame wind-up, window 22-29, perfect 22-23) against its quick heavy one (10).
        var slow = Make.Move("slow", Weight.Medium, startup: 30, window: 8, perfect: 2, reach: 130, damage: 5);
        var quick = Make.Move("quick", Weight.Heavy, startup: 10, window: 4, perfect: 2, reach: 130, damage: 9);
        var book = new TechniqueBook(new[] { slow, quick });
        var you = new Loadout(book);
        you.Assign(Slot.Square, slow);
        var it = new Loadout(book);
        it.Assign(Slot.Square, quick);

        var brain = new Brain(Style(clash: 100, perfect: 0), 1);
        var d = YouStrike(brain, Slot.Square, you: you, it: it);

        var hit = d.Hits.First();
        Assert.Equal(Side.Two, hit.Attacker);
        Assert.Equal(ClashOutcome.StrikerWins, hit.Clash);
        Assert.InRange(hit.DefenderMoveFrame, 24, 29);
        Assert.Equal(9, hit.Damage);
    }

    [Fact]
    public void Seals_and_grabs_are_never_clashed()
    {
        var brain = new Brain(Style(clash: 100, perfect: 100), 1);
        var sealed_ = YouStrike(brain, Slot.R1);
        Assert.All(sealed_.Hits, h => Assert.Equal(ClashOutcome.NoClash, h.Clash));

        var grabber = Starter();
        grabber.Assign(Slot.R2, Book.Find("qinna_shou")!);
        var brain2 = new Brain(Style(clash: 100, perfect: 100), 1);
        var grabbed = YouStrike(brain2, Slot.R2, apart: 95, you: grabber);
        Assert.All(grabbed.Hits, h => Assert.Equal(ClashOutcome.NoClash, h.Clash));
    }

    [Fact]
    public void Too_slow_to_see_it_in_time_it_does_not_clash()
    {
        // Reaction 24 (the novice): it sees 铁砂掌 on frame 24, after the last press that could land in the window (frame 22).
        var brain = new Brain(Style(reaction: 24, clash: 100, perfect: 100), 1);
        var d = YouStrike(brain, Slot.Triangle);
        Assert.All(d.Hits, h => Assert.Equal(ClashOutcome.NoClash, h.Clash));
    }

    [Fact]
    public void On_the_real_sheet_the_master_clashes_your_iron_palm_often_and_the_novice_never()
    {
        var sheet = BrainCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("brains.csv"))).Sheet!;
        int Clashes(string id)
        {
            int n = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var d = YouStrike(new Brain(sheet.Find(id)!, seed), Slot.Triangle, ticks: 40);
                if (d.Hits.Any(h => h.Attacker == Side.Two && h.Clash != ClashOutcome.NoClash)) n++;
            }
            return n;
        }
        Assert.Equal(0, Clashes("novice"));
        Assert.InRange(Clashes("master"), 15, 120); // clash 35%, when it isn't already attacking you on its first choice
    }
}
