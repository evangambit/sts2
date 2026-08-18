using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/BloodWall.cs: HpLossVar(2m) dealt as
// Unblockable | Unpowered | Move, then BlockVar(16m); OnUpgrade raises block by 4.
public class BloodWallTests
{
    [Fact]
    public void LosesTwoHpAndGainsSixteenBlock()
    {
        var fight = Fight.Hand(Card(IC.BloodWall)).Energy(2).PlayerHp(64).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(16, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsTwentyBlock()
    {
        var fight = Fight
            .Hand(Card(IC.BloodWall, upgraded: true))
            .Energy(2)
            .PlayerHp(64)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(20, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheHpLossIsUnblockableSoExistingBlockDoesNotAbsorbIt()
    {
        var fight = Fight.Hand(Card(IC.BloodWall)).Energy(2).PlayerHp(64).Enemy(hp: 40);
        fight.State.PlayerBlock = 10;

        fight.Play();

        Assert.Equal(62, fight.State.PlayerHp);
        Assert.Equal(26, fight.State.PlayerBlock);
    }
}
