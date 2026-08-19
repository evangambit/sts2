using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power, CardMultiplayerConstraint.MultiplayerOnly.
// MegaCrit.Sts2.Core.Models.Cards/Tank.cs applies TankPower(1) — enemies prefer to
// attack the tank — and OnUpgrade only makes it cheaper.
//
// TankPower needs allies to mean anything, so singleplayer models it as a no-op. These
// tests pin that it is inert rather than that it is implemented.
public class TankTests
{
    [Fact]
    public void IsPlayableAndChangesNothingInSingleplayer()
    {
        var fight = Fight.Hand(Card(IC.Tank)).Energy(2).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(40, fight.Enemy0.Hp);
        Assert.Empty(fight.State.PlayerBuffs);
    }

    [Fact]
    public void CostsItsEnergyAndLeavesPlayLikeAnyPower()
    {
        var fight = Fight.Hand(Card(IC.Tank)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.State.Energy);
        // A played Power goes nowhere: not back to hand, not to the discard pile.
        Assert.Empty(fight.State.Hand);
        Assert.Empty(fight.State.DiscardPile);
    }
}
