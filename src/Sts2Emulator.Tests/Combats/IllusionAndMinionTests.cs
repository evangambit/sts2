using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Secondary enemies: the ones that cannot hold a fight open on their own, and the one
/// that cannot be killed at all.
///
/// <para>
/// <c>Creature.IsPrimaryEnemy</c> puts it in the game's own words — "a secondary enemy
/// will automatically die unless there's also a living primary enemy" — and what makes a
/// creature secondary is carrying <c>MinionPower</c> or <c>IllusionPower</c>. The emulator
/// had neither notion: it counted every body in the roster, which is wrong in both
/// directions at once. A Fogmog's eye revives forever, so a fight it outlived could never
/// be won; a Gas Bomb outliving its Living Fog kept a finished fight running.
/// </para>
/// </summary>
public class IllusionAndMinionTests
{
    /// <summary>
    /// A Fogmog mid-fight with its eye already out, which is every turn after the first:
    /// ILLUSION_MOVE is the machine's initial state and nothing leads back to it, so the
    /// eye is summoned once and thereafter keeps itself alive by reviving. LastMove is set
    /// past ILLUSION for the same reason -- a Fogmog that still owed its summon would add
    /// a second eye, which no real fight ever sees.
    /// </summary>
    private static Fight FogmogAndEye()
    {
        var fight = Fight
            .Hand(Card(IC.StrikeIronclad))
            .PlayerHp(200, 200)
            .Energy(3)
            .Enemy(hp: 40, defId: KE.Fogmog)
            .Enemy(hp: 6, defId: KE.EyeWithTeeth, buffs: new BuffState(BuffId.Illusion, 1));
        fight.State.Enemies[0].LastMove = 1;
        return fight;
    }

    private static EnemyState Eye(Fight fight) => fight.State.Enemies[1];

    /// <summary>
    /// <c>IllusionPower.AfterDeath</c> forces a REVIVE_MOVE with
    /// <c>MustPerformOnceBeforeTransitioning</c>, so the eye killed on the player's turn
    /// spends the next one coming back rather than acting — and cannot be hit meanwhile,
    /// which the emulator gets by leaving it at 0 until the heal lands.
    /// </summary>
    [Fact]
    public void AKilledIllusionSpendsItsNextTurnReviving()
    {
        var fight = FogmogAndEye();
        Eye(fight).Hp = 1;

        fight.Play(0, target: 1);

        Assert.Equal(0, Eye(fight).Hp);
        Assert.Equal(1, BuffSystem.Get(Eye(fight).Buffs, BuffId.Reviving));
    }

    [Fact]
    public void AndComesBackAtFullHealth()
    {
        var fight = FogmogAndEye();
        Eye(fight).Hp = 1;
        fight.Play(0, target: 1);

        fight.EndTurn();

        Assert.Equal(Eye(fight).MaxHp, Eye(fight).Hp);
        Assert.Equal(0, BuffSystem.Get(Eye(fight).Buffs, BuffId.Reviving));
    }

    /// <summary>
    /// It keeps IllusionPower through the death — <c>ShouldPowerBeRemovedOnDeath</c> is
    /// false for anything that is not a debuff — so it revives again, and again.
    /// </summary>
    [Fact]
    public void AnIllusionCannotBeKilledOffForGood()
    {
        var fight = FogmogAndEye();

        for (int round = 0; round < 3; round++)
        {
            // Killed through the engine rather than by assignment: a death is a
            // transition, which HandleEnemyDeaths reads off the turn's own before-snapshot.
            fight.State.Hand.Clear();
            fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
            fight.State.Energy = 3;
            Eye(fight).Hp = 1;

            fight.Play(0, target: 1);
            Assert.Equal(0, Eye(fight).Hp);

            fight.EndTurn();
            Assert.Equal(Eye(fight).MaxHp, Eye(fight).Hp);
            Assert.True(BuffSystem.Get(Eye(fight).Buffs, BuffId.Illusion) > 0);
        }
    }

    [Fact]
    public void KillingTheLastPrimaryWinsWithAnIllusionStillAlive()
    {
        var fight = FogmogAndEye();
        fight.State.Enemies[0].Hp = 0;

        var result = fight.EndTurn();

        Assert.True(result.Terminal);
        Assert.True(result.PlayerWon);
    }

    [Fact]
    public void AMinionDoesNotHoldTheFightOpenEither()
    {
        var fight = Fight
            .Hand()
            .PlayerHp(200, 200)
            .Enemy(hp: 0, defId: KE.LivingFog)
            .Enemy(hp: 9, defId: KE.GasBomb, buffs: new BuffState(BuffId.Minion, 1));

        var result = fight.EndTurn();

        Assert.True(result.Terminal);
        Assert.True(result.PlayerWon);
    }

    /// <summary>A living primary keeps the fight open, however its secondaries are doing.</summary>
    [Fact]
    public void ALivingPrimaryKeepsTheFightOpen()
    {
        var fight = FogmogAndEye();
        Eye(fight).Hp = 0;

        var result = fight.EndTurn();

        Assert.False(result.Terminal);
    }
}
