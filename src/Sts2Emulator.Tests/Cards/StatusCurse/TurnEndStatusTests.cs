using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The statuses the game marks HasTurnEndInHandEffect: each burns its holder for the
/// card's own damage value when the turn ends. Infection is the one Phrog Parasite
/// deals three of per Infect, and its absence was worth six HP a turn by turn five.
/// </summary>
public class TurnEndStatusTests
{
    [Theory]
    [InlineData(ST.Burn, 2)]
    [InlineData(ST.Infection, 3)]
    [InlineData(ST.Wither, 3)]
    [InlineData(ST.Toxic, 5)]
    public void BurnsHolderForItsOwnDamage(int statusId, int expected)
    {
        var fight = Fight.Hand(TestDeck.Card(statusId)).Enemy();
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - expected, fight.State.PlayerHp);
    }

    [Fact]
    public void StacksOncePerCopyHeld()
    {
        var fight = Fight
            .Hand(
                TestDeck.Card(ST.Infection),
                TestDeck.Card(ST.Infection),
                TestDeck.Card(ST.Infection)
            )
            .Enemy();
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - 9, fight.State.PlayerHp);
    }

    /// <summary>
    /// These deal damage rather than lose HP, so block absorbs them — which is what
    /// separates them from Beckon, the one turn-end status that ignores block.
    /// </summary>
    [Fact]
    public void IsBlockable()
    {
        var fight = Fight.Hand(TestDeck.Card(ST.Infection)).Enemy();
        fight.State.PlayerBlock = 5;
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before, fight.State.PlayerHp);
    }

    /// <summary>
    /// Toxic is the one of these that can be played, and playing it is how the damage is
    /// dodged: it has no OnPlay at all, only Exhaust and the turn-end effect.
    /// </summary>
    [Fact]
    public void PlayingToxicExhaustsItInsteadOfDealingItsDamage()
    {
        var fight = Fight.Hand(TestDeck.Card(ST.Toxic)).Enemy().Energy(1);
        int before = fight.State.PlayerHp;

        fight.Play();

        Assert.Equal(before, fight.State.PlayerHp);
        Assert.Contains(fight.State.ExhaustPile, card => card.DefId == ST.Toxic);
    }

    [Fact]
    public void DoesNothingFromTheDiscardPile()
    {
        var fight = Fight.Hand().Discard(TestDeck.Card(ST.Infection)).Enemy();
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before, fight.State.PlayerHp);
    }
}
