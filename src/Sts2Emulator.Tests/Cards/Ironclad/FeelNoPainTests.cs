using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Power. MegaCrit.Sts2.Core.Models.Cards/FeelNoPain.cs applies FeelNoPainPower at
// DynamicVar("Power", 3m); OnUpgrade raises it by 1. The power's AfterCardExhausted
// grants that much block every time a card is exhausted.
public class FeelNoPainTests
{
    [Fact]
    public void AppliesThree()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.FeelNoPain));
    }

    [Fact]
    public void UpgradedAppliesFour()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FeelNoPain));
    }

    [Fact]
    public void GivesBlockWhenACardIsExhausted()
    {
        var fight = Fight.Hand(Card(IC.FeelNoPain), Card(IC.Tremble)).Energy(9).Enemy(hp: 40);
        fight.Play(index: 0);

        // Tremble exhausts itself.
        fight.Play(index: 0);

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    [Fact]
    public void GivesBlockOncePerCardExhausted()
    {
        var fight = Fight
            .Hand(Card(IC.FeelNoPain), Card(IC.FiendFire), Card(IC.Bash), Card(IC.StrikeIronclad))
            .Energy(9)
            .Enemy(hp: 60);
        fight.Play(index: 0);

        // Fiend Fire exhausts the two remaining cards and then itself.
        fight.Play(index: 0);

        Assert.Equal(9, fight.State.PlayerBlock);
    }

    /// <summary>
    /// `FeelNoPainPower.AfterCardExhausted` is `CreatureCmd.GainBlock(..., Unpowered)` --
    /// the ordinary command. It was a bare `PlayerBlock +=` with `IncomingBlock` applied,
    /// so Dexterity raised it where Unpowered says nothing touches it.
    ///
    /// These drive the exhaust directly rather than through a card, so what is measured
    /// is the power's own block and not a card's on top of it.
    /// </summary>
    [Fact]
    public void DexterityDoesNotRaiseIt()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.FeelNoPain, 3).PlayerBuff(BuffId.Dexterity, 5);

        CardEffects.ExhaustCard(fight.State, new CardInstance(IC.StrikeIronclad, false));

        Assert.Equal(3, fight.State.PlayerBlock);
    }

    /// <summary>And Frail does not cut it, for the same reason.</summary>
    [Fact]
    public void FrailDoesNotCutIt()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.FeelNoPain, 4).PlayerBuff(BuffId.Frail, 3);

        CardEffects.ExhaustCard(fight.State, new CardInstance(IC.StrikeIronclad, false));

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    /// <summary>
    /// The other direction: going through the command means everything hung off a block
    /// gain now applies. Feel No Pain into Juggernaut is a deck people build, and the
    /// bare `+=` meant the exhaust block never triggered it.
    /// </summary>
    [Fact]
    public void ItTriggersJuggernaut()
    {
        var fight = Fight
            .Hand()
            .Enemy(hp: 40)
            .PlayerBuff(BuffId.FeelNoPain, 3)
            .PlayerBuff(BuffId.Juggernaut, 6);

        CardEffects.ExhaustCard(fight.State, new CardInstance(IC.StrikeIronclad, false));

        Assert.Equal(3, fight.State.PlayerBlock);
        Assert.Equal(34, fight.Enemy0.Hp);
    }

    /// <summary>And Shadowmeld's doubling, which the bare path also skipped.</summary>
    [Fact]
    public void ShadowmeldDoublesIt()
    {
        var fight = Fight.Hand().PlayerBuff(BuffId.FeelNoPain, 3).PlayerBuff(BuffId.Shadowmeld, 1);

        CardEffects.ExhaustCard(fight.State, new CardInstance(IC.StrikeIronclad, false));

        Assert.Equal(6, fight.State.PlayerBlock);
    }
}
