using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `CardModel.OnTurnEndInHand`: what a card does to its holder while it sits in hand at
/// the end of the turn. ELEVEN cards have one, and the emulator modelled five.
/// </summary>
/// <remarks>
/// The list was hand-kept — four ids that "burn the holder for the card's own damage
/// value", with Beckon handled beside them. Six were missing, and the SHAPE was wrong as
/// well as the membership: Debt takes gold, Doubt applies Weak, Shame applies Frail, and
/// Regret's damage is the size of the hand. `HasTurnEndInHandEffect` is extracted now, so
/// which cards belong is data and only what they do is written.
/// </remarks>
public class TurnEndStatusTests
{
    /// <summary>
    /// The hand, against a punching bag. `.Enemy()` rather than a real encounter: an
    /// unattended fight has the enemies hitting for sixteen a turn, which lands on top of
    /// whatever the card did and is not what any of these tests is measuring.
    /// </summary>
    private static Fight Holding(params int[] cardIds)
    {
        var fight = Fight.Hand(cardIds.Select(id => new CardInstance(id, false)).ToArray()).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        return fight;
    }

    private static (int Hp, int Gold, int Weak, int Frail) Snapshot(Fight fight) =>
        (
            fight.State.PlayerHp,
            fight.State.PlayerGold,
            BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak),
            BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail)
        );

    /// <summary>
    /// Every card the game flags is one the emulator answers. This is the test the old
    /// hand-kept list could not have: it asks the DATA which cards belong.
    /// </summary>
    [Fact]
    public void EveryFlaggedCardHasAnEffect()
    {
        var flagged = GeneratedData.Cards.All.ToArray().Where(def => def.TurnEndInHand).ToList();

        Assert.Equal(11, flagged.Count);

        foreach (var def in flagged)
        {
            var fight = Holding(def.Id);
            // Debt takes `Min(10, Gold)`, so a broke player is a player it cannot touch --
            // correct, and indistinguishable from "not modelled" unless there is gold.
            fight.State.PlayerGold = 100;
            var before = Snapshot(fight);
            fight.EndTurn();

            Assert.True(
                Snapshot(fight) != before,
                $"{def.Name} is flagged HasTurnEndInHandEffect and changed nothing"
            );
        }
    }

    /// <summary>The five that deal the card's own DamageVar.</summary>
    [Theory]
    [InlineData("Burn", 2)]
    [InlineData("Decay", 2)]
    [InlineData("Infection", 3)]
    [InlineData("Wither", 3)]
    [InlineData("Toxic", 5)]
    public void TheDamagingOnesDealTheirPrintedDamage(string name, int damage)
    {
        int id = GeneratedData.Cards.FindId(name)!.Value;
        var fight = Holding(id);
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - damage, fight.State.PlayerHp);
    }

    /// <summary>
    /// Those five are BLOCKABLE — `ValueProp.Unpowered | Move`, with no Unblockable — so
    /// block put up during the turn absorbs them.
    /// </summary>
    [Fact]
    public void TheDamagingOnesAreBlockable()
    {
        var fight = Holding(ST.Burn);
        fight.State.PlayerBlock = 20;
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before, fight.State.PlayerHp);
    }

    [Fact]
    public void StacksOncePerCopyHeld()
    {
        var fight = Holding(ST.Infection, ST.Infection, ST.Infection);
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - 9, fight.State.PlayerHp);
    }

    /// <summary>
    /// Beckon and Bad Luck lose HP directly — Unblockable and Unpowered, so block does
    /// not save you.
    /// </summary>
    [Theory]
    [InlineData("Beckon", 6)]
    [InlineData("BadLuck", 13)]
    public void TheUnblockableOnesGoStraightThroughBlock(string name, int loss)
    {
        int id = GeneratedData.Cards.FindId(name)!.Value;
        var fight = Holding(id);
        fight.State.PlayerBlock = 50;
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before - loss, fight.State.PlayerHp);
    }

    /// <summary>
    /// `Debt`: `Min(GoldVar(10), Owner.Gold)` — it cannot put the run into debt, which is
    /// the joke in the name.
    /// </summary>
    [Theory]
    [InlineData(50, 40)]
    [InlineData(10, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 0)]
    public void DebtTakesTenOrEverythingWhicheverIsLess(int gold, int left)
    {
        var fight = Holding(ST.Debt);
        fight.State.PlayerGold = gold;

        fight.EndTurn();

        Assert.Equal(left, fight.State.PlayerGold);
    }

    /// <summary>`Doubt` applies Weak 1, `Shame` applies Frail 1.</summary>
    [Fact]
    public void DoubtAndShameApplyTheirDebuffs()
    {
        var doubt = Holding(ST.Doubt);
        var shame = Holding(ST.Shame);

        doubt.EndTurn();
        shame.EndTurn();

        Assert.Equal(1, BuffSystem.Get(doubt.State.PlayerBuffs, BuffId.Weak));
        Assert.Equal(1, BuffSystem.Get(shame.State.PlayerBuffs, BuffId.Frail));
    }

    /// <summary>
    /// A FRESH stack survives the tick that happens moments later — the game sets
    /// `SkipNextDurationTick` when the player did not already have it, and the emulator's
    /// round-start snapshot says the same thing from the other side. Without it the debuff
    /// would land and expire in the same breath and be worth nothing at all.
    /// </summary>
    [Fact]
    public void AFreshStackSurvivesTheSameTurnsTick()
    {
        var fight = Holding(ST.Doubt);

        fight.EndTurn();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
    }

    /// <summary>
    /// `Regret`'s damage is the SIZE OF THE HAND, snapshotted before the turn-end sequence
    /// starts — not a number on the card.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void RegretHitsForTheWholeHand(int handSize)
    {
        var fight = Holding(ST.Regret);
        for (int i = 1; i < handSize; i++)
        {
            fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        }

        fight.State.PlayerBlock = 50;
        int before = fight.State.PlayerHp;
        fight.EndTurn();

        // Unblockable, so the block does not soften it.
        Assert.Equal(before - handSize, fight.State.PlayerHp);
    }

    /// <summary>
    /// A card that is not in HAND does nothing — the hook is `OnTurnEndInHand`, and the
    /// draw pile is not hand.
    /// </summary>
    [Fact]
    public void ACurseInTheDrawPileIsQuiet()
    {
        var fight = Holding();
        fight.State.DrawPile.Insert(0, new CardInstance(ST.BadLuck, false));
        int before = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(before, fight.State.PlayerHp);
    }
}

// The six curses below were not in `ImplementedCards` at all until this pass: their only
// behaviour is a turn-end hook, and the coverage generator scrapes the PLAY switch. Each
// gets its own class because that is what the gate keys on; the behaviour they share is
// exercised above.

/// <summary>Bad Luck: Unplayable, Eternal, 13 unblockable at turn end in hand.</summary>
public class BadLuckTests
{
    [Fact]
    public void ItCannotBeRemovedAndCannotBeBlocked()
    {
        var def = GeneratedData.Cards.Get(ST.BadLuck);

        Assert.True(def.Eternal, "Eternal is what makes it a punishment rather than a chore");
        Assert.True(def.TurnEndInHand);
        Assert.Equal(CardType.Curse, def.Type);
    }
}

/// <summary>Debt: takes gold, not HP -- the only turn-end card that does.</summary>
public class DebtTests
{
    [Fact]
    public void ItIsTheOnlyTurnEndCardThatTakesGold()
    {
        var fight = Fight.Hand(new CardInstance(ST.Debt, false)).Enemy();
        fight.State.PlayerGold = 100;
        int hp = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(90, fight.State.PlayerGold);
        Assert.Equal(hp, fight.State.PlayerHp);
    }
}

/// <summary>Decay: Burn's twin, as a Curse rather than a Status.</summary>
public class DecayTests
{
    [Fact]
    public void ItIsBurnAsACurse()
    {
        var decay = GeneratedData.Cards.Get(ST.Decay);
        var burn = GeneratedData.Cards.Get(ST.Burn);

        Assert.Equal(burn.BaseDamage, decay.BaseDamage);
        Assert.Equal(CardType.Curse, decay.Type);
        Assert.Equal(CardType.Status, burn.Type);
    }
}

/// <summary>Doubt: Weak 1 at turn end, and the fresh stack survives the tick.</summary>
public class DoubtTests
{
    [Fact]
    public void TheWeakItAppliesLastsIntoTheNextTurn()
    {
        var fight = Fight.Hand(new CardInstance(ST.Doubt, false)).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;

        fight.EndTurn();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
    }
}

/// <summary>Regret: damage equal to the size of the hand.</summary>
public class RegretTests
{
    [Fact]
    public void ABiggerHandHurtsMore()
    {
        var small = Fight.Hand(new CardInstance(ST.Regret, false)).Enemy();
        var big = Fight
            .Hand(
                new CardInstance(ST.Regret, false),
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.StrikeIronclad, false)
            )
            .Enemy();
        int smallHp = small.State.PlayerHp;
        int bigHp = big.State.PlayerHp;

        small.EndTurn();
        big.EndTurn();

        Assert.Equal(smallHp - 1, small.State.PlayerHp);
        Assert.Equal(bigHp - 3, big.State.PlayerHp);
    }
}

/// <summary>Shame: Frail 1 at turn end, Doubt's mirror.</summary>
public class ShameTests
{
    [Fact]
    public void ItAppliesFrailWhereDoubtAppliesWeak()
    {
        var fight = Fight.Hand(new CardInstance(ST.Shame, false)).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;

        fight.EndTurn();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Frail));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Weak));
    }
}

// The five that were already modelled and already in `Pending`. Their shared behaviour is
// swept above; each gets a class of its own because the coverage gate keys on the name,
// and each says the one thing that distinguishes it from the other four.

/// <summary>Burn: the baseline, 2 blockable damage.</summary>
public class BurnTests
{
    [Fact]
    public void ItIsTwoBlockableDamage()
    {
        var fight = Fight.Hand(new CardInstance(ST.Burn, false)).Enemy();
        fight.State.PlayerBlock = 5;
        int hp = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(hp, fight.State.PlayerHp);
    }
}

/// <summary>Infection: three a copy, and the Phrog Parasite deals three copies an Infect.</summary>
public class InfectionTests
{
    [Fact]
    public void ThreeCopiesCostNine()
    {
        var fight = Fight
            .Hand(
                new CardInstance(ST.Infection, false),
                new CardInstance(ST.Infection, false),
                new CardInstance(ST.Infection, false)
            )
            .Enemy();
        int hp = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(hp - 9, fight.State.PlayerHp);
    }
}

/// <summary>Toxic: five, and it EXHAUSTS rather than being unplayable.</summary>
public class ToxicTests
{
    [Fact]
    public void ItExhaustsInsteadOfBeingUnplayable()
    {
        var def = GeneratedData.Cards.Get(ST.Toxic);

        Assert.True(def.Exhaust);
        Assert.False(def.Unplayable);
        Assert.Equal(5, def.BaseDamage);
    }
}

/// <summary>Wither: three, and its `FakeUpgrade` growth nothing solo can reach.</summary>
public class WitherTests
{
    [Fact]
    public void ItIsThreeAndDoesNotGrowInASoloRun()
    {
        var fight = Fight.Hand(new CardInstance(ST.Wither, false)).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        int hp = fight.State.PlayerHp;

        fight.EndTurn();
        Assert.Equal(hp - 3, fight.State.PlayerHp);

        // It is Unplayable, so it is FLUSHED at end of turn rather than retained -- the
        // second turn costs nothing because the card is in the discard pile, not because
        // it stopped hurting. Put another in hand and it is still three: `FakeUpgrade`
        // raises the damage by three a call and nothing in a solo run calls it.
        fight.State.Hand.Add(new CardInstance(ST.Wither, false));
        hp = fight.State.PlayerHp;
        fight.EndTurn();
        Assert.Equal(hp - 3, fight.State.PlayerHp);
    }
}

/// <summary>Beckon: costs 1 rather than being unplayable, and hits for 6 unblockable.</summary>
public class BeckonTests
{
    [Fact]
    public void ItIsPlayableAndItsDamageIsUnblockable()
    {
        var def = GeneratedData.Cards.Get(ST.Beckon);
        Assert.Equal(1, def.Cost);
        Assert.False(def.Unplayable);

        var fight = Fight.Hand(new CardInstance(ST.Beckon, false)).Enemy();
        fight.State.PlayerBlock = 50;
        int hp = fight.State.PlayerHp;

        fight.EndTurn();

        Assert.Equal(hp - 6, fight.State.PlayerHp);
    }
}
