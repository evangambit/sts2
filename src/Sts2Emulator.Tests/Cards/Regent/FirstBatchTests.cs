using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Arsenal.cs: `PowerVar<ArsenalPower>(1)`, and the upgrade
// adds Innate rather than a bigger stack. The POWER is what pays:
// `AfterCardGeneratedForCombat` gives its owner that much Strength for every card they
// GENERATE — any generated card, not only a Status.
//
// The emulator gave a flat 1/2 Strength on play: the right stat at the wrong time, and
// never again.
public class ArsenalTests
{
    private const int Arsenal = 19;
    private const int Dirge = 145; // makes X Souls

    private static Fight Armed()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Arsenal, false));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void PlayingItGrantsNoStrengthByItself()
    {
        var fight = Armed();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Arsenal));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void EachGeneratedCardPaysAStrength()
    {
        var fight = Armed();
        fight.State.Energy = 4;
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        // Four energy through Dirge is four Souls, so four Strength.
        Assert.Equal(4, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void WithoutItGeneratingPaysNothing()
    {
        var fight = Fight.Hand().Energy(4).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Dirge, false));

        fight.Play(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Alignment.cs: an EnergyVar of 2 upgrading by 1, for a
// star cost of 3. The emulator had it stacked into a body that gains 1/2 — the right
// effect at someone else's numbers.
public class AlignmentTests
{
    private const int Alignment = 11;

    [Fact]
    public void ItGainsTwoEnergyForThreeStars()
    {
        var fight = Fight.Hand().Energy(1).Enemy(hp: 500);
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(Alignment, false));

        fight.Play(0);

        Assert.Equal(3, fight.State.Energy);
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeGainsThree()
    {
        var fight = Fight.Hand().Energy(1).Enemy(hp: 500);
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(Alignment, true));

        fight.Play(0);

        Assert.Equal(4, fight.State.Energy);
    }

    [Fact]
    public void WithoutTheStarsItCannotBePlayed()
    {
        var fight = Fight.Hand().Energy(1).Enemy(hp: 500);
        fight.State.Stars = 2;
        fight.State.Hand.Add(new CardInstance(Alignment, false));

        fight.Play(0);

        Assert.Equal(1, fight.State.Energy);
        Assert.Single(fight.State.Hand);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/AstralPulse.cs: 6 damage upgrading by 2, aimed with
// `TargetingAllOpponents(...).WithHitCount(2)` — twice at EVERY enemy, for 3 stars. The
// emulator hit one enemy once.
public class AstralPulseTests
{
    private const int AstralPulse = 22;

    private static Fight TwoEnemies()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500);
        fight.State.Stars = 9;
        return fight;
    }

    [Fact]
    public void ItHitsEveryEnemyTwice()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(AstralPulse, false));

        fight.Play(0, target: 0);

        Assert.Equal(488, fight.Enemy0.Hp);
        Assert.Equal(488, fight.State.Enemies[1].Hp);
    }

    [Fact]
    public void TheUpgradeHitsForEightEachTime()
    {
        var fight = TwoEnemies();
        fight.State.Hand.Add(new CardInstance(AstralPulse, true));

        fight.Play(0, target: 0);

        Assert.Equal(484, fight.Enemy0.Hp);
        Assert.Equal(484, fight.State.Enemies[1].Hp);
    }

    /// <summary>Separate hits, so block is spent twice.</summary>
    [Fact]
    public void BlockIsSpentOnEachHit()
    {
        var fight = TwoEnemies();
        fight.Enemy0.Block = 6;
        fight.State.Hand.Add(new CardInstance(AstralPulse, false));

        fight.Play(0, target: 0);

        Assert.Equal(0, fight.Enemy0.Block);
        Assert.Equal(494, fight.Enemy0.Hp);
    }
}
