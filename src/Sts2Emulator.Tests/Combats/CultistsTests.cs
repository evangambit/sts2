using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// CultistsNormal: a Calcified Cultist and a Damp Cultist. Read off
/// MegaCrit.Sts2.Core.Models.Monsters: both open on INCANTATION (a buff) and then
/// DARK_STRIKE forever — the strike's FollowUpState is itself, so the buff happens once.
/// Calcified is HP 39-42 with Incantation 2 and DarkStrike 9 (11 at A9); Damp is HP 52-54
/// with Incantation 5 (6 at A9) and DarkStrike 1 (3 at A9).
/// </summary>
public class CultistsTests
{
    private static Fight Encounter(int ascension = Ascension.DefaultLevel) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.Cultists, ascension);

    [Fact]
    public void RosterIsACalcifiedAndADampCultist()
    {
        var fight = Encounter();

        Assert.Equal([KE.CalcifiedCultist, KE.DampCultist], fight.EnemyDefIds);
    }

    [Fact]
    public void HpIsRolledInsideTheDeclaredBands()
    {
        var fight = Encounter();

        Assert.InRange(fight.State.Enemies[0].MaxHp, 39, 42);
        Assert.InRange(fight.State.Enemies[1].MaxHp, 52, 54);
    }

    [Fact]
    public void HpBandsAreLowerBelowAscensionEight()
    {
        var fight = Encounter(ascension: 7);

        Assert.InRange(fight.State.Enemies[0].MaxHp, 38, 41);
        Assert.InRange(fight.State.Enemies[1].MaxHp, 51, 53);
    }

    [Fact]
    public void BothOpenOnIncantation()
    {
        var fight = Encounter();

        Assert.All(fight.Intents, intent => Assert.Equal(IntentType.Buff, intent.Type));
    }

    /// <summary>
    /// DARK_STRIKE.FollowUpState is itself, so after the opening ritual neither cultist
    /// ever buffs again — a cycling implementation would re-buff on turn three.
    /// </summary>
    [Fact]
    public void IncantationHappensOnceAndThenTheyStrikeForever()
    {
        var fight = Encounter();
        fight.EndTurn();

        for (int turn = 0; turn < 4; turn++)
        {
            Assert.All(fight.Intents, intent => Assert.Equal(IntentType.Attack, intent.Type));
            fight.EndTurn();
        }
    }

    /// <summary>
    /// Incantation applies RitualPower, not Strength directly — Ritual hands over its
    /// Strength at the end of each turn, skipping the one it was applied on. Damp's
    /// IncantationAmount is 5 at A8 and 6 only from A9; the emulator gave 6 at every level
    /// and its own comment said "deadly ascension value" while doing it.
    /// </summary>
    [Fact]
    public void IncantationGivesRitualWhichPaysOutATurnLater()
    {
        var fight = Encounter();
        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Ritual));
        Assert.Equal(5, BuffSystem.Get(fight.State.Enemies[1].Buffs, BuffId.Ritual));
        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Strength));
        Assert.Equal(5, BuffSystem.Get(fight.State.Enemies[1].Buffs, BuffId.Strength));
    }

    [Fact]
    public void DampRitualIsLargerAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal(6, BuffSystem.Get(fight.State.Enemies[1].Buffs, BuffId.Ritual));
    }

    /// <summary>
    /// Dark Strike is 9 for Calcified and 1 for Damp at A8 — the emulator had Damp at a
    /// flat 3, its A9 value, so the weaker cultist hit three times as hard as it should.
    /// </summary>
    [Fact]
    public void DarkStrikeUsesTheAscensionEightDamage()
    {
        var fight = Encounter();
        fight.EndTurn();

        Assert.Equal([(IntentType.Attack, 9), (IntentType.Attack, 1)], fight.Intents);
    }

    [Fact]
    public void DarkStrikeHitsHarderAtAscensionNine()
    {
        var fight = Encounter(ascension: 9);
        fight.EndTurn();

        Assert.Equal([(IntentType.Attack, 11), (IntentType.Attack, 3)], fight.Intents);
    }

    /// <summary>
    /// AttackIntent.GetSingleDamage runs the move through Hook.ModifyDamage before showing
    /// it, so what the player reads grows with the Ritual the cultist has been stacking.
    /// The observation is where a policy reads it, and it carried the raw move damage.
    /// </summary>
    [Fact]
    public void TheAnnouncedDamageGrowsWithRitual()
    {
        var fight = Encounter();
        fight.EndTurn();
        fight.EndTurn();

        Span<int> obs = stackalloc int[CombatObservation.ObsSize];
        CombatObservation.Write(fight.State, obs);
        int announced = obs[
            CombatObservation.EnemyOffset + CombatObservation.EnemyIntentField + 1
        ];

        // Strength 2 from the first Ritual payout, on top of the printed 9.
        Assert.Equal(11, announced);
        Assert.Equal((IntentType.Attack, 11), fight.Intents.First());
    }
}
