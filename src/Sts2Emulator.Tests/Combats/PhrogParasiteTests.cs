using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// PhrogParasiteElite: one Phrog Parasite, alternating Infect and Lash. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/PhrogParasite: HP 66-68 at A8 (ToughEnemies is
/// live there), LashDamage 4 below A9 for four hits, and INFECT_MOVE adding three
/// Infections to the discard pile.
///
/// The Infections are the whole fight: each one deals 3 to the player at the end of a
/// turn it is held in hand, so the parasite's real damage arrives several turns after
/// the move that dealt it.
/// </summary>
public class PhrogParasiteTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.PhrogParasite, ascension);

    [Fact]
    public void RosterIsOneParasite()
    {
        var fight = Encounter();

        Assert.Equal([KE.PhrogParasite], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBand()
    {
        var fight = Encounter();

        Assert.InRange(fight.Enemy0.MaxHp, 66, 68);
    }

    [Fact]
    public void AlternatesInfectAndLash()
    {
        var fight = Encounter();
        var announced = new List<(IntentType Type, int Magnitude)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.Single());
            fight.EndTurn();
        }

        // Lash announces its four hits as one number: 4 x 4 at A8.
        Assert.Equal(
            [
                (IntentType.Debuff, 3),
                (IntentType.Attack, 16),
                (IntentType.Debuff, 3),
                (IntentType.Attack, 16),
            ],
            announced
        );
    }

    [Fact]
    public void LashHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 20), fight.Intents.Single());
    }

    [Fact]
    public void InfectPutsThreeInfectionsInTheDiscardPile()
    {
        var fight = Encounter();
        Assert.Equal(IntentType.Debuff, fight.Intents.Single().Type);

        fight.EndTurn();

        Assert.Equal(3, fight.State.DiscardPile.Count(card => card.DefId == ST.Infection));
    }
}
