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

/// <summary>
/// The four Wrigglers InfestedPower spawns when the parasite dies. Read off
/// MegaCrit.Sts2.Core.Models.Monsters/Wriggler: HP 18-22 at A8, BiteDamage 6 below A9,
/// and an INIT_MOVE that branches on the creature's slot — wriggler1 and wriggler3 open
/// on NASTY_BITE, wriggler2 and wriggler4 on WRIGGLE.
///
/// Confirmed against a live A8 capture reached by stacking the deck with Bludgeons: the
/// pack announces bite, buff, bite, buff and swaps every turn.
/// </summary>
public class WrigglerTests
{
    /// <summary>
    /// InfestedPower spawns them from the parasite's death, so the parasite has to die
    /// during a turn — setting it to 0 HP beforehand is not a death, it is a corpse.
    /// </summary>
    private static Fight KillTheParasite()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.PhrogParasite);
        fight.State.Hand = [TestDeck.Card(IC.StrikeIronclad)];
        fight.State.Energy = 3;
        fight.Enemy0.Hp = 1;
        fight.Play();
        return fight;
    }

    [Fact]
    public void FourSpawnWhenTheParasiteDies()
    {
        var fight = KillTheParasite();

        Assert.Equal(4, fight.State.Enemies.Count(enemy => enemy.DefId == KE.Wriggler));
    }

    [Fact]
    public void TheyOpenInTwoPhases()
    {
        var fight = KillTheParasite();

        // Stunned on arrival — Wriggler is the one monster in the game that sets
        // StartStunned — so the announcements to read are the turn after.
        fight.EndTurn();

        var wrigglers = fight
            .State.Enemies.Where(enemy => enemy.DefId == KE.Wriggler)
            .Select(enemy => enemy.CurrentIntent.Type)
            .ToList();
        Assert.Equal(
            [IntentType.Attack, IntentType.Buff, IntentType.Attack, IntentType.Buff],
            wrigglers
        );
    }

    /// <summary>
    /// WRIGGLE_MOVE adds an Infection to the discard, the same status the parasite deals
    /// three of — not a Dazed. It matters twice over: Infection burns for 3 in hand at
    /// end of turn, and the card that is NOT added is a card the player would have drawn.
    /// </summary>
    [Fact]
    public void WriggleAddsAnInfection()
    {
        var fight = KillTheParasite();
        int before = InfectionsInPlay(fight);

        // Past the stunned turn, then the turn the wrigglers on the buff phase act.
        fight.EndTurn();
        fight.EndTurn();

        // Counted across every pile: a reshuffle moves them out of the discard, and one
        // held in hand at end of turn burns for 3 and stays there.
        Assert.Equal(before + 2, InfectionsInPlay(fight));
        Assert.DoesNotContain(AllCards(fight), card => card.DefId == ST.Dazed);
    }

    private static List<CardInstance> AllCards(Fight fight) =>
        [
            .. fight.State.Hand,
            .. fight.State.DrawPile,
            .. fight.State.DiscardPile,
            .. fight.State.ExhaustPile,
        ];

    private static int InfectionsInPlay(Fight fight) =>
        AllCards(fight).Count(card => card.DefId == ST.Infection);

    [Fact]
    public void EachTakesAnHpNoLivingEnemyHolds()
    {
        var fight = KillTheParasite();

        var hps = fight
            .State.Enemies.Where(enemy => enemy.DefId == KE.Wriggler)
            .Select(enemy => enemy.MaxHp)
            .ToList();
        Assert.Equal(4, hps.Count);
        Assert.Equal(hps.Count, hps.Distinct().Count());
        Assert.All(hps, hp => Assert.InRange(hp, 18, 22));
    }
}
