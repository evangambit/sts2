using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 0-cost Skill. MegaCrit.Sts2.Core.Models.Cards/TheGambit.cs: BlockVar(50m) then
// TheGambitPower(1); OnUpgrade raises the block by 25.
//
// TheGambitPower.AfterDamageReceived removes itself and KILLS the owner on the first
// unblocked powered attack, which is what makes 50 block for nothing a gamble. The
// emulator stood NoBlock in for it, a far milder card; it now models the kill.
public class TheGambitTests
{
    [Fact]
    public void GainsFiftyBlock()
    {
        var fight = Fight.Hand(Card(CL.TheGambit)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(50, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsSeventyFive()
    {
        var fight = Fight.Hand(Card(CL.TheGambit, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(75, fight.State.PlayerBlock);
    }

    [Fact]
    public void ArmsThePowerThatCanKillYou()
    {
        var fight = Fight.Hand(Card(CL.TheGambit)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TheGambitPower));
    }

    [Fact]
    public void AnUnblockedAttackKillsTheArmedPlayerOutright()
    {
        var fight = Fight.Hand(Card(CL.TheGambit)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        // The dummy enemy has no moves of its own, so give it something to swing.
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 10);
        fight.Play();
        // Spend the block it just gave, so the hit lands unblocked.
        fight.State.PlayerBlock = 0;

        fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerHp);
    }

    [Fact]
    public void TheBlockItGivesIsWhatKeepsYouAlive()
    {
        var fight = Fight.Hand(Card(CL.TheGambit)).Energy(1).PlayerHp(64).Enemy(hp: 40);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 10);
        fight.Play();

        fight.EndTurn();

        // 50 block absorbs the attack, so nothing goes unblocked and the power stays
        // armed.
        Assert.Equal(64, fight.State.PlayerHp);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.TheGambitPower));
    }
}
