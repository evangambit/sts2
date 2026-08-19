using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/TheBomb.cs applies TheBombPower for
// DynamicVar("Turns", 3m) turns carrying DynamicVar("BombDamage", 40m); OnUpgrade raises
// the damage by 10 and leaves the fuse at 3.
public class TheBombTests
{
    [Fact]
    public void SetsAThreeTurnFuseForFortyDamage()
    {
        var fight = Fight.Hand(Card(CL.TheBomb)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.TheBombPower));
        Assert.Equal(40, fight.PlayerBuffAmount(BuffId.TheBombDamage));
    }

    [Fact]
    public void UpgradedCarriesFifty()
    {
        var fight = Fight.Hand(Card(CL.TheBomb, upgraded: true)).Energy(2).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.TheBombPower));
        Assert.Equal(50, fight.PlayerBuffAmount(BuffId.TheBombDamage));
    }

    [Fact]
    public void DoesNothingToEnemiesOnThePlayItself()
    {
        var fight = Fight.Hand(Card(CL.TheBomb)).Energy(2).Enemy(hp: 60).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60, fight.Enemy0.Hp);
        Assert.Equal(60, fight.Enemy1.Hp);
    }
}
