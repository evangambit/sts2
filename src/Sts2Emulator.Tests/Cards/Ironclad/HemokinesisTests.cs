using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Attack. MegaCrit.Sts2.Core.Models.Cards/Hemokinesis.cs: HpLossVar(2m) dealt to
// the player as Unblockable | Unpowered | Move, then DamageVar(15m); OnUpgrade raises
// the damage by 5.
public class HemokinesisTests
{
    [Fact]
    public void LosesTwoHpAndDealsFifteen()
    {
        var fight = Fight.Hand(Card(IC.Hemokinesis)).Energy(1).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(25, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedDealsTwenty()
    {
        var fight = Fight
            .Hand(Card(IC.Hemokinesis, upgraded: true))
            .Energy(1)
            .PlayerHp(64)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(20, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheHpLossIsUnblockable()
    {
        var fight = Fight.Hand(Card(IC.Hemokinesis)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 10;

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(10, fight.State.PlayerBlock);
    }
}
