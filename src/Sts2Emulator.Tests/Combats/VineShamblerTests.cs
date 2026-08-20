using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// VineShamblerNormal: one Vine Shambler. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/VineShambler: HP a flat 64 at A8 (61 below), and a
/// three-move ring entered at SWIPE (MultiAttack 2x6, 2x7 at A9), then GRASPING_VINES
/// (8 damage, 9 at A9, plus a CardDebuffIntent) and CHOMP (16, 18 at A9).
/// </summary>
public class VineShamblerTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.VineShambler, ascension);

    [Fact]
    public void RosterIsOneShambler()
    {
        var fight = Encounter();

        Assert.Equal([KE.VineShambler], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsFixedAtSixtyFour()
    {
        var fight = Encounter();

        Assert.Equal(64, fight.State.Enemies[0].MaxHp);
    }

    [Fact]
    public void HpIsLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.Equal(61, fight.State.Enemies[0].MaxHp);
    }

    /// <summary>Swipe announces its total, two hits of six.</summary>
    [Fact]
    public void OpensOnSwipe()
    {
        var fight = Encounter();

        Assert.Equal([(IntentType.Attack, 12)], fight.Intents);
    }

    [Fact]
    public void MovesRunTheirRingInOrder()
    {
        var fight = Encounter();
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 4; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [
                (IntentType.Attack, 12),
                (IntentType.Attack, 8),
                (IntentType.Attack, 16),
                (IntentType.Attack, 12),
            ],
            announced
        );
    }

    [Fact]
    public void EveryMoveHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        var announced = new List<(IntentType, int)>();

        for (int turn = 0; turn < 3; turn++)
        {
            announced.Add(fight.Intents.First());
            fight.EndTurn();
        }

        Assert.Equal(
            [(IntentType.Attack, 14), (IntentType.Attack, 9), (IntentType.Attack, 18)],
            announced
        );
    }

    /// <summary>
    /// Swipe is two hits, which Thorns counts and block cannot: block absorbs the same
    /// total whether the twelve arrives once or twice.
    /// </summary>
    [Fact]
    public void SwipeLandsAsTwoSeparateHits()
    {
        var fight = Encounter();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Thorns, 1);
        int enemyHpBefore = fight.State.Enemies[0].Hp;

        fight.EndTurn();

        Assert.Equal(2, enemyHpBefore - fight.State.Enemies[0].Hp);
    }

    /// <summary>
    /// GraspingVinesMove's CardDebuffIntent is Tangled, which taxes Attacks by one energy.
    /// TangledPower.AfterSideTurnEnd removes it when its owner's side turn ends — the
    /// player's — so it has to survive the enemy turn that applied it and last through the
    /// whole player turn after. The emulator used to clear it at the start of that turn,
    /// which meant it never taxed a single card.
    /// </summary>
    /// <summary>
    /// GRASPING_VINES lists SingleAttackIntent first and CardDebuffIntent second, so the
    /// game announces an Attack and carries the debuff alongside. The emulator announced a
    /// Debuff; a live sweep caught it, reporting emu (Debuff, 8) against live (Attack, 8).
    /// </summary>
    [Fact]
    public void GraspingVinesAnnouncesAnAttackWithADebuffBeside()
    {
        var fight = Encounter();
        fight.EndTurn();

        Assert.Equal((IntentType.Attack, 8), fight.Intents.First());
        Assert.Equal(IntentType.Debuff, fight.State.Enemies[0].SecondaryIntent?.Type);
    }

    [Fact]
    public void GraspingVinesTaxesTheFollowingPlayerTurn()
    {
        var fight = Encounter();
        fight.EndTurn(); // Swipe
        fight.EndTurn(); // Grasping Vines

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Tangled));

        fight.State.Hand = TestDeck.Pile(IC.StrikeIronclad);
        fight.Energy(1);
        fight.Play();

        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void TangledIsGoneByTheTurnAfterThat()
    {
        var fight = Encounter();
        fight.EndTurn(); // Swipe
        fight.EndTurn(); // Grasping Vines
        fight.EndTurn(); // Chomp — applies nothing

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Tangled));
    }
}
