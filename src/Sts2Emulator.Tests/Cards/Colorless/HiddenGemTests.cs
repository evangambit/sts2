using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill. MegaCrit.Sts2.Core.Models.Cards/HiddenGem.cs picks a card in the DRAW
// pile off Rng.CombatCardSelection and raises its BaseReplayCount by IntVar("Replay", 2m)
// — that card then plays itself twice more when drawn; OnUpgrade raises the replay by 1.
//
// Replay counts are not modelled at all. The emulator draws a card and makes the hand
// free for the turn instead, which is a different card entirely; these pin what it does
// so the substitution is not mistaken for the real effect.
public class HiddenGemTests
{
    [Fact]
    public void DrawsACardAndMakesTheHandFreeForTheTurn()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem), Card(IC.Bash))
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad))
            .Enemy(hp: 60);

        fight.Play(index: 0);

        Assert.All(fight.State.Hand, card => Assert.True(card.FreeThisTurn));
    }

    [Fact]
    public void UpgradedDrawsTwo()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem, upgraded: true))
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void LeavesTheDrawPileCardsThemselvesUnchanged()
    {
        var fight = Fight
            .Hand(Card(CL.HiddenGem))
            .Energy(9)
            .Draw(Card(IC.StrikeIronclad), Card(IC.Bash))
            .Enemy(hp: 60);

        fight.Play();

        // No replay count exists to raise, so the drawn card is an ordinary Strike.
        Assert.Equal([IC.StrikeIronclad], Fight.Ids(fight.State.Hand));
    }
}
