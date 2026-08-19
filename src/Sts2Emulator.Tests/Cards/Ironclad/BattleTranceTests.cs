using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/BattleTrance.cs: CardsVar(3) drawn, then
// PowerCmd.Apply<NoDrawPower>(1) — no further drawing this turn. OnUpgrade raises the
// draw to 4.
//
// The NoDrawPower is what makes it a gamble: nothing else draws for the rest of the turn,
// whatever asks.
public class BattleTranceTests
{
    [Fact]
    public void DrawsThree()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash, IC.StrikeIronclad, IC.DefendIronclad], Fight.Ids(fight.State.Hand));
    }

    [Fact]
    public void UpgradedDrawsFour()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance, upgraded: true))
            .Energy(1)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.DefendIronclad), Card(IC.Anger))
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.State.Hand.Count);
    }

    [Fact]
    public void StopsAnyLaterDrawThisTurn()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance), Card(IC.PommelStrike))
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);
        fight.Play(index: 0);
        int handAfterTrance = fight.State.Hand.Count;

        // Pommel Strike would draw one; the power stops it.
        fight.Play(index: 0);

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.NoDraw));
        Assert.Equal(handAfterTrance - 1, fight.State.Hand.Count);
    }

    [Fact]
    public void TheDrawBanLiftsAtTheEndOfTheTurn()
    {
        var fight = Fight
            .Hand(Card(IC.BattleTrance))
            .Energy(9)
            .Draw(Card(IC.Bash), Card(IC.StrikeIronclad), Card(IC.Anger), Card(IC.DefendIronclad))
            .Enemy(hp: 40);
        fight.Play();

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.NoDraw));
    }

    [Fact]
    public void DrawsWhatItCanFromAShortDrawPile()
    {
        var fight = Fight.Hand(Card(IC.BattleTrance)).Energy(1).Draw(Card(IC.Bash)).Enemy(hp: 40);

        fight.Play();

        Assert.Equal([IC.Bash], Fight.Ids(fight.State.Hand));
    }
}
