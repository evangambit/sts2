using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/TrueGrit.cs: BlockVar(7m), OnUpgrade
// raises it by 2, then exhausts one card from hand.
//
// Two known divergences, so these tests pin what the emulator models rather than the
// full rule. The game exhausts a RANDOM card unupgraded and a CHOSEN one upgraded; the
// emulator has no selection screen and always picks at random. And the game's random
// pick comes off Rng.CombatCardSelection while the emulator uses the combat rng — the
// same stream mistake that Juggernaut's target had, for card selection rather than
// targeting.
public class TrueGritTests
{
    [Fact]
    public void GainsSevenBlockAndExhaustsACardFromHand()
    {
        var fight = Fight
            .Hand(Card(IC.TrueGrit), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Single(fight.State.Hand);
        Assert.Single(fight.State.ExhaustPile);
    }

    [Fact]
    public void UpgradedGainsNineBlock()
    {
        var fight = Fight
            .Hand(Card(IC.TrueGrit, upgraded: true), Card(IC.Bash))
            .Energy(1)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void StillGainsBlockWithNothingElseInHand()
    {
        var fight = Fight.Hand(Card(IC.TrueGrit)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Empty(fight.State.ExhaustPile);
    }

    [Fact]
    public void NeverExhaustsItselfInsteadOfAHandCard()
    {
        var fight = Fight.Hand(Card(IC.TrueGrit), Card(IC.Bash)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Contains(fight.State.DiscardPile, card => card.DefId == IC.TrueGrit);
        Assert.Equal([IC.Bash], Fight.Ids(fight.State.ExhaustPile));
    }
}
