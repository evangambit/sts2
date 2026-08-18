using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Attack, CardKeyword.Exhaust and CardKeyword.Innate.
// MegaCrit.Sts2.Core.Models.Cards/Backstab.cs: DamageVar(11m); OnUpgrade raises it by 4.
public class BackstabTests
{
    [Fact]
    public void DealsEleven()
    {
        var fight = Fight.Hand(Card(SI.Backstab)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(29, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsFifteen()
    {
        var fight = Fight.Hand(Card(SI.Backstab, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(25, fight.Enemy0.Hp);
    }

    [Fact]
    public void ExhaustsItself()
    {
        var fight = Fight.Hand(Card(SI.Backstab)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([SI.Backstab], Fight.Ids(fight.State.ExhaustPile));
        Assert.Empty(fight.State.DiscardPile);
    }
}
