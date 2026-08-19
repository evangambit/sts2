using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardKeyword.Exhaust. MegaCrit.Sts2.Core.Models.Cards/Production.cs:
// EnergyVar(2); OnUpgrade raises it by 1.
public class ProductionTests
{
    [Fact]
    public void GainsTwoEnergy()
    {
        var fight = Fight.Hand(Card(CL.Production)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(2, fight.State.Energy);
    }

    [Fact]
    public void UpgradedGainsThree()
    {
        var fight = Fight.Hand(Card(CL.Production, upgraded: true)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.State.Energy);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(CL.Production)).Energy(0).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([CL.Production], Fight.Ids(fight.State.ExhaustPile));
        Assert.Empty(fight.State.DiscardPile);
    }
}
