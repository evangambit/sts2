using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Darv's eight, and the option list that offers them. Tier B of PLAN.md §7.
/// </summary>
/// <remarks>
/// Five of the eight already had written implementations that could NEVER RUN: their id
/// constants in `RunConstants` were 1332, 1363, 1394, 1399 and 1510, none of which is a
/// relic at all. Wrong values would have been bad enough; these were fabricated, so the
/// `switch` arms behind them were unreachable and the audit reported the relics as
/// unmodelled while the code read as finished. Prismatic Gem's 1533 was the sixth. E398.
/// </remarks>
public class DarvOptionTests
{
    private static RunEngine AtDarv(string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Ancient = RunConstants.AncientDarv;
        return engine;
    }

    /// <summary>Three options, always -- either three relics or two and Dusty Tome.</summary>
    [Theory]
    [InlineData("NXV45HW43K")]
    [InlineData("ABCDEF")]
    [InlineData("SAM9XS24LM")]
    public void ItAlwaysOffersThree(string seed)
    {
        var engine = AtDarv(seed);

        var options = RunNonCombatEffects.GenerateAncientOptions(
            engine.State,
            RunConstants.AncientDarv
        );

        Assert.Equal(3, options.Length);
        Assert.All(options, id => Assert.NotEqual(0, id));
    }

    /// <summary>Every option is one of Darv's own, and no option repeats.</summary>
    [Fact]
    public void TheOptionsComeFromDarvsSetsAndAreDistinct()
    {
        var allowed = RunConstants
            .DarvSingleRelicSets.ToArray()
            .Append(RunConstants.DarvDustyTome)
            .ToHashSet();
        var engine = AtDarv();

        var options = RunNonCombatEffects.GenerateAncientOptions(
            engine.State,
            RunConstants.AncientDarv
        );

        Assert.Equal(3, options.Distinct().Count());
        Assert.All(options, id => Assert.Contains(id, allowed));
    }

    /// <summary>
    /// The act-gated sets are `CurrentActIndex == 1` and `== 2`. Act index 0 -- which is
    /// the only one the emulator runs -- reaches neither, and neither spends a draw.
    /// </summary>
    [Fact]
    public void TheActGatedSetsAreAbsentInActOne()
    {
        var engine = AtDarv();
        var actTwoOnly = RunConstants
            .DarvActOneSet.ToArray()
            .Concat(RunConstants.DarvActTwoSet.ToArray())
            .ToHashSet();

        var options = RunNonCombatEffects.GenerateAncientOptions(
            engine.State,
            RunConstants.AncientDarv
        );

        Assert.All(options, id => Assert.DoesNotContain(id, actTwoOnly));
    }

    /// <summary>
    /// The whole list comes off the ancient's OWN stream, so it does not move with the
    /// player's rewards stream.
    /// </summary>
    [Fact]
    public void TheOfferDoesNotMoveWithTheRewardsStream()
    {
        var plain = AtDarv();
        var spent = AtDarv();
        spent.State.PlayerRng.Rewards.NextDouble();

        Assert.Equal(
            RunNonCombatEffects.GenerateAncientOptions(plain.State, RunConstants.AncientDarv),
            RunNonCombatEffects.GenerateAncientOptions(spent.State, RunConstants.AncientDarv)
        );
    }
}

public class DarvRelicTests
{
    private static RunEngine Run(int relicId)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        RunNonCombatEffects.ApplyRelicPickup(engine.State, relicId);
        return engine;
    }

    /// <summary>
    /// `RunicPyramid.ShouldFlush` is false for its owner on EVERY turn -- the hand is
    /// simply never discarded. The Ringing Triangle is the same hook with a turn-one guard.
    /// </summary>
    [Fact]
    public void RunicPyramidKeepsTheWholeHand()
    {
        var fight = Fight.WithRelics(RelicEffects.RunicPyramid);
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        // Ethereal cards EXHAUST at end of turn whether the hand is retained or not, so
        // Ascender's Bane leaves however strong the retain is.
        var handBefore = fight
            .State.Hand.Where(c => !c.IsEthereal())
            .Select(c => c.DefId)
            .ToList();

        fight.EndTurn();

        foreach (int defId in handBefore)
        {
            Assert.Contains(defId, fight.State.Hand.Select(c => c.DefId));
        }
    }

    /// <summary>And it never runs out -- turn five keeps the hand as turn one did.</summary>
    [Fact]
    public void ThePyramidHasNoClock()
    {
        var fight = Fight.WithRelics(RelicEffects.RunicPyramid);
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;

        for (int turn = 0; turn < 4; turn++)
        {
            fight.EndTurn();
        }

        // Nothing the PLAYER held was flushed, five turns in. The discard pile is not
        // empty -- the enemies have been shovelling Wounds into it -- so the thing to
        // measure is that the hand kept growing instead of being emptied each turn.
        Assert.True(
            fight.State.Hand.Count > 5,
            $"a never-flushed hand should have grown past five, got {fight.State.Hand.Count}"
        );
        Assert.DoesNotContain(
            fight.State.DiscardPile,
            card => card.DefId == IC.StrikeIronclad || card.DefId == IC.DefendIronclad
        );
    }

    /// <summary>
    /// `SneckoEye`: Confused, and two more cards every hand draw. The cards are what pays
    /// for the Confused -- Fake Snecko Eye gives the Confused and nothing else.
    /// </summary>
    [Fact]
    public void SneckoEyeDrawsTwoMoreAndConfuses()
    {
        var plain = Fight.WithRelics();
        var snecko = Fight.WithRelics(RelicEffects.SneckoEye);

        Assert.Equal(1, BuffSystem.Get(snecko.State.PlayerBuffs, BuffId.Confused));
        Assert.Equal(0, BuffSystem.Get(plain.State.PlayerBuffs, BuffId.Confused));
        Assert.Equal(plain.State.Hand.Count + 2, snecko.State.Hand.Count);
    }

    /// <summary>The fake gives the Confused and NO cards.</summary>
    [Fact]
    public void TheFakeGivesTheDownsideOnly()
    {
        var plain = Fight.WithRelics();
        var fake = Fight.WithRelics(RelicEffects.FakeSneckoEye);

        Assert.Equal(1, BuffSystem.Get(fake.State.PlayerBuffs, BuffId.Confused));
        Assert.Equal(plain.State.Hand.Count, fake.State.Hand.Count);
    }

    /// <summary>
    /// `ConfusedPower.AfterCardDrawn` re-rolls the card's cost for the rest of the combat
    /// to 0..3, off the run's `combat_energy_costs` stream.
    /// </summary>
    [Fact]
    public void ConfusedRerollsTheCostOfEveryCardDrawn()
    {
        var fight = Fight.WithRelics(RelicEffects.SneckoEye);

        Assert.All(
            fight.State.Hand.Where(card => GeneratedData.Cards.Get(card.DefId).Cost >= 0),
            card =>
            {
                Assert.NotEqual(int.MinValue, card.CostForCombat);
                Assert.InRange(card.CostForCombat, 0, 3);
            }
        );
    }

    /// <summary>
    /// An X-cost card is skipped -- `EnergyCost.Canonical &lt; 0`. A rolled number would
    /// turn it into an ordinary card.
    /// </summary>
    [Fact]
    public void AnXCostCardKeepsItsCost()
    {
        var fight = Fight.WithRelics(RelicEffects.SneckoEye);
        int xCost = GeneratedData
            .Cards.All.ToArray()
            .First(def => def.HasEnergyCostX)
            .Id;
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(xCost, false));
        fight.State.Hand.Clear();

        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(int.MinValue, fight.State.Hand[0].CostForCombat);
    }

    /// <summary>`BlackStar`: an extra relic on every ELITE reward, and nowhere else.</summary>
    [Fact]
    public void BlackStarPaysAnExtraRelicAfterAnElite()
    {
        var engine = Run(RelicEffects.BlackStar);
        engine.State.LastResolvedRoomType = RunConstants.NodeElite;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.NotEmpty(engine.State.PendingBonusRelicRewards);
    }

    [Fact]
    public void AnOrdinaryFightPaysNoExtraRelic()
    {
        var engine = Run(RelicEffects.BlackStar);
        engine.State.LastResolvedRoomType = RunConstants.NodeNormal;

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Empty(engine.State.PendingBonusRelicRewards);
    }

    /// <summary>
    /// `PandorasBox`: every BASIC Strike or Defend that is removable, transformed. The
    /// arm matched Ironclad's two card IDS on the Transformations stream -- wrong filter,
    /// wrong stream, and nothing at all for any other character.
    /// </summary>
    [Fact]
    public void PandorasBoxTransformsEveryBasicStrikeAndDefend()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int before = engine.State.Deck.Count;
        int basics = engine.State.Deck.Count(card =>
        {
            var def = GeneratedData.Cards.Get(card.DefId);
            return def.Rarity == CardRarity.Basic && (def.StrikeTag || def.DefendTag);
        });
        Assert.True(basics > 0);

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.PandorasBox);

        Assert.Equal(before, engine.State.Deck.Count);
        Assert.Equal(
            0,
            engine.State.Deck.Count(card =>
            {
                var def = GeneratedData.Cards.Get(card.DefId);
                return def.Rarity == CardRarity.Basic && (def.StrikeTag || def.DefendTag);
            })
        );
    }

    /// <summary>Ascender's Bane is not removable, so the box leaves it alone.</summary>
    [Fact]
    public void ItLeavesAscendersBaneWhereItIs()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int bane = engine.State.Deck.Count(c => c.DefId == IC.AscendersBane);

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.PandorasBox);

        Assert.Equal(bane, engine.State.Deck.Count(c => c.DefId == IC.AscendersBane));
    }

    /// <summary>
    /// `DustyTome`: an UPGRADED copy of an ANCIENT-rarity card from the character's own
    /// pool, minus Archaic Tooth's transcendence cards. The Ironclad's Ancient cards are
    /// Break and Corruption, and Break is a transcendence card -- so it is always
    /// Corruption.
    /// </summary>
    [Fact]
    public void DustyTomeAddsAnUpgradedAncientCard()
    {
        var engine = Run(RelicEffects.DustyTome);
        var added = engine.State.Deck.Last();

        Assert.Equal(CardRarity.Ancient, GeneratedData.Cards.Get(added.DefId).Rarity);
        Assert.True(added.Upgraded);
        Assert.Equal("Corruption", GeneratedData.Cards.Get(added.DefId).Name);
    }

    /// <summary>
    /// `CallingBell`: a Curse of the Bell and THREE relics on a screen, one of each
    /// rarity. The arm handed over three ROLLED pool relics directly -- no screen, no
    /// rarities.
    /// </summary>
    [Fact]
    public void CallingBellPaysACurseAndThreeRelicsOfEachRarity()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.CallingBell);

        Assert.Contains(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.NamedCard("CurseOfTheBell")
        );
        Assert.Equal(3, engine.State.PendingBonusRelicRewards.Count);
        var rarities = engine
            .State.PendingBonusRelicRewards.Select(id => GeneratedData.Relics.Get(id).Rarity)
            .ToList();
        Assert.Contains(RelicRarity.Common, rarities);
        Assert.Contains(RelicRarity.Uncommon, rarities);
        Assert.Contains(RelicRarity.Rare, rarities);
    }

    /// <summary>`EmptyCage`: TWO cards the player picks, removed.</summary>
    [Fact]
    public void EmptyCageAsksForTwoRemovals()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int before = engine.State.Deck.Count;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.EmptyCage);
        Assert.Equal(DeckSelection.Remove, engine.State.PendingSelectionKind);
        Assert.Equal(2, engine.State.PendingSelectionCount);

        for (int pick = 0; pick < 2; pick++)
        {
            int index = Enumerable
                .Range(0, engine.State.Deck.Count)
                .First(i => RunNonCombatEffects.CanSelectCard(engine.State, i));
            RunNonCombatEffects.ApplyDeckSelection(engine.State, index);
        }

        Assert.Equal(before - 2, engine.State.Deck.Count);
    }

    /// <summary>
    /// `Astrolabe`: THREE cards the player picks, each transformed AND upgraded. The
    /// upgrade lands on the NEW card, not the old one.
    /// </summary>
    [Fact]
    public void AstrolabeTransformsThreeChosenCardsAndUpgradesThem()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int before = engine.State.Deck.Count;
        int upgradedBefore = engine.State.Deck.Count(c => c.Upgraded);

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.Astrolabe);
        Assert.Equal(DeckSelection.TransformToRandomUpgraded, engine.State.PendingSelectionKind);
        Assert.Equal(3, engine.State.PendingSelectionCount);

        for (int pick = 0; pick < 3; pick++)
        {
            RunNonCombatEffects.ApplyDeckSelection(engine.State, 0);
        }

        Assert.Equal(before, engine.State.Deck.Count);
        Assert.True(engine.State.Deck.Count(c => c.Upgraded) > upgradedBefore);
    }

    /// <summary>
    /// `PrismaticGem`: +1 max energy, AND card rewards roll from every character's pool.
    /// Its arm added a random card on pickup, which is a different relic entirely.
    /// </summary>
    [Fact]
    public void PrismaticGemPaysAnEnergyAndWidensTheRewardPool()
    {
        var plain = Fight.WithRelics();
        var gem = Fight.WithRelics(RelicEffects.PrismaticGem);
        Assert.Equal(plain.State.MaxEnergy + 1, gem.State.MaxEnergy);

        var engine = Run(RelicEffects.PrismaticGem);
        var ironclad = GeneratedData.CardPools.Ironclad.ToArray().ToHashSet();
        bool sawAnother = false;
        for (int attempt = 0; attempt < 40 && !sawAnother; attempt++)
        {
            RunRewardGenerator.PopulateCardReward(engine.State);
            sawAnother = engine.State.RewardCards.Any(id => id != 0 && !ironclad.Contains(id));
        }

        Assert.True(sawAnother, "a card reward should be able to offer another pool's cards");
    }

    /// <summary>Without it the reward screen stays inside the player's own pool.</summary>
    [Fact]
    public void WithoutTheGemRewardsStayInThePlayersPool()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        var ironclad = GeneratedData.CardPools.Ironclad.ToArray().ToHashSet();

        for (int attempt = 0; attempt < 20; attempt++)
        {
            RunRewardGenerator.PopulateCardReward(engine.State);
            Assert.All(
                engine.State.RewardCards.Where(id => id != 0),
                id => Assert.Contains(id, ironclad)
            );
        }
    }
}
