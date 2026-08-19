using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power, CardMultiplayerConstraint.MultiplayerOnly.
// MegaCrit.Sts2.Core.Models.Cards/BeaconOfHope.cs applies BeaconOfHopePower(1) — shared
// block for allies — and OnUpgrade adds CardKeyword.Innate.
//
// Singleplayer has no allies, so the emulator models it as a no-op. These tests pin that
// it is inert, and that the upgrade's Innate still comes off the card data.
public class BeaconOfHopeTests
{
    [Fact]
    public void IsInertInSingleplayer()
    {
        var fight = Fight.Hand(Card(CL.BeaconOfHope)).Energy(2).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Empty(fight.State.PlayerBuffs);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(40, fight.Enemy0.Hp);
    }

    [Fact]
    public void IsInnateOnlyOnceUpgraded()
    {
        Assert.False(Card(CL.BeaconOfHope).IsInnate());
        Assert.True(Card(CL.BeaconOfHope, upgraded: true).IsInnate());
    }
}
