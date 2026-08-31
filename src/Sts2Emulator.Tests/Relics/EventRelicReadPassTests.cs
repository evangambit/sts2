using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `BiiigHug.AfterShuffle`: a SOOT into the draw pile at a random position, on every
/// shuffle, for the whole run. Only its pickup half — remove four chosen cards — was
/// modelled, which made a hug that costs nothing into a hug that only pays.
/// </summary>
public class BiiigHugTests
{
    [Fact]
    public void EveryShuffleAddsASoot()
    {
        var fight = Fight.WithRelics(RelicEffects.BiiigHug).Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(473, false));

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));
        Assert.Equal(1, fight.State.DrawPile.Count(c => c.DefId == ST.Soot));

        fight.State.DiscardPile.AddRange(fight.State.DrawPile);
        fight.State.DrawPile.Clear();
        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.Equal(2, fight.State.DrawPile.Count(c => c.DefId == ST.Soot));
    }

    [Fact]
    public void WithoutItNothingIsAdded()
    {
        var fight = Fight.WithRelics().Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(473, false));

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.DoesNotContain(fight.State.DrawPile, c => c.DefId == ST.Soot);
    }
}

/// <summary>
/// Booming Conch's two cards are a `ModifyHandDraw`, not a draw of their own — the third
/// relic carrying that mechanic, after Ring of the Snake and Bag of Preparation, and the
/// second to have been modelled as a separate `DrawCards` at combat start.
/// </summary>
/// <remarks>
/// Its energy IS a combat-start effect (`AfterSideTurnStart`) and stays there. Both halves
/// only pay in an ELITE room, and only on turn one.
/// </remarks>
public class BoomingConchTests
{
    // A real elite encounter, so `IsEliteCombat` is set by the factory BEFORE combat
    // start runs -- setting the flag afterwards is too late for either half of the conch.
    private static Fight Elite(params int[] relics) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.BygoneEffigy, relicIds: relics);

    private static Fight NotElite(params int[] relics) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.DenseVegetation, relicIds: relics);

    [Fact]
    public void TheTwoCardsArePartOfTheHandDraw()
    {
        var plain = Elite();
        var conch = Elite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Hand.Count + 2, conch.State.Hand.Count);
        Assert.Equal(0, conch.State.ExtraCardsDrawnThisTurn);
    }

    [Fact]
    public void OutsideAnEliteItPaysNothing()
    {
        var plain = NotElite();
        var conch = NotElite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Hand.Count, conch.State.Hand.Count);
        Assert.Equal(plain.State.Energy, conch.State.Energy);
    }

    [Fact]
    public void TheEnergyIsStillACombatStartEffect()
    {
        var plain = Elite();
        var conch = Elite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Energy + 1, conch.State.Energy);
    }
}

/// <summary>
/// `LavaRock.TryModifyRewards` adds `DynamicVar("Relics", 2)` relic rewards to the ACT-1
/// BOSS room, once per run, and disables itself.
/// </summary>
/// <remarks>
/// The emulator's run ends at that boss, so this is the only room it can ever fire in —
/// and it had no effect at all: the relic was an id constant with nothing behind it.
/// </remarks>
public class LavaRockTests
{
    private static RunEngine AtBoss(bool withRock)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        if (withRock)
        {
            engine.State.Relics.Add(new RelicInstance(RunConstants.RelicLavaRock));
        }

        engine.State.CurrentNodeType = RunConstants.NodeBoss;
        RunRewardGenerator.GenerateCombatRewards(engine.State);
        return engine;
    }

    [Fact]
    public void TheBossRoomOwesTwoRelics()
    {
        var plain = AtBoss(withRock: false);
        var rock = AtBoss(withRock: true);

        Assert.False(plain.State.PendingRelicReward);
        Assert.Empty(plain.State.PendingBonusRelicRewards);

        // One on the screen and one queued behind it — two rewards, one at a time.
        Assert.True(rock.State.PendingRelicReward);
        Assert.Single(rock.State.PendingBonusRelicRewards);
    }

    /// <summary>Once per run: `HasTriggered` disables it.</summary>
    [Fact]
    public void ItFiresOnlyOnce()
    {
        var engine = AtBoss(withRock: true);
        engine.State.PendingBonusRelicRewards.Clear();
        engine.State.PendingRelicReward = false;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Empty(engine.State.PendingBonusRelicRewards);
        Assert.False(engine.State.PendingRelicReward);
    }

    /// <summary>And only at a BOSS — an ordinary combat owes nothing.</summary>
    [Fact]
    public void AnOrdinaryCombatOwesNothing()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(new RelicInstance(RunConstants.RelicLavaRock));
        engine.State.CurrentNodeType = RunConstants.NodeNormal;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Empty(engine.State.PendingBonusRelicRewards);
        Assert.False(engine.State.LavaRockTriggered);
    }
}

/// <summary>
/// `PaelsLegion` is a PET that doubles a card's block, then sits out `Turns` (2) of its
/// owner's turns before it will again.
/// </summary>
/// <remarks>
/// The emulator had the creature in `EnemyAI` and the relic in Pael's blessing options,
/// and no behaviour at all: the pet existed and did nothing. `ModifyBlockMultiplicative`
/// returns 2 while the cooldown is clear; `AfterCardPlayed` starts the cooldown once the
/// doubled gain has actually landed, which is Vambrace's latch with a timer instead of a
/// once-per-combat flag.
/// </remarks>
public class PaelsLegionTests
{
    private const int DefendIronclad = 131;

    private static Fight WithLegion() =>
        Fight.WithRelics(RelicEffects.PaelsLegion).Energy(9).Enemy(hp: 200);

    [Fact]
    public void ItDoublesTheFirstCardBlock()
    {
        var plain = Fight.WithRelics().Energy(9).Enemy(hp: 200);
        plain.State.Hand.Add(new CardInstance(DefendIronclad, false));
        plain.Play(plain.State.Hand.Count - 1);

        var legion = WithLegion();
        legion.State.Hand.Add(new CardInstance(DefendIronclad, false));
        legion.Play(legion.State.Hand.Count - 1);

        Assert.Equal(plain.State.PlayerBlock * 2, legion.State.PlayerBlock);
    }

    /// <summary>And then it sits out — the very next card is not doubled.</summary>
    [Fact]
    public void TheNextCardIsNotDoubled()
    {
        var legion = WithLegion();
        legion.State.Hand.Add(new CardInstance(DefendIronclad, false));
        legion.Play(legion.State.Hand.Count - 1);
        int afterFirst = legion.State.PlayerBlock;

        legion.State.Hand.Add(new CardInstance(DefendIronclad, false));
        legion.Play(legion.State.Hand.Count - 1);

        // The second gain is the plain one: half of what the first paid.
        Assert.Equal(afterFirst + afterFirst / 2, legion.State.PlayerBlock);
    }

    /// <summary>Two of the owner's turns and the pet is ready again.</summary>
    [Fact]
    public void ItComesBackAfterTwoTurns()
    {
        var legion = WithLegion();
        legion.State.Hand.Add(new CardInstance(DefendIronclad, false));
        legion.Play(legion.State.Hand.Count - 1);
        int doubled = legion.State.PlayerBlock;

        legion.EndTurn();
        legion.EndTurn();
        legion.State.PlayerBlock = 0;
        legion.State.Hand.Add(new CardInstance(DefendIronclad, false));
        legion.Play(legion.State.Hand.Count - 1);

        Assert.Equal(doubled, legion.State.PlayerBlock);
    }
}

/// <summary>
/// Big Mushroom's `ModifyHandDraw` SUBTRACTS its `CardsVar(2)` on turn one: the opening
/// hand is three, and that is the price of its twenty max HP.
/// </summary>
/// <remarks>
/// Only its pickup half was modelled — read the `AfterObtained`, stop, and a relic with a
/// drawback becomes a relic without one. The fourth relic on `ModifyHandDraw`, after Ring
/// of the Snake, Bag of Preparation and Booming Conch, and the only one that goes down.
/// </remarks>
public class BigMushroomTests
{
    [Fact]
    public void TheOpeningHandIsTwoCardsShorter()
    {
        var plain = Fight.WithRelics();
        var mushroom = Fight.WithRelics(RelicEffects.BigMushroom);

        Assert.Equal(plain.State.Hand.Count - 2, mushroom.State.Hand.Count);
    }

    /// <summary>Turn one only — the next turn draws normally.</summary>
    [Fact]
    public void LaterTurnsDrawNormally()
    {
        var plain = Fight.WithRelics();
        var mushroom = Fight.WithRelics(RelicEffects.BigMushroom);

        plain.EndTurn();
        mushroom.EndTurn();

        Assert.Equal(plain.State.Hand.Count, mushroom.State.Hand.Count);
    }

    /// <summary>And the twenty max HP it pays for still lands on pickup.</summary>
    [Fact]
    public void ThePickupStillPaysTwentyMaxHp()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int maxBefore = engine.State.PlayerMaxHp;

        RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunNonCombatEffects.NamedRelic("BigMushroom")
        );

        Assert.Equal(maxBefore + 20, engine.State.PlayerMaxHp);
    }
}

/// <summary>
/// Neow's Talisman upgrades the LAST BASIC card tagged Strike and the last tagged Defend.
/// It matched Ironclad's two card ids, so for any other character it upgraded nothing —
/// Leafy Poultice's bug pointing the other way.
/// </summary>
public class NeowsTalismanTests
{
    [Fact]
    public void ItUpgradesTheLastBasicStrikeAndDefend()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");

        RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunNonCombatEffects.NamedRelic("NeowsTalisman")
        );

        var upgraded = engine
            .State.Deck.Where(c => c.Upgraded)
            .Select(c => GeneratedData.Cards.Get(c.DefId))
            .ToList();

        Assert.Equal(2, upgraded.Count);
        Assert.Contains(upgraded, d => d.StrikeTag);
        Assert.Contains(upgraded, d => d.DefendTag);
        Assert.All(upgraded, d => Assert.Equal(CardRarity.Basic, d.Rarity));
    }

    /// <summary>The LAST of each, not the first — the two relics differ only in that.</summary>
    [Fact]
    public void ItTakesTheLastOfEachNotTheFirst()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int lastStrike = engine.State.Deck.FindLastIndex(c =>
            GeneratedData.Cards.Get(c.DefId).StrikeTag
        );

        RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunNonCombatEffects.NamedRelic("NeowsTalisman")
        );

        Assert.True(engine.State.Deck[lastStrike].Upgraded);
    }
}
