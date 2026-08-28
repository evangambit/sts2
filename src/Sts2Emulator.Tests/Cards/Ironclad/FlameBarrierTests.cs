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

    /// <summary>
    /// FlameBarrierPower is <c>AfterDamageReceived</c>, which CreatureCmd runs once per
    /// DamageResult -- so a three-hit attack pays three times. The retaliation used to sit
    /// in the attack branch's generic tail, past the multi-hit path's own <c>break</c>, so
    /// a multi-hit attacker took nothing at all.
    /// </summary>
    [Fact]
    public void RetaliatesOncePerHitOfAMultiHitAttack()
    {
        var fight = Fight.Hand().PlayerHp(80, 80).PlayerBuff(BuffId.FlameBarrier, 4).Enemy(hp: 40);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 2, Hits: 3);

        EnemyAI.ExecuteIntent(fight.Enemy0, fight.State, new Random(0));

        Assert.Equal(40 - (3 * 4), fight.Enemy0.Hp);
    }

    /// <summary>
    /// CreatureCmd guards <c>AfterCurrentHpChanged</c> on <c>UnblockedDamage > 0</c> and
    /// pointedly does not guard <c>AfterDamageReceived</c>, so a hit block swallows whole
    /// still burns its attacker.
    /// </summary>
    [Fact]
    public void RetaliatesAgainstAHitBlockAbsorbsEntirely()
    {
        var fight = Fight.Hand().PlayerHp(80, 80).PlayerBuff(BuffId.FlameBarrier, 4).Enemy(hp: 40);
        fight.State.PlayerBlock = 50;
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 6);

        EnemyAI.ExecuteIntent(fight.Enemy0, fight.State, new Random(0));

        Assert.Equal(80, fight.State.PlayerHp);
        Assert.Equal(36, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The hook is skipped when the blow killed its target (<c>!WasTargetKilled ||
    /// !IsDead</c>), so a player who dies to the hit does not retaliate.
    /// </summary>
    [Fact]
    public void DoesNotRetaliateOnTheHitThatKillsThePlayer()
    {
        var fight = Fight.Hand().PlayerHp(4, 80).PlayerBuff(BuffId.FlameBarrier, 4).Enemy(hp: 40);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 20);

        EnemyAI.ExecuteIntent(fight.Enemy0, fight.State, new Random(0));

        Assert.Equal(0, fight.State.PlayerHp);
        Assert.Equal(40, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The retaliation belongs to the damage, not to the branch that dealt it. A monster
    /// with a special case of its own breaks out of the attack branch long before the
    /// generic tail, and eighteen of them do.
    /// </summary>
    [Fact]
    public void RetaliatesAgainstAnAttackerWithItsOwnSpecialCase()
    {
        var fight = Fight.Hand().PlayerHp(80, 80).PlayerBuff(BuffId.FlameBarrier, 4).Enemy(hp: 40);
        fight.Enemy0.DefId = KE.SnappingJaxfruit;
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 3);

        EnemyAI.ExecuteIntent(fight.Enemy0, fight.State, new Random(0));

        // ENERGY_ORB is an attack plus StrengthPower(2), and it breaks out of the branch
        // as soon as it has dealt its damage.
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Strength));
        Assert.Equal(36, fight.Enemy0.Hp);
    }
}
