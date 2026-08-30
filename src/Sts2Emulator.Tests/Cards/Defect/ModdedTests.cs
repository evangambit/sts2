using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Modded.cs: one orb SLOT, draw CardsVar 1 (upgrading by 1),
// and `EnergyCost.AddThisCombat(1)` — on the CARD, so this copy costs one more every time it
// is played, exactly as Frantic Escape does.
//
// The card was missing from the emulator's data entirely: `extract_data.py` had skipped it
// since the initial commit with no reason given, and it is unconditionally in
// DefectCardPool between Meteor Strike and Momentum Strike. So the emulator's Defect pool
// was 87 cards where the game has 88, and this one could never be offered, generated or
// played. A live capture of it refused to generate because the id map had never heard of it.
public class ModdedTests
{
    private const int Modded = 547;
    private const int StrikeDefect = 471;

    private static Fight Fresh()
    {
        var fight = DefectFight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeDefect, false));
        }

        return fight;
    }

    [Fact]
    public void ItIsInTheDefectPool()
    {
        Assert.Contains(Modded, GeneratedData.CardPools.Defect.ToArray());
        Assert.Equal(88, GeneratedData.CardPools.Defect.Length);
    }

    [Fact]
    public void ItAddsASlotAndDrawsOne()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Modded, false));

        fight.Play(0);

        Assert.Equal(4, fight.State.OrbCapacity);
        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void TheUpgradeDrawsTwo()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Modded, true));

        fight.Play(0);

        Assert.Equal(4, fight.State.OrbCapacity);
        Assert.Equal(2, fight.State.Hand.Count);
    }

    /// <summary>The cost rides on the COPY, so the one that was played gets dearer.</summary>
    [Fact]
    public void ThePlayedCopyCostsOneMore()
    {
        var fight = Fresh();
        fight.State.Hand.Add(new CardInstance(Modded, false));

        fight.Play(0);

        var played = fight.State.DiscardPile.First(c => c.DefId == Modded);
        Assert.Equal(1, CombatEngine.EffectiveCost(played, fight.State));
    }

    /// <summary>Ten slots is the ceiling — `Math.Min(10 - Capacity, amount)`.</summary>
    [Fact]
    public void ItStopsAtTenSlots()
    {
        var fight = Fresh();
        fight.State.OrbCapacity = 10;
        fight.State.Hand.Add(new CardInstance(Modded, false));

        fight.Play(0);

        Assert.Equal(10, fight.State.OrbCapacity);
    }
}
