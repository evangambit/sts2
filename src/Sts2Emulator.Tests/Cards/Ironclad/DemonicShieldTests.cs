using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill, CardMultiplayerConstraint.MultiplayerOnly and TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/DemonicShield.cs: HpLossVar(1m), then a
// CalculatedBlockVar whose multiplier is the owner's current Block — so it doubles the
// block already there. In singleplayer the only ally is the player.
public class DemonicShieldTests
{
    [Fact]
    public void LosesOneHpAndDoublesExistingBlock()
    {
        var fight = Fight.Hand(Card(IC.DemonicShield)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 9;

        fight.Play();

        Assert.Equal(63, fight.State.PlayerHp);
        Assert.Equal(18, fight.State.PlayerBlock);
    }

    [Fact]
    public void StillCostsOneHpWithNoBlockToDouble()
    {
        var fight = Fight.Hand(Card(IC.DemonicShield)).Energy(1).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(63, fight.State.PlayerHp);
        Assert.Equal(0, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheHpLossIsUnblockable()
    {
        var fight = Fight.Hand(Card(IC.DemonicShield)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 5;

        fight.Play();

        Assert.Equal(63, fight.State.PlayerHp);
    }
}
