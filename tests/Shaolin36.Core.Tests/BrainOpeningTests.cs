using System.IO;
using System.Linq;
using Shaolin36.Core.Brains;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Data;
using Shaolin36.Core.Fighters;
using Xunit;

namespace Shaolin36.Core.Tests;

/// <summary>
/// Chan: the mini boss gave too few openings. Right after each of its own attacks it won't parry or dodge for a moment
/// (<see cref="Brain.OpeningFrames"/>, miniboss_opening in run.csv), so punishing it works; attack first and it still defends.
/// </summary>
public class BrainOpeningTests
{
    private static readonly Rules Rules = RulesCsv.Parse(string.Join("\n",
        File.ReadAllLines(GameDataTests.SheetPath("rules.csv")).Select(l => l.StartsWith("opponent_ready,") ? "opponent_ready,0,test" : l))).Rules!;
    private static readonly TechniqueBook Book =
        new(TechniqueCsv.Parse(File.ReadAllText(GameDataTests.SheetPath("techniques.csv"))).Techniques);

    private static Loadout Set()
    {
        var l = new Loadout(Book);
        l.Assign(Slot.Square, Book.Find("luohan_quan_1")!);
        l.Assign(Slot.L1, Book.Find("timed_parry")!);
        return l;
    }

    /// <summary>Always parries what it sees, at once; attacks only when a test makes it (aggression 100 for the first choice).</summary>
    private static BrainStyle Parrier(int aggression) =>
        new("test", "test", 0, 10, aggression, 0, 0, 100, 0, 0, 0, 0, 0);

    /// <summary>
    /// The brain throws one 罗汉拳 hit 1 (no chain) from 110 cm; you stand still until it is idle again, wait
    /// <paramref name="afterIdle"/> more ticks, then throw your 罗汉拳. True if your hit landed.
    /// </summary>
    private static bool PunishLands(int opening, int afterIdle)
    {
        var d = new Duel(Rules, Set(), Set(), 0, 110);
        var brain = new Brain(Parrier(100), 7) { OpeningFrames = opening };
        bool attacked = false;
        int idleFor = -1, pressAt = -1;
        for (int i = 0; i < 400 && !d.IsOver; i++)
        {
            var b = brain.Think(d, Side.Two);
            if (d.Two.State.IsInMove() && d.Two.Technique!.ReachCm > 0) attacked = true;
            // Stop it attacking again once it has: it only defends from here.
            if (attacked) b = new BrainInput(b.Slots.IsPressed(Slot.Square) ? SlotInput.None : b.Slots, b.Move);
            if (attacked && d.Two.State == FighterState.Idle && d.One.CanAct && pressAt < 0)
            {
                idleFor++;
                if (idleFor >= afterIdle) pressAt = i;
            }
            d.Step(i == pressAt ? SlotInput.Press(Slot.Square) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
            if (pressAt >= 0 && i > pressAt + 30) break;
        }
        Assert.True(attacked, "the brain attacked first");
        Assert.True(pressAt >= 0, "you got to press");
        return d.Hits.Any(h => h.Attacker == Side.One && h.Landed);
    }

    [Fact]
    public void Right_after_its_attack_your_punish_lands()
    {
        Assert.True(PunishLands(opening: 30, afterIdle: 0));
    }

    [Fact]
    public void Without_an_opening_it_parries_the_same_punish()
    {
        Assert.False(PunishLands(opening: 0, afterIdle: 0));
    }

    [Fact]
    public void Once_the_opening_has_passed_it_parries_again()
    {
        Assert.False(PunishLands(opening: 30, afterIdle: 40));
    }

    [Fact]
    public void Attack_first_and_it_still_parries_you()
    {
        var d = new Duel(Rules, Set(), Set(), 0, 110);
        var brain = new Brain(Parrier(0), 7) { OpeningFrames = 30 };
        for (int i = 0; i < 40; i++)
        {
            var b = brain.Think(d, Side.Two);
            d.Step(i == 5 ? SlotInput.Press(Slot.Square) : SlotInput.None, b.Slots, MoveInput.None, b.Move);
        }
        Assert.Contains(d.Hits, h => h.Attacker == Side.One && h.Parry != ParryOutcome.NotParried);
        Assert.False(brain.IsOpen(d.Tick));
    }
}
