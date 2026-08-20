using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics that count cards as they are played, read off
/// MegaCrit.Sts2.Core.Models.Relics: Shuriken CardsVar(3)/StrengthPower(1m), Kunai
/// CardsVar(3)/DexterityPower(1m), Ornamental Fan CardsVar(3)/BlockVar(4m, Unpowered),
/// Letter Opener CardsVar(3)/DamageVar(5m, Unpowered), Nunchaku CardsVar(10)/EnergyVar(1),
/// Permafrost BlockVar(7m, Unpowered) on the first Power, Mummified Hand's free card.
/// </summary>
public class CardPlayRelicTests
{
    private static Fight WithHand(int relicId, params int[] handIds)
    {
        var fight = Fight.WithRelics(relicId).Energy(20);
        fight.State.Hand = TestDeck.Pile(handIds);
        return fight;
    }

    private static int[] Repeat(int cardId, int count) =>
        Enumerable.Repeat(cardId, count).ToArray();

    [Fact]
    public void ShurikenGivesOneStrengthOnEveryThirdAttack()
    {
        var fight = WithHand(RelicEffects.Shuriken, Repeat(IC.StrikeIronclad, 6));

        fight.Play();
        fight.Play();
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));

        fight.Play();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Strength));

        fight.Play();
        fight.Play();
        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void ShurikenIgnoresSkills()
    {
        var fight = WithHand(RelicEffects.Shuriken, Repeat(IC.DefendIronclad, 3));

        fight.Play();
        fight.Play();
        fight.Play();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Strength));
    }

    [Fact]
    public void KunaiGivesOneDexterityOnEveryThirdAttack()
    {
        var fight = WithHand(RelicEffects.Kunai, Repeat(IC.StrikeIronclad, 3));

        fight.Play();
        fight.Play();
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.Play();
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    /// <summary>
    /// The count is per turn — the game clears it in BeforeSideTurnStart — so two attacks
    /// on one turn and a third on the next pay nothing.
    /// </summary>
    [Fact]
    public void KunaiForgetsAttacksPlayedOnAnEarlierTurn()
    {
        var fight = WithHand(RelicEffects.Kunai, Repeat(IC.StrikeIronclad, 2));

        fight.Play();
        fight.Play();
        fight.EndTurn();

        fight.State.Hand = TestDeck.Pile(IC.StrikeIronclad);
        fight.Energy(20).Play();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void OrnamentalFanGivesFourBlockOnEveryThirdAttack()
    {
        var fight = WithHand(RelicEffects.OrnamentalFan, Repeat(IC.StrikeIronclad, 3));

        fight.Play();
        fight.Play();
        Assert.Equal(0, fight.State.PlayerBlock);

        fight.Play();
        Assert.Equal(4, fight.State.PlayerBlock);
    }

    /// <summary>BlockVar(4m, ValueProp.Unpowered): Dexterity must not raise it.</summary>
    [Fact]
    public void OrnamentalFansBlockIgnoresDexterity()
    {
        var fight = WithHand(RelicEffects.OrnamentalFan, Repeat(IC.StrikeIronclad, 3));
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);

        fight.Play();
        fight.Play();
        fight.Play();

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    [Fact]
    public void LetterOpenerHitsEveryEnemyOnEveryThirdSkill()
    {
        var fight = WithHand(RelicEffects.LetterOpener, Repeat(IC.DefendIronclad, 3));
        var hpBefore = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();

        fight.Play();
        fight.Play();
        Assert.Equal(hpBefore, fight.State.Enemies.Select(enemy => enemy.Hp));

        fight.Play();
        Assert.Equal(hpBefore.Select(hp => hp - 5), fight.State.Enemies.Select(enemy => enemy.Hp));
    }

    /// <summary>DamageVar(5m, ValueProp.Unpowered): Strength must not raise it.</summary>
    [Fact]
    public void LetterOpenersDamageIgnoresStrength()
    {
        var fight = WithHand(RelicEffects.LetterOpener, Repeat(IC.DefendIronclad, 3));
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 5);
        var hpBefore = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();

        fight.Play();
        fight.Play();
        fight.Play();

        Assert.Equal(hpBefore.Select(hp => hp - 5), fight.State.Enemies.Select(enemy => enemy.Hp));
    }

    /// <summary>
    /// Nunchaku counts attacks for the whole combat rather than the turn, which is what
    /// separates it from Shuriken and Kunai: nine attacks over two turns and the tenth
    /// still pays.
    /// </summary>
    [Fact]
    public void NunchakuGivesEnergyOnTheTenthAttackOfTheCombat()
    {
        var fight = WithHand(RelicEffects.Nunchaku, Repeat(IC.StrikeIronclad, 5));
        for (int i = 0; i < 5; i++)
        {
            fight.Play();
        }

        fight.EndTurn();
        fight.State.Hand = TestDeck.Pile(Repeat(IC.StrikeIronclad, 5));
        fight.Energy(20);
        for (int i = 0; i < 4; i++)
        {
            fight.Play();
        }

        int energyBeforeTenth = fight.State.Energy;
        fight.Play();

        // The tenth attack costs one energy and hands one back.
        Assert.Equal(energyBeforeTenth, fight.State.Energy);
    }

    [Fact]
    public void PermafrostGivesSevenBlockOnTheFirstPowerOnly()
    {
        var fight = WithHand(RelicEffects.Permafrost, IC.Inflame, IC.Inflame);

        fight.Play();
        Assert.Equal(7, fight.State.PlayerBlock);

        fight.Play();
        Assert.Equal(7, fight.State.PlayerBlock);
    }

    [Fact]
    public void PermafrostIgnoresAttacksAndSkills()
    {
        var fight = WithHand(
            RelicEffects.Permafrost,
            IC.StrikeIronclad,
            IC.DefendIronclad,
            IC.Inflame
        );

        fight.Play();
        fight.Play();
        int blockFromDefend = fight.State.PlayerBlock;

        fight.Play();

        Assert.Equal(blockFromDefend + 7, fight.State.PlayerBlock);
    }

    [Fact]
    public void MummifiedHandMakesOneCardInHandFree()
    {
        var fight = WithHand(
            RelicEffects.MummifiedHand,
            IC.Inflame,
            IC.StrikeIronclad,
            IC.StrikeIronclad
        );

        fight.Play();

        Assert.Single(fight.State.Hand, card => card.FreeThisTurn);
    }

    /// <summary>
    /// The freed card is playable for nothing, which is the whole point — asserting on the
    /// flag alone would pass even if the cost path ignored it.
    /// </summary>
    [Fact]
    public void MummifiedHandsFreeCardCostsNoEnergy()
    {
        var fight = WithHand(RelicEffects.MummifiedHand, IC.Inflame, IC.StrikeIronclad);
        fight.Energy(1);

        fight.Play();
        Assert.Equal(0, fight.State.Energy);

        var enemyHpBefore = fight.State.Enemies[0].Hp;
        fight.Play();

        Assert.Equal(0, fight.State.Energy);
        Assert.True(fight.State.Enemies[0].Hp < enemyHpBefore, "the free Strike should land");
    }

    [Fact]
    public void MummifiedHandOnlyTriggersOnPowers()
    {
        var fight = WithHand(
            RelicEffects.MummifiedHand,
            IC.StrikeIronclad,
            IC.DefendIronclad,
            IC.StrikeIronclad
        );

        fight.Play();
        fight.Play();

        Assert.DoesNotContain(fight.State.Hand, card => card.FreeThisTurn);
    }
}
