using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// HauntedShipNormal: one Haunted Ship. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/HauntedShip: HP 67 at A8 (63 below), opening on
/// HAUNT (a DebuffIntent plus StatusIntent(5)), then SWIPE (13, 14 at A9) and STOMP
/// (MultiAttack 3x4, 3x5 at A9) alternating forever — HAUNT never comes round again.
/// </summary>
public class HauntedShipTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.HauntedShip, ascension);

    [Fact]
    public void RosterIsOneShip()
    {
        var fight = Encounter();

        Assert.Equal([KE.HauntedShip], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtSixtySeven()
    {
        var fight = Encounter();

        Assert.Equal(67, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(63, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void OpensOnHaunt()
    {
        var fight = Encounter();

        Assert.Equal([(IntentType.Debuff, 5)], fight.Intents);
    }

    /// <summary>
    /// HAUNT's follow-up is SWIPE and the two attacks then point at each other, so the
    /// opening debuff happens once per combat and never again.
    /// </summary>
    [Fact]
    public void SwipeAndStompAlternateAfterTheOpeningHaunt()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 5; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [
                (IntentType.Debuff, 5),
                (IntentType.Attack, 13),
                (IntentType.Attack, 12),
                (IntentType.Attack, 13),
                (IntentType.Attack, 12),
            ],
            announced
        );
    }

    /// <summary>
    /// StompDamage x StompRepeat is three separate hits, and the intent announces their
    /// total. Block cannot tell the difference — it absorbs the same amount either way —
    /// so this counts Thorns retaliations, which fire once per hit.
    /// </summary>
    [Fact]
    public void StompLandsAsThreeSeparateHits()
    {
        var fight = Encounter();
        fight.EndTurn(); // Haunt
        fight.EndTurn(); // Swipe

        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Thorns, 1);
        int enemyHpBefore = fight.State.Enemies[0].Hp;
        int playerHpBefore = fight.State.PlayerHp;
        fight.EndTurn(); // Stomp

        Assert.Equal(3, enemyHpBefore - fight.State.Enemies[0].Hp);
        Assert.Equal(playerHpBefore - 12, fight.State.PlayerHp);
    }

    [Fact]
    public void HauntAppliesWeakAndFiveDazed()
    {
        var fight = Encounter();
        fight.EndTurn();

        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Weak));
        Assert.Equal(5, fight.State.DiscardPile.Count(card => card.DefId == ST.Dazed));
    }
}
