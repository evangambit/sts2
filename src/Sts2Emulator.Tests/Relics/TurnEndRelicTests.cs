using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The relics that fire as the player's turn ends, read off
/// MegaCrit.Sts2.Core.Models.Relics: Cloak Clasp BlockVar(1m, Unpowered) per card in hand,
/// Screaming Flagon DamageVar(20m, Unpowered) on an empty hand, Stone Calendar
/// DamageVar(52m, Unpowered) on DynamicVar("DamageTurn", 7m), Parrying Shield
/// DamageVar(6m, Unpowered) once the player holds BlockVar(10m).
///
/// Block granted at the end of a turn is spent by the enemies before EndTurn returns, so
/// these measure what the enemies could not get through rather than the block itself.
/// </summary>
public class TurnEndRelicTests
{
    [Fact]
    public void CloakClaspGivesOneBlockPerCardLeftInHand()
    {
        var fight = Fight.Hand(Card(IC.DefendIronclad), Card(IC.DefendIronclad)).Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.CloakClasp));
        fight.State.Enemies[0].CurrentIntent = new Intent(IntentType.Attack, 1);

        int hpBefore = fight.State.PlayerHp;
        fight.EndTurn();

        // Two cards in hand, so two block — the enemy's single point never lands.
        Assert.Equal(hpBefore, fight.State.PlayerHp);
    }

    [Fact]
    public void CloakClaspGivesNothingForAnEmptyHand()
    {
        var fight = Fight.Hand().Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.CloakClasp));
        fight.State.Enemies[0].CurrentIntent = new Intent(IntentType.Attack, 1);

        int hpBefore = fight.State.PlayerHp;
        fight.EndTurn();

        Assert.Equal(hpBefore - 1, fight.State.PlayerHp);
    }

    /// <summary>BlockVar(1m, Unpowered): Dexterity must not raise the per-card block.</summary>
    [Fact]
    public void CloakClaspsBlockIgnoresDexterity()
    {
        var fight = Fight.Hand(Card(IC.DefendIronclad)).Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.CloakClasp));
        fight.State.Enemies[0].CurrentIntent = new Intent(IntentType.Attack, 2);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);

        int hpBefore = fight.State.PlayerHp;
        fight.EndTurn();

        // One card, so one block: the second point of the hit still gets through.
        Assert.Equal(hpBefore - 1, fight.State.PlayerHp);
    }

    [Fact]
    public void ScreamingFlagonHitsEveryEnemyWhenTheHandIsEmpty()
    {
        var fight = Fight.Hand().Enemy(hp: 100).Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.ScreamingFlagon));

        fight.EndTurn();

        Assert.All(fight.State.Enemies, enemy => Assert.Equal(80, enemy.Hp));
    }

    [Fact]
    public void ScreamingFlagonStaysQuietWithCardsInHand()
    {
        var fight = Fight.Hand(Card(IC.DefendIronclad)).Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.ScreamingFlagon));

        fight.EndTurn();

        Assert.Equal(100, fight.State.Enemies[0].Hp);
    }

    [Fact]
    public void StoneCalendarHitsEveryEnemyAtTheEndOfTheSeventhTurn()
    {
        // HP enough to survive seven turns of two dummies swinging for 18 apiece. The
        // combat ENDS when the player dies, so a fight that has to reach turn seven has
        // to be survivable to get there -- this used to run on past a dead player.
        var fight = Fight.Hand().PlayerHp(999, 999).Enemy(hp: 100).Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.StoneCalendar));

        for (int turn = 0; turn < 6; turn++)
        {
            fight.EndTurn();
        }

        Assert.All(fight.State.Enemies, enemy => Assert.Equal(100, enemy.Hp));

        fight.EndTurn();

        Assert.All(fight.State.Enemies, enemy => Assert.Equal(48, enemy.Hp));
    }

    [Fact]
    public void ParryingShieldHitsOnceWhenThePlayerEndsOnTenBlock()
    {
        var fight = Fight.Hand().Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.ParryingShield));
        fight.State.PlayerBlock = 10;

        fight.EndTurn();

        Assert.Equal(94, fight.State.Enemies[0].Hp);
    }

    [Fact]
    public void ParryingShieldStaysQuietBelowTenBlock()
    {
        var fight = Fight.Hand().Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.ParryingShield));
        fight.State.PlayerBlock = 9;

        fight.EndTurn();

        Assert.Equal(100, fight.State.Enemies[0].Hp);
    }

    /// <summary>
    /// Parrying Shield is AfterSideTurnEnd where Cloak Clasp is Before, so a full hand can
    /// push the player over the ten-block line and arm it.
    /// </summary>
    [Fact]
    public void CloakClaspsBlockCanArmParryingShield()
    {
        var fight = Fight
            .Hand(Enumerable.Repeat(Card(IC.DefendIronclad), 10).ToArray())
            .Enemy(hp: 100);
        fight.State.Relics.Add(new RelicInstance(RelicEffects.CloakClasp));
        fight.State.Relics.Add(new RelicInstance(RelicEffects.ParryingShield));

        fight.EndTurn();

        Assert.Equal(94, fight.State.Enemies[0].Hp);
    }
}
