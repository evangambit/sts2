using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class FeedTests
{
    [Fact]
    public void KillsEnemyAndGrantsMaxHp()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 1; // set enemy to 1 HP so Feed kills it
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(0, enemy.Hp);
        Assert.Equal(maxHpBefore + 3, state.PlayerMaxHp);
    }

    [Fact]
    public void NoKillNoMaxHpGain()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        var enemy = state.Enemies[0];
        enemy.Hp = 100; // enemy survives
        int maxHpBefore = state.PlayerMaxHp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Feed, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);
        Assert.Equal(maxHpBefore, state.PlayerMaxHp);
    }

    /// <summary>
    /// `CreatureCmd.GainMaxHp` heals by the amount it grants. Raising the cap alone hands
    /// the player a number they then have to go and earn back at a rest site, which is a
    /// materially worse card.
    /// </summary>
    [Fact]
    public void TheMaxHpGainHealsToo()
    {
        var fight = Fight.Hand(new CardInstance(IC.Feed, false)).Energy(3).Enemy(hp: 1);
        fight.State.PlayerHp = 50;
        int maxHpBefore = fight.State.PlayerMaxHp;

        fight.Play(0);

        Assert.Equal(maxHpBefore + 3, fight.State.PlayerMaxHp);
        Assert.Equal(53, fight.State.PlayerHp);
    }

    /// <summary>
    /// `Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal())`, and `MinionPower`
    /// answers false. Killing a summon is not a Fatal, so Feed on one is 10 damage and
    /// nothing else.
    /// </summary>
    [Fact]
    public void KillingAMinionFeedsNothing()
    {
        var fight = Fight
            .Hand(new CardInstance(IC.Feed, false))
            .Energy(3)
            .Enemy(hp: 1, buffs: new BuffState(BuffId.Minion, 1));
        int maxHpBefore = fight.State.PlayerMaxHp;

        fight.Play(0);

        Assert.True(fight.State.Enemies[0].Hp <= 0);
        Assert.Equal(maxHpBefore, fight.State.PlayerMaxHp);
    }

    /// <summary>
    /// `ReattachPower` answers false while any OTHER segment stands: a Decimillipede is
    /// not dead until its last segment is, so the first segment killed pays nothing and
    /// the last one pays.
    /// </summary>
    [Fact]
    public void ASegmentPaysOnlyWhenItIsTheLast()
    {
        var early = Fight
            .Hand(new CardInstance(IC.Feed, false))
            .Energy(3)
            .Enemy(hp: 1, defId: KE.DecimillipedeSegment, buffs: new BuffState(BuffId.Reattach, 25))
            .Enemy(
                hp: 40,
                defId: KE.DecimillipedeSegment,
                buffs: new BuffState(BuffId.Reattach, 25)
            );
        int earlyMaxHp = early.State.PlayerMaxHp;
        early.Play(0);
        Assert.Equal(earlyMaxHp, early.State.PlayerMaxHp);

        var last = Fight
            .Hand(new CardInstance(IC.Feed, false))
            .Energy(3)
            .Enemy(hp: 1, defId: KE.DecimillipedeSegment, buffs: new BuffState(BuffId.Reattach, 25))
            .Enemy(
                hp: 0,
                defId: KE.DecimillipedeSegment,
                buffs: new BuffState(BuffId.Reattach, 25)
            );
        int lastMaxHp = last.State.PlayerMaxHp;
        last.Play(0);
        Assert.Equal(lastMaxHp + 3, last.State.PlayerMaxHp);
    }
}
