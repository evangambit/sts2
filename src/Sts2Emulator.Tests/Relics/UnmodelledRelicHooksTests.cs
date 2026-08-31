using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The rest of the reachable unmodelled relics: the ones that hang off an event during the
/// fight rather than off its opening.
/// </summary>
/// <remarks>
/// Several needed a hook the emulator did not have, and two of those turned out to be the
/// interesting part. `AfterCardDiscarded` is NOT the end-of-turn hand dump --
/// `FlushPlayerHand` is a plain pile add followed by `AfterFlush`, with no discard between
/// them -- and player Strength had no chokepoint at all, which is what Ruined Helmet needs
/// to double the first grant of it.
/// </remarks>
public class DiscardAndExhaustRelicTests
{
    /// <summary>`ToughBandages`: BlockVar(3m, Unpowered) per card an effect discards.</summary>
    [Fact]
    public void ToughBandagesPaysThreeBlockPerDiscardedCard()
    {
        var fight = Fight.WithRelics(RelicEffects.ToughBandages);
        int block = fight.State.PlayerBlock;

        CardEffects.DiscardFirstCardsFromHand(fight.State, 2);

        Assert.Equal(block + 6, fight.State.PlayerBlock);
    }

    /// <summary>
    /// The end-of-turn hand dump is NOT a discard. `FlushPlayerHand` adds the cards to the
    /// pile and dispatches `AfterFlush`, with no `CardDiscarded` history row and no
    /// `AfterCardDiscarded` -- so a hand emptied at end of turn pays nothing, which is
    /// most of what a player would assume the relic does.
    /// </summary>
    [Fact]
    public void TheEndOfTurnFlushIsNotADiscard()
    {
        var fight = Fight.WithRelics(RelicEffects.ToughBandages);
        Assert.NotEmpty(fight.State.Hand);

        fight.EndTurn();

        // Block is cleared at the turn boundary; what matters is that nothing was paid
        // for the five cards that just left the hand.
        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>`Tingsha`: DamageVar(3m, Unpowered) to one random enemy per card.</summary>
    [Fact]
    public void TingshaHitsOncePerDiscardedCard()
    {
        var fight = Fight.WithRelics(RelicEffects.Tingsha);
        int before = fight.State.Enemies.Sum(enemy => enemy.Hp);

        CardEffects.DiscardFirstCardsFromHand(fight.State, 3);

        Assert.Equal(before - 9, fight.State.Enemies.Sum(enemy => enemy.Hp));
    }

    /// <summary>
    /// `CharonsAshes`: DamageVar(3m, Unpowered) to EVERY hittable enemy, per card
    /// exhausted -- and unlike Joss Paper's banked count, an Ethereal exhaust pays too.
    /// </summary>
    [Fact]
    public void CharonsAshesBurnsTheWholeRoomPerExhaust()
    {
        var fight = Fight.WithRelics(RelicEffects.CharonsAshes);
        var before = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();

        CardEffects.ExhaustCard(fight.State, new CardInstance(430, false), rng: new System.Random(0));

        for (int i = 0; i < fight.State.Enemies.Count; i++)
        {
            Assert.Equal(before[i] - 3, fight.State.Enemies[i].Hp);
        }
    }
}

public class HelicalDartTests
{
    /// <summary>
    /// The TAG, not the Shiv id: Knife Trap carries `CardTag.Shiv` too, so the dart pays
    /// on the trap as well as on the Shivs it replays.
    /// </summary>
    [Fact]
    public void ItGainsDexterityFromAnyShivTaggedCard()
    {
        var fight = Fight.WithRelics(RelicEffects.HelicalDart).Energy(9).Enemy(hp: 200);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(430, false));

        fight.Play(0);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));
    }

    [Fact]
    public void AnOrdinaryAttackPaysNothing()
    {
        var fight = Fight.WithRelics(RelicEffects.HelicalDart).Energy(9).Enemy(hp: 200);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));

        fight.Play(0);

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));
    }

    /// <summary>Both cards the game tags, so the tag is data rather than a name test.</summary>
    [Fact]
    public void TheTagIsOnShivAndKnifeTrap()
    {
        Assert.True(GeneratedData.Cards.Get(430).ShivTag);
        Assert.True(
            GeneratedData.Cards.Get(GeneratedData.Cards.FindId("KnifeTrap")!.Value).ShivTag
        );
    }
}

public class PaperKraneAndPhrogTests
{
    /// <summary>
    /// `PaperKrane.ModifyWeakMultiplier` is -0.15 when its owner is the TARGET, so a Weak
    /// enemy hits them at 0.60 rather than 0.75. It reads the target, not the attacker --
    /// it does nothing to the Weak the player applies.
    /// </summary>
    [Fact]
    public void PaperKraneDeepensWeakOnThingsHittingYou()
    {
        var weakBuffs = new List<BuffState> { new(BuffId.Weak, 1) };

        int plain = BuffSystem.IncomingDamage(100, weakBuffs, []);
        int krane = BuffSystem.IncomingDamage(100, weakBuffs, [], weakDelta: -0.15f);

        Assert.Equal(75, plain);
        Assert.Equal(60, krane);
    }

    /// <summary>
    /// `PaperPhrog.ModifyVulnerableMultiplier` is +0.25 when the target is NOT its owner,
    /// so a Vulnerable enemy takes 1.75 rather than 1.5. The mirror image, and note the
    /// asymmetry: one helps only when you are hit, the other only when you are not.
    /// </summary>
    [Fact]
    public void PaperPhrogDeepensVulnerableOnEnemies()
    {
        var vulnerable = new List<BuffState> { new(BuffId.Vulnerable, 1) };

        int plain = BuffSystem.IncomingDamage(100, [], vulnerable);
        int phrog = BuffSystem.IncomingDamage(100, [], vulnerable, vulnerableDelta: 0.25f);

        Assert.Equal(150, plain);
        Assert.Equal(175, phrog);
    }

    /// <summary>The Krane's discount reaches the ANNOUNCED intent, not just the landing.</summary>
    [Fact]
    public void TheAnnouncedIntentMovesWithTheKrane()
    {
        // Encounter 3, not 1: encounter 1's pair each hold Artifact 2, and Artifact eats
        // the Weak this test needs before the relic can deepen it.
        var plain = Fight.Encounter(3);
        var krane = Fight.Encounter(3, RelicEffects.PaperKrane);
        BuffSystem.Apply(plain.State.Enemies[0].Buffs, BuffId.Weak, 1);
        BuffSystem.Apply(krane.State.Enemies[0].Buffs, BuffId.Weak, 1);

        Assert.True(
            krane.Intents.First().Magnitude < plain.Intents.First().Magnitude,
            "the readout should already carry the relic, the way it carries Strength"
        );
    }
}

public class DemonTongueAndEmotionChipTests
{
    /// <summary>
    /// `DemonTongue` heals the first unblocked hit taken on the player's OWN turn --
    /// `CurrentSide == Owner.Side` -- so it pays back self-inflicted damage and never an
    /// enemy attack.
    /// </summary>
    [Fact]
    public void DemonTongueHealsSelfInflictedDamageOnce()
    {
        var fight = Fight.WithRelics(RelicEffects.DemonTongue);
        int hp = fight.State.PlayerHp;

        CardEffects.LoseHp(fight.State, 7);
        Assert.Equal(hp, fight.State.PlayerHp);

        CardEffects.LoseHp(fight.State, 5);
        Assert.Equal(hp - 5, fight.State.PlayerHp);
    }

    /// <summary>Once per turn: the clock resets at the next player turn start.</summary>
    [Fact]
    public void DemonTongueRearmsEachTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.DemonTongue);
        CardEffects.LoseHp(fight.State, 4);

        fight.EndTurn();
        int afterEnemies = fight.State.PlayerHp;

        // Turn two's first self-inflicted hit is healed back like turn one's.
        CardEffects.LoseHp(fight.State, 4);

        Assert.Equal(afterEnemies, fight.State.PlayerHp);
    }

    /// <summary>
    /// `EmotionChip` fires every orb passive at the start of a turn following one where
    /// the player took unblocked damage.
    /// </summary>
    /// <remarks>
    /// Driven through `ApplyStartOfPlayerTurn` directly rather than through a real turn,
    /// because a real turn has the enemies attacking in it -- which arms the chip on both
    /// sides of the comparison and hides the effect entirely.
    /// </remarks>
    [Fact]
    public void EmotionChipRepeatsTheOrbPassivesAfterALossyTurn()
    {
        var hurt = Fight.WithRelics(RelicEffects.EmotionChip, RelicEffects.RunicCapacitor);
        var safe = Fight.WithRelics(RelicEffects.EmotionChip, RelicEffects.RunicCapacitor);
        foreach (var fight in new[] { hurt, safe })
        {
            fight.State.Orbs.Clear();
            CardEffects.ChannelOrb(fight.State, OrbType.Frost, new System.Random(0));
            fight.State.PlayerBlock = 0;
        }

        CardEffects.LoseHp(hurt.State, 5);
        RelicEffects.ApplyStartOfPlayerTurn(hurt.State, new System.Random(0));
        RelicEffects.ApplyStartOfPlayerTurn(safe.State, new System.Random(0));

        Assert.Equal(0, safe.State.PlayerBlock);
        Assert.True(
            hurt.State.PlayerBlock > 0,
            "a Frost orb's passive should have fired an extra time"
        );
    }

    /// <summary>And the flag is spent: the turn after a safe one repeats nothing.</summary>
    [Fact]
    public void TheChipDoesNotFireTwiceOffOneHit()
    {
        var fight = Fight.WithRelics(RelicEffects.EmotionChip, RelicEffects.RunicCapacitor);
        fight.State.Orbs.Clear();
        CardEffects.ChannelOrb(fight.State, OrbType.Frost, new System.Random(0));

        CardEffects.LoseHp(fight.State, 5);
        RelicEffects.ApplyStartOfPlayerTurn(fight.State, new System.Random(0));
        fight.State.PlayerBlock = 0;
        RelicEffects.ApplyStartOfPlayerTurn(fight.State, new System.Random(0));

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}

public class StarSpendingRelicTests
{
    /// <summary>`MiniRegent`: 1 Strength on the FIRST star spend each turn.</summary>
    [Fact]
    public void MiniRegentPaysOncePerTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.MiniRegent);

        RelicEffects.ApplyAfterStarsSpent(fight.State, 1, null);
        RelicEffects.ApplyAfterStarsSpent(fight.State, 3, null);

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>
    /// `GalacticDust` counts every star spent and pays 10 Unpowered block per full ten --
    /// `floor(StarsSpent / 10) * 10`, so a single spend of twenty-five pays twenty at once
    /// and carries five, rather than paying ten and losing the rest.
    /// </summary>
    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 10)]
    [InlineData(25, 20)]
    public void GalacticDustPaysPerFullTenStars(int spent, int block)
    {
        var fight = Fight.WithRelics(RelicEffects.GalacticDust);
        int before = fight.State.PlayerBlock;

        RelicEffects.ApplyAfterStarsSpent(fight.State, spent, null);

        Assert.Equal(before + block, fight.State.PlayerBlock);
    }

    /// <summary>The remainder carries: nine then two is eleven, and pays once.</summary>
    [Fact]
    public void TheDustsRemainderCarriesBetweenSpends()
    {
        var fight = Fight.WithRelics(RelicEffects.GalacticDust);
        int before = fight.State.PlayerBlock;

        RelicEffects.ApplyAfterStarsSpent(fight.State, 9, null);
        RelicEffects.ApplyAfterStarsSpent(fight.State, 2, null);

        Assert.Equal(before + 10, fight.State.PlayerBlock);
    }
}

public class OrbRelicTests
{
    /// <summary>
    /// `GoldPlatedCables`: the orb at the FRONT of the queue triggers its passive one
    /// extra time, and no other orb does.
    /// </summary>
    [Fact]
    public void GoldPlatedCablesRepeatsTheFrontOrbOnly()
    {
        var plain = Fight.WithRelics(RelicEffects.RunicCapacitor);
        var cabled = Fight.WithRelics(RelicEffects.RunicCapacitor, RelicEffects.GoldPlatedCables);
        foreach (var fight in new[] { plain, cabled })
        {
            fight.State.Orbs.Clear();
            CardEffects.ChannelOrb(fight.State, OrbType.Frost, new System.Random(0));
            CardEffects.ChannelOrb(fight.State, OrbType.Frost, new System.Random(0));
            fight.State.PlayerBlock = 0;
        }

        CardEffects.TriggerAllOrbBeforeTurnEndPassives(plain.State, new System.Random(0));
        CardEffects.TriggerAllOrbBeforeTurnEndPassives(cabled.State, new System.Random(0));

        // Two Frost orbs pay two passives plain, three with the cables -- the front one
        // twice. A trigger COUNT, not a doubled value.
        Assert.Equal(plain.State.PlayerBlock / 2 * 3, cabled.State.PlayerBlock);
    }

    /// <summary>
    /// `Metronome`: the SEVENTH orb channelled in a combat deals 30 Unpowered to all. The
    /// test is `== OrbCount`, not `>=`, so the eighth does nothing.
    /// </summary>
    [Fact]
    public void MetronomeFiresOnTheSeventhOrbAndNotTheEighth()
    {
        var fight = Fight.WithRelics(RelicEffects.Metronome, RelicEffects.RunicCapacitor);
        fight.State.Orbs.Clear();
        var rng = new System.Random(0);

        for (int i = 0; i < 6; i++)
        {
            CardEffects.ChannelOrb(fight.State, OrbType.Frost, rng);
        }

        var beforeSeventh = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();
        CardEffects.ChannelOrb(fight.State, OrbType.Frost, rng);
        var afterSeventh = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();
        CardEffects.ChannelOrb(fight.State, OrbType.Frost, rng);
        var afterEighth = fight.State.Enemies.Select(enemy => enemy.Hp).ToList();

        for (int i = 0; i < afterSeventh.Count; i++)
        {
            Assert.Equal(beforeSeventh[i] - 30, afterSeventh[i]);
            Assert.Equal(afterSeventh[i], afterEighth[i]);
        }
    }
}

public class DamageAndPowerModifierRelicTests
{
    /// <summary>
    /// `SneckoSkull`: one more Poison on every Poison the player applies. Additive on the
    /// amount GIVEN, so it lands once per application rather than once per stack.
    /// </summary>
    [Fact]
    public void SneckoSkullAddsOnePoisonPerApplication()
    {
        var plain = Fight.Encounter(3);
        var skull = Fight.Encounter(3, RelicEffects.SneckoSkull);

        CardEffects.ApplyPoisonToAllEnemies(plain.State, 3, new System.Random(0));
        CardEffects.ApplyPoisonToAllEnemies(skull.State, 3, new System.Random(0));

        Assert.Equal(3, BuffSystem.Get(plain.State.Enemies[0].Buffs, BuffId.Poison));
        Assert.Equal(4, BuffSystem.Get(skull.State.Enemies[0].Buffs, BuffId.Poison));
    }

    /// <summary>
    /// `RuinedHelmet`: the FIRST positive Strength the player receives each combat is
    /// doubled, and the relic is then spent for the rest of the fight.
    /// </summary>
    [Fact]
    public void RuinedHelmetDoublesOnlyTheFirstStrength()
    {
        var fight = Fight.WithRelics(RelicEffects.RuinedHelmet);

        RelicEffects.GainPlayerStrength(fight.State, 3);
        Assert.Equal(6, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        RelicEffects.GainPlayerStrength(fight.State, 3);
        Assert.Equal(9, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>
    /// A LOSS passes through untouched: `TryModifyPowerAmountReceived` refuses
    /// `amount &lt;= 0`, so Shockwave's -2 is not doubled into a bigger loss -- and it does
    /// not spend the relic either.
    /// </summary>
    [Fact]
    public void RuinedHelmetIgnoresAStrengthLoss()
    {
        var fight = Fight.WithRelics(RelicEffects.RuinedHelmet);

        RelicEffects.GainPlayerStrength(fight.State, -2);
        Assert.Equal(-2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        RelicEffects.GainPlayerStrength(fight.State, 3);
        Assert.Equal(4, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>
    /// `UndyingSigil`: an attacker already at or below its own Doom hits for half. The
    /// relic's own doc comment says it "doesn't actually do anything" -- that is about its
    /// OTHER half, and the halving right below the comment is real.
    /// </summary>
    [Fact]
    public void UndyingSigilHalvesADoomedAttackersHit()
    {
        // Encounter 3 again: Doom is a debuff, and encounter 1's Artifact swallows it.
        var plain = Fight.Encounter(3);
        var sigil = Fight.Encounter(3, RelicEffects.UndyingSigil);
        foreach (var fight in new[] { plain, sigil })
        {
            var enemy = fight.State.Enemies[0];
            enemy.Hp = 5;
            BuffSystem.Apply(enemy.Buffs, BuffId.Doom, 10);
            fight.State.PlayerBlock = 0;
        }

        int plainHp = plain.State.PlayerHp;
        int sigilHp = sigil.State.PlayerHp;
        EnemyAI.DealAttackDamageForTests(plain.State.Enemies[0], plain.State, 20);
        EnemyAI.DealAttackDamageForTests(sigil.State.Enemies[0], sigil.State, 20);

        int plainTaken = plainHp - plain.State.PlayerHp;
        Assert.True(plainTaken > 0);
        Assert.Equal(plainTaken / 2, sigilHp - sigil.State.PlayerHp);
    }

    /// <summary>An attacker with more HP than Doom is not weakened.</summary>
    [Fact]
    public void UndyingSigilDoesNothingToAHealthyAttacker()
    {
        var fight = Fight.Encounter(3, RelicEffects.UndyingSigil);
        var enemy = fight.State.Enemies[0];
        BuffSystem.Apply(enemy.Buffs, BuffId.Doom, 1);
        fight.State.PlayerBlock = 0;
        int hp = fight.State.PlayerHp;

        EnemyAI.DealAttackDamageForTests(enemy, fight.State, 20);

        Assert.Equal(hp - 20, fight.State.PlayerHp);
    }

    /// <summary>`VitruvianMinion`: 2x damage AND 2x block from a Minion-tagged card.</summary>
    [Fact]
    public void VitruvianMinionDoublesBothHalves()
    {
        int strike = GeneratedData.Cards.FindId("MinionStrike")!.Value;
        int sacrifice = GeneratedData.Cards.FindId("MinionSacrifice")!.Value;

        var plain = Fight.WithRelics().Energy(9).Enemy(hp: 300);
        var relic = Fight.WithRelics(RelicEffects.VitruvianMinion).Energy(9).Enemy(hp: 300);
        foreach (var fight in new[] { plain, relic })
        {
            fight.State.Hand.Clear();
            fight.State.Hand.Add(new CardInstance(strike, false));
            fight.State.Hand.Add(new CardInstance(sacrifice, false));
            fight.State.PlayerBlock = 0;
            fight.Play(0);
            fight.Play(0);
        }

        int plainDealt = 300 - plain.State.Enemies[0].Hp;
        int relicDealt = 300 - relic.State.Enemies[0].Hp;
        Assert.Equal(plainDealt * 2, relicDealt);
        Assert.Equal(plain.State.PlayerBlock * 2, relic.State.PlayerBlock);
    }
}

public class GenerationAndPetRelicTests
{
    /// <summary>`Regalite`: BlockVar(2m, Unpowered) per card the player generates.</summary>
    [Fact]
    public void RegalitePaysTwoBlockPerGeneratedCard()
    {
        var fight = Fight.WithRelics(RelicEffects.Regalite);
        fight.State.PlayerBlock = 0;

        CardEffects.AddGeneratedCardsToHand(fight.State, 430, 3);

        Assert.Equal(6, fight.State.PlayerBlock);
    }

    /// <summary>`BoneFlute`: BlockVar(2m, Unpowered) on every swing the pet takes.</summary>
    [Fact]
    public void BoneFlutePaysPerOstySwing()
    {
        var fight = Fight.WithRelics(RelicEffects.BoneFlute);
        fight.State.PlayerBlock = 0;

        CardEffects.OstyAttackDamage(fight.State, 5);
        CardEffects.OstyAttackDamage(fight.State, 5);

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    /// <summary>
    /// `BookRepairKnife`: HealVar(3m) for EACH creature that died to Doom, and nothing at
    /// all when none did.
    /// </summary>
    [Fact]
    public void BookRepairKnifeHealsThreePerDoomedDeath()
    {
        var fight = Fight.Encounter(3, RelicEffects.BookRepairKnife);
        fight.State.PlayerHp = 30;
        foreach (var enemy in fight.State.Enemies.Take(2))
        {
            enemy.Hp = 1;
            BuffSystem.Apply(enemy.Buffs, BuffId.Doom, 5);
        }

        CardEffects.KillDoomedEnemiesForTurnEnd(fight.State);

        Assert.Equal(36, fight.State.PlayerHp);
    }

    [Fact]
    public void ADoomlessRoomHealsNothing()
    {
        var fight = Fight.Encounter(3, RelicEffects.BookRepairKnife);
        fight.State.PlayerHp = 30;

        CardEffects.KillDoomedEnemiesForTurnEnd(fight.State);

        Assert.Equal(30, fight.State.PlayerHp);
    }
}

public class HandDrawRelicTests
{
    /// <summary>`NinjaScroll`: three Shivs into HAND before the opening draw.</summary>
    [Fact]
    public void NinjaScrollOpensWithThreeShivs()
    {
        var fight = Fight.WithRelics(RelicEffects.NinjaScroll);

        Assert.Equal(3, fight.State.Hand.Count(card => card.DefId == 430));
    }

    /// <summary>
    /// `FuneraryMask`: three Souls shuffled into the DRAW pile, not dealt into hand.
    /// </summary>
    [Fact]
    public void FuneraryMaskBuriesThreeSouls()
    {
        var fight = Fight.WithRelics(RelicEffects.FuneraryMask);

        Assert.Equal(
            3,
            fight.State.DrawPile.Count(card => card.DefId == 446)
                + fight.State.Hand.Count(card => card.DefId == 446)
        );
        Assert.Contains(fight.State.DrawPile, card => card.DefId == 446);
    }
}

public class BookmarkTests
{
    /// <summary>
    /// `Bookmark.AfterFlush`: one RETAINED card costing more than zero gets -1 until it is
    /// played. Fires on the flush, which is a boundary the discard relics deliberately do
    /// not see.
    /// </summary>
    [Fact]
    public void ItDiscountsOneRetainedCard()
    {
        var fight = Fight.WithRelics(RelicEffects.Bookmark);
        fight.State.Hand.Clear();
        for (int i = 0; i < 3; i++)
        {
            fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false, RetainThisTurn: true));
        }

        fight.EndTurn();

        // Exactly one of the retained cards is discounted, whatever else the new turn
        // drew in alongside them.
        Assert.Equal(1, fight.State.Hand.Count(card => card.CostBump == -1));
    }

    /// <summary>A hand of free cards is no candidate: the filter is `cost > 0`.</summary>
    [Fact]
    public void AFreeHandGetsNothing()
    {
        var fight = Fight.WithRelics(RelicEffects.Bookmark);
        fight.State.Hand.Clear();
        for (int i = 0; i < 3; i++)
        {
            fight.State.Hand.Add(new CardInstance(430, false, RetainThisTurn: true));
        }

        fight.EndTurn();

        Assert.All(
            fight.State.Hand.Where(card => card.DefId == 430),
            card => Assert.Equal(0, card.CostBump)
        );
    }
}

public class RunLevelUnmodelledRelicTests
{
    /// <summary>`LoomingFruit.AfterObtained`: MaxHpVar(31m).</summary>
    [Fact]
    public void LoomingFruitPaysThirtyOneMaxHp()
    {
        var engine = new Sts2Emulator.Core.Run.RunEngine();
        engine.Reset("NXV45HW43K");
        int max = engine.State.PlayerMaxHp;

        Sts2Emulator.Core.Run.RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            Sts2Emulator.Core.Run.RunNonCombatEffects.NamedRelic("LoomingFruit")
        );

        Assert.Equal(max + 31, engine.State.PlayerMaxHp);
    }

    /// <summary>
    /// `FresnelLens.TryModifyCardBeingAddedToDeck`: every card entering the deck that
    /// Nimble can take arrives already enchanted at 2. This is the hook that catches every
    /// route in -- the other two only change what a reward SCREEN shows.
    /// </summary>
    [Fact]
    public void FresnelLensEnchantsEveryCardEnteringTheDeck()
    {
        var engine = new Sts2Emulator.Core.Run.RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(
            new RelicInstance(Sts2Emulator.Core.Run.RunNonCombatEffects.NamedRelic("FresnelLens"))
        );

        int skill = GeneratedData.Cards.FindId("Rally")!.Value;
        Sts2Emulator.Core.Run.RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(skill, false)
        );

        var added = engine.State.Deck.Last();
        Assert.Equal(Enchantment.Nimble, added.Enchantment);
        Assert.Equal(2, added.EnchantAmount);
    }

    /// <summary>Nimble is Skills only, so an Attack arrives untouched.</summary>
    [Fact]
    public void AnAttackIsNotEnchanted()
    {
        var engine = new Sts2Emulator.Core.Run.RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(
            new RelicInstance(Sts2Emulator.Core.Run.RunNonCombatEffects.NamedRelic("FresnelLens"))
        );

        Sts2Emulator.Core.Run.RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(IC.StrikeIronclad, false)
        );

        Assert.Equal(Enchantment.None, engine.State.Deck.Last().Enchantment);
    }
}
