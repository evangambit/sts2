using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/NotYet.cs heals HealVar(10m); OnUpgrade
// raises it by 3.
public class NotYetTests
{
    [Fact]
    public void HealsTen()
    {
        var fight = Fight.Hand(Card(IC.NotYet)).Energy(2).PlayerHp(40).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(50, fight.State.PlayerHp);
    }

    [Fact]
    public void UpgradedHealsThirteen()
    {
        var fight = Fight
            .Hand(Card(IC.NotYet, upgraded: true))
            .Energy(2)
            .PlayerHp(40)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(53, fight.State.PlayerHp);
    }

    [Fact]
    public void NeverHealsPastMaxHp()
    {
        var fight = Fight.Hand(Card(IC.NotYet)).Energy(2).PlayerHp(78, maxHp: 80).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(80, fight.State.PlayerHp);
    }
}
