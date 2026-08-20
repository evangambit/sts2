using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics whose ModifyMaxEnergy adds EnergyVar(1), read off
/// MegaCrit.Sts2.Core.Models.Relics. They all grant the same energy; the tests that matter
/// are the prices — Sozu ShouldProcurePotion, Ectoplasm ModifyGoldGained, Velvet Choker
/// CardsVar(6) via ShouldPlay, Spiked Gauntlets TryModifyEnergyCostInCombat on Powers,
/// Philosopher's Stone StrengthPower(1m) on every opponent, Blessed Antler CardsVar(3)
/// of Dazed.
/// </summary>
public class EnergyRelicTests
{
    [Theory]
    [InlineData(RelicEffects.Ectoplasm)]
    [InlineData(RelicEffects.Sozu)]
    [InlineData(RelicEffects.SpikedGauntlets)]
    [InlineData(RelicEffects.VelvetChoker)]
    [InlineData(RelicEffects.PhilosophersStone)]
    [InlineData(RelicEffects.BlessedAntler)]
    public void EachGrantsOneExtraEnergy(int relicId)
    {
        var plain = Fight.WithRelics();
        var withRelic = Fight.WithRelics(relicId);

        Assert.Equal(plain.State.MaxEnergy + 1, withRelic.State.MaxEnergy);
        Assert.Equal(plain.State.Energy + 1, withRelic.State.Energy);
    }

    /// <summary>The refill at turn start reads MaxEnergy, so the bonus has to survive it.</summary>
    [Fact]
    public void TheExtraEnergyIsStillThereOnLaterTurns()
    {
        var plain = Fight.WithRelics();
        var withRelic = Fight.WithRelics(RelicEffects.Ectoplasm);

        plain.EndTurn();
        withRelic.EndTurn();

        Assert.Equal(plain.State.Energy + 1, withRelic.State.Energy);
    }

    /// <summary>
    /// StepResult.Invalid is just (false, false, 0), which a legal but unrewarding play
    /// also produces — so a refused play is asserted by the state not moving.
    /// </summary>
    [Fact]
    public void VelvetChokerStopsTheSeventhCardOfTheTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.VelvetChoker).Energy(20);
        fight.State.Hand = Pile(Enumerable.Repeat(IC.DefendIronclad, 7).ToArray());

        for (int i = 0; i < 6; i++)
        {
            fight.Play();
        }

        Assert.Single(fight.State.Hand);
        int blockAfterSix = fight.State.PlayerBlock;
        fight.Play();

        Assert.Single(fight.State.Hand);
        Assert.Equal(blockAfterSix, fight.State.PlayerBlock);
    }

    /// <summary>
    /// A blocked play must also leave the action space, or a policy spends its turn
    /// choosing a move the engine refuses.
    /// </summary>
    [Fact]
    public void VelvetChokerLeavesOnlyEndTurnAndPotions()
    {
        var fight = Fight.WithRelics(RelicEffects.VelvetChoker).Energy(20);
        fight.State.Hand = Pile(Enumerable.Repeat(IC.DefendIronclad, 7).ToArray());
        for (int i = 0; i < 6; i++)
        {
            fight.Play();
        }

        Assert.DoesNotContain(
            CombatEngine.ValidActions(fight.State),
            action => action < fight.State.Hand.Count
        );
    }

    [Fact]
    public void VelvetChokersLimitResetsEachTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.VelvetChoker).Energy(20);
        fight.State.Hand = Pile(Enumerable.Repeat(IC.DefendIronclad, 6).ToArray());
        for (int i = 0; i < 6; i++)
        {
            fight.Play();
        }

        fight.EndTurn();
        fight.State.Hand = Pile(IC.DefendIronclad);
        fight.Energy(20);
        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void SpikedGauntletsMakesPowersCostOneMore()
    {
        var fight = Fight.WithRelics(RelicEffects.SpikedGauntlets).Energy(1);
        fight.State.Hand = Pile(IC.Inflame);

        fight.Play();
        Assert.Single(fight.State.Hand);
        Assert.Equal(1, fight.State.Energy);

        fight.Energy(2);
        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal(0, fight.State.Energy);
    }

    [Fact]
    public void SpikedGauntletsLeavesAttacksAndSkillsAlone()
    {
        var fight = Fight.WithRelics(RelicEffects.SpikedGauntlets).Energy(1);
        fight.State.Hand = Pile(IC.StrikeIronclad);

        fight.Play();

        Assert.Empty(fight.State.Hand);
        Assert.Equal(0, fight.State.Energy);
    }

    [Fact]
    public void PhilosophersStoneGivesEveryEnemyOneStrength()
    {
        var fight = Fight.Encounter(3, RelicEffects.PhilosophersStone);

        Assert.NotEmpty(fight.State.Enemies);
        Assert.All(
            fight.State.Enemies,
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Strength))
        );
    }

    /// <summary>
    /// The Dazed go into the draw pile before the opening hand is dealt, so some of them
    /// can be drawn straight into it — the count is over both piles.
    /// </summary>
    [Fact]
    public void BlessedAntlerShufflesThreeDazedIntoTheDeck()
    {
        var plain = Fight.WithRelics();
        var withAntler = Fight.WithRelics(RelicEffects.BlessedAntler);

        Assert.Equal(0, CountDazed(plain));
        Assert.Equal(3, CountDazed(withAntler));
        Assert.Equal(
            plain.State.DrawPile.Count + plain.State.Hand.Count + 3,
            withAntler.State.DrawPile.Count + withAntler.State.Hand.Count
        );
    }

    private static int CountDazed(Fight fight) =>
        fight.State.DrawPile.Concat(fight.State.Hand).Count(card => card.DefId == ST.Dazed);

    [Fact]
    public void EctoplasmZeroesGoldGainedInCombat()
    {
        var plain = HandOfGreedFight();
        var withEctoplasm = HandOfGreedFight();
        withEctoplasm.State.Relics.Add(new RelicInstance(RelicEffects.Ectoplasm));

        int plainGold = plain.State.PlayerGold;
        plain.Play();
        withEctoplasm.Play();

        Assert.Equal(plainGold + 20, plain.State.PlayerGold);
        Assert.Equal(plainGold, withEctoplasm.State.PlayerGold);
    }

    /// <summary>Hand of Greed pays 20 gold only when it kills, hence the 5 HP enemy.</summary>
    private static Fight HandOfGreedFight() =>
        Fight.Hand(Card(CL.HandOfGreed)).Energy(3).Enemy(hp: 5);

    [Fact]
    public void SozuRefusesEveryPotion()
    {
        var plain = new RunState();
        var withSozu = new RunState();
        withSozu.Relics.Add(new RelicInstance(RelicEffects.Sozu));

        Assert.True(RunRewardGenerator.AddPotion(plain, potionId: 1));
        Assert.False(RunRewardGenerator.AddPotion(withSozu, potionId: 1));
        Assert.Equal(1, plain.PotionSlots[0]);
        Assert.Equal(0, withSozu.PotionSlots[0]);
    }
}
