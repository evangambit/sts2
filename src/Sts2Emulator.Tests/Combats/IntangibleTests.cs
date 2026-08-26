using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// <c>IntangiblePower</c> caps everything that takes HP off its owner at 1.
/// </summary>
/// <remarks>
/// It was applied in ELEVEN places and read in NONE — six player cards (Wraith Form,
/// Shadow Step and four more), the Soul Fysh's INTANGIBLE move and the Test Subject's
/// Nemesis toggle — so every one of them was a no-op, and the whole suite stayed green
/// when the cap was added because nothing had ever asserted it. That is the same shape as
/// the Aeonglass's invented `BuffId.Ebb` (E116), two orders of magnitude larger.
///
/// The power carries two hooks and both land on 1. `ModifyDamageCap` runs inside
/// `Hook.ModifyDamage` under the `Cap` flag, which `ModifyDamageHookType.All` includes —
/// so it governs the damage NUMBER, and reaches the intent READOUT as well as the blow.
/// `ModifyHpLostAfterOsty` is the backstop on HP itself, for anything that takes HP by a
/// route that is not an attack.
/// </remarks>
public class IntangibleTests
{
    private static Fight Fresh()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Axebot);
        fight.State.PlayerHp = 500;
        fight.State.PlayerBlock = 0;
        return fight;
    }

    /// <summary>
    /// An intangible player takes 1 from an attack that would otherwise land for twelve —
    /// and the block it would have eaten is capped with it, since `ModifyDamageCap` runs
    /// before block is applied.
    /// </summary>
    [Fact]
    public void AnAttackOnAnIntangiblePlayerLandsForOne()
    {
        var fight = Fresh();
        var bot = fight.State.Enemies[0];
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Intangible, 2);
        fight.State.PlayerBlock = 10;

        // Driven directly rather than through EndTurn: block clears at the start of the
        // next player turn, so a test that ends the turn cannot see what the blow ate.
        EnemyAI.ExecuteIntent(bot, fight.State, new Random(0)); // HAMMER_UPPERCUT, 12 at A8

        Assert.Equal(500, fight.State.PlayerHp);
        Assert.Equal(9, fight.State.PlayerBlock);
    }

    /// <summary>
    /// The READOUT is capped too. <c>AttackIntent.GetSingleDamage</c> runs the move through
    /// the same hook, so an intangible player is told the enemy will hit them for 1 — and
    /// for a multi-hit attack, once per hit.
    /// </summary>
    [Fact]
    public void TheIntentAnnouncesTheCappedNumber()
    {
        var fight = Fresh();
        var bot = fight.State.Enemies[0];
        bot.MoveIndex = 2; // ONE_TWO: two hits of nine at A8
        EnemyAI.ChooseIntents([bot], 0, new Random(0), ascension: 8);

        Assert.Equal(18, bot.CurrentIntent.AnnouncedDamage(bot.Buffs, fight.State.PlayerBuffs));

        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Intangible, 1);

        Assert.Equal(2, bot.CurrentIntent.AnnouncedDamage(bot.Buffs, fight.State.PlayerBuffs));
    }

    /// <summary>An intangible ENEMY takes 1 from a card, the same way.</summary>
    [Fact]
    public void ACardOnAnIntangibleEnemyLandsForOne()
    {
        var fight = Fresh();
        var bot = fight.State.Enemies[0];
        bot.Block = 0;
        BuffSystem.Apply(bot.Buffs, BuffId.Intangible, 1);
        fight.State.Hand = [new CardInstance(IC.StrikeIronclad, false)];
        fight.State.Energy = 3;

        int before = bot.Hp;
        fight.Play(0, target: 0);

        Assert.Equal(before - 1, bot.Hp);
    }

    /// <summary>
    /// Poison too: <c>PoisonPower</c> runs its own damage through
    /// <c>Hook.ModifyDamage(..., All, ...)</c>, so the cap catches it like any other.
    /// </summary>
    [Fact]
    public void PoisonOnAnIntangibleCreatureTicksForOne()
    {
        var fight = Fresh();
        var bot = fight.State.Enemies[0];
        BuffSystem.Apply(bot.Buffs, BuffId.Poison, 20);
        BuffSystem.Apply(bot.Buffs, BuffId.Intangible, 3);

        int before = bot.Hp;
        fight.EndTurn();

        Assert.Equal(before - 1, bot.Hp);
    }

    /// <summary>
    /// It counts down at the end of the ENEMY side turn, whoever owns it — and it is a
    /// Buff, so it does not get the skip-a-tick grace a debuff landing on the player has.
    /// </summary>
    [Fact]
    public void ItCountsDownAtTheEndOfTheEnemyTurn()
    {
        var fight = Fresh();
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Intangible, 2);

        fight.EndTurn();
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Intangible));

        fight.State.PlayerHp = 500;
        fight.EndTurn();
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Intangible));

        // And the cap goes with it: the next uppercut lands in full.
        fight.State.PlayerHp = 500;
        fight.State.PlayerBlock = 0;
        fight.EndTurn();
        Assert.True(fight.State.PlayerHp < 500);
    }

    /// <summary>
    /// The Test Subject's third form carries NemesisPower, which toggles Intangible at the
    /// end of every enemy turn — so it is untouchable on alternate player turns rather
    /// than permanently, which is what the emulator used to give it.
    /// </summary>
    [Fact]
    public void NemesisAlternatesRatherThanStaying()
    {
        var fight = Fight.Encounter(CombatFactory.ActOneEncounter.TestSubject);
        var subject = fight.State.Enemies[0];
        BuffSystem.Remove(subject.Buffs, BuffId.Adaptable);
        BuffSystem.Apply(subject.Buffs, BuffId.Nemesis, 1);
        subject.MoveIndex = 4;
        fight.State.PlayerHp = 9999;

        var seen = new List<bool>();
        for (int turn = 0; turn < 4; turn++)
        {
            fight.State.PlayerHp = 9999;
            subject.Hp = 9999;
            fight.EndTurn();
            seen.Add(BuffSystem.Get(subject.Buffs, BuffId.Intangible) > 0);
        }

        Assert.Equal([true, false, true, false], seen);
    }
}
