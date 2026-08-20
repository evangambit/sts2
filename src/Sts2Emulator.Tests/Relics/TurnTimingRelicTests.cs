using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Relics that fire on a particular turn, read off MegaCrit.Sts2.Core.Models.Relics:
/// Lantern EnergyVar(1) and Bag of Marbles VulnerablePower(1) on turn one, Horn Cleat
/// BlockVar(14m) on turn two, Captain's Wheel BlockVar(18m) on turn three, Happy Flower
/// EnergyVar(1) every DynamicVar("Turns", 3m), Pendulum CardsVar(1) on the same cycle,
/// Art of War EnergyVar(1) after an Attack-free turn, Pocketwatch CardsVar(3) after a turn
/// of DynamicVar("CardThreshold", 3m) cards or fewer.
/// </summary>
public class TurnTimingRelicTests
{
    [Fact]
    public void LanternGivesOneEnergyOnTheFirstTurnOnly()
    {
        var plain = Fight.WithRelics();
        var withLantern = Fight.WithRelics(RelicEffects.Lantern);

        Assert.Equal(plain.State.Energy + 1, withLantern.State.Energy);

        int afterFirst = withLantern.State.Energy;
        withLantern.EndTurn();

        Assert.Equal(plain.State.Energy, withLantern.State.Energy);
        Assert.NotEqual(afterFirst, withLantern.State.Energy);
    }

    /// <summary>
    /// Encounter 3, because every enemy in the default encounter holds Artifact and eats
    /// the debuff — filtering those out left this asserting over an empty list.
    /// </summary>
    [Fact]
    public void BagOfMarblesMakesEnemiesVulnerableOnTurnOne()
    {
        var fight = Fight.Encounter(3, RelicEffects.BagOfMarbles);

        Assert.NotEmpty(fight.State.Enemies);
        Assert.All(
            fight.State.Enemies,
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable))
        );
    }

    /// <summary>
    /// The debuff goes through the normal application path, so Artifact eats it — an
    /// enemy holding Artifact spends a stack instead of becoming Vulnerable. This
    /// encounter contains one, which is why the obvious "every enemy is Vulnerable"
    /// assertion is wrong.
    /// </summary>
    [Fact]
    public void BagOfMarblesIsAbsorbedByArtifact()
    {
        var plain = Fight.WithRelics();
        var withBag = Fight.WithRelics(RelicEffects.BagOfMarbles);

        int artifactWithout = plain.State.Enemies.Sum(e =>
            BuffSystem.Get(e.Buffs, BuffId.Artifact)
        );
        int artifactWith = withBag.State.Enemies.Sum(e => BuffSystem.Get(e.Buffs, BuffId.Artifact));
        int protectedEnemies = plain.State.Enemies.Count(e =>
            BuffSystem.Get(e.Buffs, BuffId.Artifact) > 0
        );

        Assert.True(protectedEnemies > 0, "this encounter should include an Artifact enemy");
        // One stack per protected enemy, and none of them took the Vulnerable.
        Assert.Equal(artifactWithout - protectedEnemies, artifactWith);
        Assert.All(
            withBag.State.Enemies.Where(e => BuffSystem.Get(e.Buffs, BuffId.Artifact) > 0),
            enemy => Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable))
        );
    }

    [Fact]
    public void HornCleatGivesFourteenBlockOnTheSecondTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.HornCleat);

        Assert.Equal(0, fight.State.PlayerBlock);
        fight.EndTurn();

        Assert.Equal(14, fight.State.PlayerBlock);
    }

    [Fact]
    public void CaptainsWheelGivesEighteenBlockOnTheThirdTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.CaptainsWheel);
        fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerBlock);
        fight.EndTurn();

        Assert.Equal(18, fight.State.PlayerBlock);
    }

    [Fact]
    public void HappyFlowerGivesEnergyEveryThirdTurn()
    {
        var plain = Fight.WithRelics();
        var withFlower = Fight.WithRelics(RelicEffects.HappyFlower);

        // Turns one and two are ordinary; the third pays out.
        withFlower.EndTurn();
        plain.EndTurn();
        Assert.Equal(plain.State.Energy, withFlower.State.Energy);

        withFlower.EndTurn();
        plain.EndTurn();
        Assert.Equal(plain.State.Energy + 1, withFlower.State.Energy);
    }

    [Fact]
    public void PendulumDrawsOneCardEveryThirdTurn()
    {
        var plain = Fight.WithRelics();
        var withPendulum = Fight.WithRelics(RelicEffects.Pendulum);

        Assert.Equal(plain.State.Hand.Count, withPendulum.State.Hand.Count);

        withPendulum.EndTurn();
        plain.EndTurn();
        Assert.Equal(plain.State.Hand.Count, withPendulum.State.Hand.Count);

        withPendulum.EndTurn();
        plain.EndTurn();
        Assert.Equal(plain.State.Hand.Count + 1, withPendulum.State.Hand.Count);
    }

    [Fact]
    public void ArtOfWarGivesEnergyAfterATurnWithNoAttacks()
    {
        var plain = Fight.WithRelics();
        var withRelic = Fight.WithRelics(RelicEffects.ArtOfWar);

        // Turn one pays nothing however it goes — the relic only looks backwards.
        Assert.Equal(plain.State.Energy, withRelic.State.Energy);

        plain.EndTurn();
        withRelic.EndTurn();

        Assert.Equal(plain.State.Energy + 1, withRelic.State.Energy);
    }

    [Fact]
    public void ArtOfWarStaysQuietAfterATurnWithAnAttack()
    {
        var plain = Fight.WithRelics().Energy(20);
        var withRelic = Fight.WithRelics(RelicEffects.ArtOfWar).Energy(20);
        foreach (var fight in new[] { plain, withRelic })
        {
            fight.State.Hand = TestDeck.Pile(IC.StrikeIronclad);
            fight.Play();
            fight.EndTurn();
        }

        Assert.Equal(plain.State.Energy, withRelic.State.Energy);
    }

    [Fact]
    public void PocketwatchDrawsThreeExtraAfterAQuietTurn()
    {
        var plain = Fight.WithRelics();
        var withWatch = Fight.WithRelics(RelicEffects.Pocketwatch);

        // The opening hand is untouched: the relic gives nothing on turn one.
        Assert.Equal(plain.State.Hand.Count, withWatch.State.Hand.Count);

        plain.EndTurn();
        withWatch.EndTurn();

        Assert.Equal(plain.State.Hand.Count + 3, withWatch.State.Hand.Count);
    }

    [Fact]
    public void PocketwatchStaysQuietAfterFourCards()
    {
        var plain = Fight.WithRelics().Energy(20);
        var withWatch = Fight.WithRelics(RelicEffects.Pocketwatch).Energy(20);
        foreach (var fight in new[] { plain, withWatch })
        {
            fight.State.Hand = TestDeck.Pile(
                IC.DefendIronclad,
                IC.DefendIronclad,
                IC.DefendIronclad,
                IC.DefendIronclad
            );
            for (int i = 0; i < 4; i++)
            {
                fight.Play();
            }

            fight.EndTurn();
        }

        Assert.Equal(plain.State.Hand.Count, withWatch.State.Hand.Count);
    }
}
