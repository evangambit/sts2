using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The five orbs, read against <c>MegaCrit.Sts2.Core.Models.Orbs/*.cs</c>.
/// </summary>
/// <remarks>
/// Defect is 88 cards and most of them channel, evoke or count orbs, so the orbs are the
/// foundation the rest of the character is tested on top of. Pinning them first is the
/// point: a card test written against a wrong orb passes and cements the wrong answer.
///
/// Two rules are worth holding in mind while reading these. `ModifyOrbValue` is where
/// Focus enters, and it is applied per READ rather than stored — so Focus gained after an
/// orb was channelled still raises it. And it is NOT applied everywhere: Plasma declares
/// plain literals at both ends, and Dark's evoke value is a bare field.
/// </remarks>
public class LightningOrbTests
{
    // PassiveVal 3, EvokeVal 8, both ModifyOrbValue'd. Unpowered damage to ONE enemy
    // rolled off the combat-targets stream. BeforeTurnEndOrbTrigger, so the passive is an
    // end-of-turn effect.
    [Fact]
    public void ThePassiveHitsForThreeAtTheEndOfTheTurn()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        fight.EndTurn();

        Assert.Equal(200 - 3, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheEvokeHitsForEight()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(200 - 8, fight.Enemy0.Hp);
        Assert.Empty(fight.State.Orbs);
    }

    /// <summary>Focus raises both ends, because both go through `ModifyOrbValue`.</summary>
    [Fact]
    public void FocusRaisesThePassiveAndTheEvoke()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 2);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(200 - 10, fight.Enemy0.Hp);
    }

    /// <summary>
    /// The evoke rolls its target, as `ApplyLightningDamage` does when handed a null
    /// target. It used to hit the aimed-at enemy, so every evoke in a three-creature fight
    /// landed on the same one and the target stream was never drawn from.
    /// </summary>
    [Fact]
    public void TheEvokeRollsItsTarget()
    {
        bool everySeedHitTheSame = true;
        for (int seed = 0; seed < 8; seed++)
        {
            var fight = Fight.Encounter(CombatFactory.ActOneEncounter.Bowlbugs, seed: seed);
            var before = fight.State.Enemies.Select(e => e.Hp).ToList();
            for (int i = 0; i < 3; i++)
            {
                CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
                CardEffects.EvokeNextOrb(fight.State, fight.State.TargetRng);
            }

            if (fight.State.Enemies.Where((e, i) => e.Hp < before[i]).Count() > 1)
            {
                everySeedHitTheSame = false;
                break;
            }
        }

        Assert.False(everySeedHitTheSame, "every evoke landed on the same enemy");
    }
}

public class FrostOrbTests
{
    // PassiveVal 2, EvokeVal 5, both ModifyOrbValue'd. Unpowered block, so Dexterity does
    // not touch it. BeforeTurnEndOrbTrigger.
    [Fact]
    public void ThePassiveBlocksTwoAtTheEndOfTheTurn()
    {
        // Block gained at the end of a turn is spent on the enemies and then cleared at
        // the start of the next one, so reading it off `PlayerBlock` after `EndTurn` sees
        // zero however the orb behaved. Barricade holds it still, and the dummy enemy has
        // no moves to spend it on.
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Barricade, 1);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.EndTurn();

        Assert.Equal(2, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheEvokeBlocksFive()
    {
        var fight = Fight.Hand().Energy(3);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(5, fight.State.PlayerBlock);
    }

    /// <summary>The block is `ValueProp.Unpowered`, so Dexterity does not raise it.</summary>
    [Fact]
    public void DexterityDoesNotRaiseItButFocusDoes()
    {
        var fight = Fight.Hand().Energy(3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 5);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 2);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(7, fight.State.PlayerBlock);
    }
}

/// <summary>
/// Dark orbs bank their passive into their own evoke, and Focus reaches only one half.
/// </summary>
/// <remarks>
/// `_evokeVal` starts at a bare `6m` — no `ModifyOrbValue` on the field or on the
/// `EvokeVal` property — while `PassiveVal => ModifyOrbValue(6m)` is. So Focus raises what
/// a Dark orb ACCUMULATES and not what it starts with, and the emulator seeded the base
/// with Focus as well.
/// </remarks>
public class DarkOrbTests
{
    [Fact]
    public void ItStartsAtSixAndBanksSixPerTurn()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);
        Assert.Equal(6, fight.State.Orbs[0].EvokeValue);

        fight.EndTurn();
        Assert.Equal(12, fight.State.Orbs[0].EvokeValue);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));
        Assert.Equal(200 - 12, fight.Enemy0.Hp);
    }

    /// <summary>Focus is in the accumulation and not in the base: 6, then 6 + 3.</summary>
    [Fact]
    public void FocusRaisesWhatItBanksAndNotWhatItStartsWith()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 3);
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);

        Assert.Equal(6, fight.State.Orbs[0].EvokeValue);

        fight.EndTurn();

        Assert.Equal(6 + 9, fight.State.Orbs[0].EvokeValue);
    }

    /// <summary>The evoke picks the WEAKEST enemy — `hittableEnemies.MinBy(c => c.CurrentHp)`.</summary>
    [Fact]
    public void TheEvokeHitsTheWeakestEnemy()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.Enemy(hp: 30);
        fight.Enemy(hp: 100);
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(200, fight.Enemy0.Hp);
        Assert.Equal(30 - 6, fight.Enemy1.Hp);
    }
}

public class PlasmaOrbTests
{
    // PassiveVal 1 and EvokeVal 2, both PLAIN LITERALS -- Plasma is the one orb Focus does
    // not touch. And it is AfterTurnStartOrbTrigger, so it pays at the start of the turn
    // rather than the end like the other four.
    [Fact]
    public void ItPaysEnergyAtTheStartOfTheTurn()
    {
        var fight = Fight.Hand().Energy(3);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Plasma);

        fight.EndTurn();

        // Max energy for the turn plus the orb's one.
        Assert.Equal(fight.State.MaxEnergy + 1, fight.State.Energy);
    }

    [Fact]
    public void TheEvokeGivesTwo()
    {
        var fight = Fight.Hand().Energy(3);
        CardEffects.ChannelOrb(fight.State, OrbType.Plasma);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(5, fight.State.Energy);
    }

    [Fact]
    public void FocusDoesNotTouchIt()
    {
        var fight = Fight.Hand().Energy(3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 5);
        CardEffects.ChannelOrb(fight.State, OrbType.Plasma);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(5, fight.State.Energy);
    }
}

/// <summary>
/// Glass orbs are a decaying area attack. The emulator had them drawing cards.
/// </summary>
/// <remarks>
/// `GlassOrb.Passive` deals its value to every hittable enemy and then knocks its own
/// `_passiveVal` down by one, floored at zero; `EvokeVal => PassiveVal * 2m`. The emulator
/// drew one card on the passive and two on the evoke — an invented effect on an orb whose
/// whole identity is that it wears out. The two-card draw also ran off a fresh
/// `new Random(0)`, so it did not even draw from the combat's stream.
/// </remarks>
public class GlassOrbTests
{
    [Fact]
    public void ThePassiveHitsEveryEnemyAndDecays()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Glass);
        Assert.Equal(4, fight.State.Orbs[0].PassiveValue);

        fight.EndTurn();
        Assert.Equal(200 - 4, fight.Enemy0.Hp);
        Assert.Equal(200 - 4, fight.Enemy1.Hp);
        Assert.Equal(3, fight.State.Orbs[0].PassiveValue);

        fight.EndTurn();
        Assert.Equal(200 - 4 - 3, fight.Enemy0.Hp);
    }

    /// <summary>The evoke is twice the current value, so it shrinks with the orb.</summary>
    [Fact]
    public void TheEvokeIsTwiceTheCurrentValue()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Glass);
        fight.EndTurn(); // decays 4 -> 3

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        Assert.Equal(200 - 4 - 6, fight.Enemy0.Hp);
        Assert.Equal(200 - 4 - 6, fight.Enemy1.Hp);
    }

    /// <summary>
    /// Worn out, it does nothing at all — the passive skips its whole body when the value
    /// is not above zero, so it neither hits nor decays further.
    /// </summary>
    [Fact]
    public void AWornOutOrbDoesNothing()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Glass);
        for (int i = 0; i < 4; i++)
        {
            fight.EndTurn();
        }

        Assert.Equal(0, fight.State.Orbs[0].PassiveValue);
        int hp = fight.Enemy0.Hp;

        fight.EndTurn();
        Assert.Equal(hp, fight.Enemy0.Hp);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));
        Assert.Equal(hp, fight.Enemy0.Hp);
    }

    /// <summary>But Focus revives one: the decayed value has Focus added when it is read.</summary>
    [Fact]
    public void FocusIsAddedToTheDecayedValue()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Glass);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 2);

        fight.EndTurn();

        Assert.Equal(200 - 6, fight.Enemy0.Hp);
    }
}

/// <summary>
/// Channelling into a full ring evokes the OLDEST orb to make room — `state.Orbs` is a
/// queue, and the front of it is what a "next orb" effect acts on.
/// </summary>
public class OrbChannelTests
{
    [Fact]
    public void ChannellingIntoAFullRingEvokesTheOldest()
    {
        var fight = Fight.Hand().Energy(3).Enemy(hp: 200);
        Assert.Equal(3, fight.State.OrbCapacity);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        CardEffects.ChannelOrb(fight.State, OrbType.Frost, new Random(0));

        // The Lightning was at the front, so it evoked on its way out.
        Assert.Equal(200 - 8, fight.Enemy0.Hp);
        Assert.Equal(
            [OrbType.Frost, OrbType.Frost, OrbType.Frost],
            fight.State.Orbs.Select(o => o.Type)
        );
    }

    [Fact]
    public void WithNoSlotsNothingIsChannelled()
    {
        var fight = Fight.Hand().Energy(3);
        fight.State.OrbCapacity = 0;

        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        Assert.Empty(fight.State.Orbs);
    }
}
