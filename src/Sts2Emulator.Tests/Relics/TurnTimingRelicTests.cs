using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Relics that fire on a particular turn, read off MegaCrit.Sts2.Core.Models.Relics:
/// Lantern EnergyVar(1) and Bag of Marbles VulnerablePower(1) on turn one, Horn Cleat
/// BlockVar(14m) on turn two, Captain's Wheel BlockVar(18m) on turn three, Happy Flower
/// EnergyVar(1) every DynamicVar("Turns", 3m).
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

    [Fact]
    public void BagOfMarblesMakesUnprotectedEnemiesVulnerableOnTurnOne()
    {
        var fight = Fight.WithRelics(RelicEffects.BagOfMarbles);

        Assert.All(
            fight.State.Enemies.Where(enemy => BuffSystem.Get(enemy.Buffs, BuffId.Artifact) == 0),
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
}
