using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 2-cost Skill. MegaCrit.Sts2.Core.Models.Cards/FlameBarrier.cs: BlockVar(12m) then
// PowerCmd.Apply<FlameBarrierPower>(4); OnUpgrade raises the block by 4 and the
// damage-back by 2.
public class FlameBarrierTests
{
    [Fact]
    public void GainsTwelveBlockAndFourDamageBack()
    {
        var fight = Fight.Hand(Card(IC.FlameBarrier)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(12, fight.State.PlayerBlock);
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FlameBarrier));
    }

    [Fact]
    public void UpgradedGainsSixteenBlockAndSixDamageBack()
    {
        var fight = Fight.Hand(Card(IC.FlameBarrier, upgraded: true)).Energy(2).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(16, fight.State.PlayerBlock);
        Assert.Equal(6, fight.PlayerBuffAmount(BuffId.FlameBarrier));
    }

    [Fact]
    public void TheBlockGainStillTriggersJuggernaut()
    {
        var fight = Fight
            .Hand(Card(IC.FlameBarrier))
            .Energy(2)
            .PlayerBuff(BuffId.Juggernaut, 6)
            .Enemy(hp: 40);

        fight.Play();

        Assert.Equal(34, fight.Enemy0.Hp);
    }
}
