using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Fake Merchant's stall: nine relics at 50 gold each, each a real relic at a
/// discount. Every one of them was inert -- the stall was built two commits before
/// anything it sells did anything.
/// </summary>
public class FakeRelicTests
{
    [Fact]
    public void FakeAnchorIsFourBlockWhereAnchorIsTen()
    {
        var plain = Fight.WithRelics();
        var fake = Fight.WithRelics(RelicEffects.FakeAnchor);
        var real = Fight.WithRelics(RelicEffects.Anchor);

        Assert.Equal(plain.State.PlayerBlock + 4, fake.State.PlayerBlock);
        Assert.Equal(plain.State.PlayerBlock + 10, real.State.PlayerBlock);
    }

    /// <summary>
    /// `FakeBloodVial` heals 1 at TURN ONE's start; the real vial heals 2 at COMBAT start.
    /// Different number and a different hook.
    /// </summary>
    [Fact]
    public void FakeBloodVialHealsOneOnTurnOne()
    {
        var plain = Fight.WithRelics();
        var fake = Fight.WithRelics(RelicEffects.FakeBloodVial);
        plain.State.PlayerHp = 50;
        fake.State.PlayerHp = 50;
        RelicEffects.ApplyStartOfPlayerTurn(fake.State, new System.Random(0));

        Assert.Equal(51, fake.State.PlayerHp);
    }

    /// <summary>`FakeHappyFlower`: +1 energy every FIVE turns; the real one is three.</summary>
    /// <remarks>
    /// The fight has to be kept alive to see turn five at all -- an unattended combat
    /// against encounter 1 kills the player around turn four, and a dead player's
    /// `EndTurn` is a no-op that reads as "the relic never fired".
    /// </remarks>
    [Fact]
    public void FakeHappyFlowerIsAFiveTurnClock()
    {
        var fake = Fight.WithRelics(RelicEffects.FakeHappyFlower);
        fake.State.PlayerHp = 900;
        fake.State.PlayerMaxHp = 900;
        int baseline = fake.State.Energy;
        var energyByTurn = new List<int>();

        for (int turn = 0; turn < 6; turn++)
        {
            energyByTurn.Add(fake.State.Energy);
            fake.EndTurn();
        }

        // Turns one to six: only the fifth pays, where the real flower pays on the third.
        Assert.Equal(
            new List<int> { baseline, baseline, baseline, baseline, baseline + 1, baseline },
            energyByTurn
        );
    }

    /// <summary>The real flower on the same clock is three turns, not five.</summary>
    [Fact]
    public void TheRealFlowerIsStillThree()
    {
        var real = Fight.WithRelics(RelicEffects.HappyFlower);
        real.State.PlayerHp = 900;
        real.State.PlayerMaxHp = 900;
        int baseline = real.State.Energy;

        real.EndTurn();
        real.EndTurn();

        Assert.Equal(baseline + 1, real.State.Energy);
    }

    /// <summary>`FakeOrichalcum`: 3 block for ending a turn with none; the real one is 6.</summary>
    [Fact]
    public void FakeOrichalcumIsThreeBlock()
    {
        var fake = Fight.WithRelics(RelicEffects.FakeOrichalcum);
        fake.State.PlayerBlock = 0;

        RelicEffects.ApplyEndOfPlayerTurn(fake.State, new System.Random(0));

        Assert.Equal(3, fake.State.PlayerBlock);
    }

    /// <summary>And nothing when the turn ends with block already up.</summary>
    [Fact]
    public void FakeOrichalcumPaysNothingBehindABlock()
    {
        var fake = Fight.WithRelics(RelicEffects.FakeOrichalcum);
        fake.State.PlayerBlock = 5;

        RelicEffects.ApplyEndOfPlayerTurn(fake.State, new System.Random(0));

        Assert.Equal(5, fake.State.PlayerBlock);
    }

    /// <summary>
    /// `FakeStrikeDummy`: +1 on a Strike-TAGGED card, where the real dummy gives 3. The
    /// tag, not the name -- Perfected Strike is tagged and not basic, and the real dummy's
    /// arm read the name behind a comment saying tags were not extracted. They are.
    /// </summary>
    [Fact]
    public void FakeStrikeDummyAddsOneToATaggedStrike()
    {
        var plain = Fight.WithRelics().Energy(9).Enemy(hp: 300);
        var fake = Fight.WithRelics(RelicEffects.FakeStrikeDummy).Energy(9).Enemy(hp: 300);
        foreach (var fight in new[] { plain, fake })
        {
            fight.State.Hand.Clear();
            fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
            fight.Play(0);
        }

        Assert.Equal(plain.State.Enemies[0].Hp - 1, fake.State.Enemies[0].Hp);
    }

    /// <summary>A card with no Strike tag gets nothing.</summary>
    [Fact]
    public void AnUntaggedCardGetsNothing()
    {
        var plain = Fight.WithRelics().Energy(9).Enemy(hp: 300);
        var fake = Fight.WithRelics(RelicEffects.FakeStrikeDummy).Energy(9).Enemy(hp: 300);
        foreach (var fight in new[] { plain, fake })
        {
            fight.State.Hand.Clear();
            fight.State.Hand.Add(new CardInstance(IC.Bash, false));
            fight.Play(0);
        }

        Assert.Equal(plain.State.Enemies[0].Hp, fake.State.Enemies[0].Hp);
    }

    /// <summary>`FakeMango`: +3 max HP; the real Mango is 14.</summary>
    [Fact]
    public void FakeMangoIsThreeMaxHp()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        int max = engine.State.PlayerMaxHp;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.FakeMango);

        Assert.Equal(max + 3, engine.State.PlayerMaxHp);
    }

    /// <summary>
    /// `FakeLeesWaffle` heals a PERCENTAGE -- `MaxHp * 10 / 100` -- where the real waffle
    /// grants 7 flat max HP. A different mechanic, not just a smaller number.
    /// </summary>
    [Fact]
    public void FakeLeesWaffleHealsATenthOfMaxHp()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.PlayerHp = 10;
        int max = engine.State.PlayerMaxHp;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, RelicEffects.FakeLeesWaffle);

        Assert.Equal(10 + max * 10 / 100, engine.State.PlayerHp);
        Assert.Equal(max, engine.State.PlayerMaxHp);
    }

    /// <summary>
    /// `FakeMerchantsRug` and Wongo's badge declare no behaviour at all -- empty
    /// `RelicModel` bodies. Named in `NoEffectRelics` so "nobody wrote this" is
    /// distinguishable from "there is nothing to write".
    /// </summary>
    [Fact]
    public void TheRugAndTheBadgeDoNothingOnPurpose()
    {
        Assert.Contains(RelicEffects.FakeMerchantsRug, RelicEffects.NoEffectRelics);
        Assert.Contains(RelicEffects.WongoCustomerAppreciationBadge, RelicEffects.NoEffectRelics);

        var plain = Fight.WithRelics();
        var rug = Fight.WithRelics(RelicEffects.FakeMerchantsRug);

        Assert.Equal(plain.State.PlayerBlock, rug.State.PlayerBlock);
        Assert.Equal(plain.State.Energy, rug.State.Energy);
        Assert.Equal(plain.State.PlayerHp, rug.State.PlayerHp);
    }

    /// <summary>
    /// Fake Snecko Eye WAS a declared gap, waiting on Confused. Confused arrived with
    /// Snecko Eye (Tier B), so the fake is modelled now -- see `DarvRelicTests`.
    /// </summary>
    [Fact]
    public void FakeSneckoEyeIsNoLongerAGap()
    {
        Assert.DoesNotContain(RelicEffects.FakeSneckoEye, RelicEffects.UnmodelledInRun);
    }
}

/// <summary>
/// Venerable Tea Set was INERT for the whole run: its armed state was a synthetic
/// `VenerableTeaSetActive` relic id that nothing in the run ever added, and its two tests
/// drove that marker directly. Testing the seam is not testing the relic. E393.
/// </summary>
public class VenerableTeaSetTests
{
    private static RunEngine AtARestSite(params int[] relicIds)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        foreach (int id in relicIds)
        {
            engine.State.Relics.Add(new RelicInstance(id));
        }

        RelicEffects.ApplyAfterRoomEntered(engine.State, isRestSite: true, cameFromUnknown: false);
        return engine;
    }

    [Fact]
    public void RestingArmsTheTeaSet()
    {
        var engine = AtARestSite(RelicEffects.VenerableTeaSet);

        Assert.Equal(
            1,
            engine.State.Relics.First(r => r.DefId == RelicEffects.VenerableTeaSet).Counter
        );
    }

    [Fact]
    public void AnUnarmedTeaSetPoursNothing()
    {
        var plain = Fight.WithRelics();
        var teaSet = Fight.WithRelics(RelicEffects.VenerableTeaSet);

        Assert.Equal(plain.State.Energy, teaSet.State.Energy);
    }

    /// <summary>Armed, it pays two on the next combat's first energy reset -- once.</summary>
    [Fact]
    public void AnArmedTeaSetPoursTwoAndThenStops()
    {
        var plain = Fight.WithRelics();
        var teaSet = Fight.WithRelics(RelicEffects.VenerableTeaSet);
        int index = teaSet.State.Relics.FindIndex(r =>
            r.DefId == RelicEffects.VenerableTeaSet
        );
        teaSet.State.Relics[index] = teaSet.State.Relics[index] with { Counter = 1 };
        teaSet.State.Energy = plain.State.Energy;

        RelicEffects.ApplyStartOfPlayerTurn(teaSet.State, new System.Random(0));

        Assert.Equal(plain.State.Energy + 2, teaSet.State.Energy);
        Assert.Equal(0, teaSet.State.Relics[index].Counter);
    }

    /// <summary>The fake is the same arming rule at one energy.</summary>
    [Fact]
    public void TheFakeTeaSetPoursOne()
    {
        var fight = Fight.WithRelics(RelicEffects.FakeVenerableTeaSet);
        int index = fight.State.Relics.FindIndex(r =>
            r.DefId == RelicEffects.FakeVenerableTeaSet
        );
        fight.State.Relics[index] = fight.State.Relics[index] with { Counter = 1 };
        int before = fight.State.Energy;

        RelicEffects.ApplyStartOfPlayerTurn(fight.State, new System.Random(0));

        Assert.Equal(before + 1, fight.State.Energy);
    }
}

public class DollRoomAndEventRelicTests
{
    /// <summary>
    /// `DaughterOfTheWind`: 1 unpowered block per ATTACK played.
    /// </summary>
    [Fact]
    public void DaughterOfTheWindPaysABlockPerAttack()
    {
        var fight = Fight.WithRelics(RelicEffects.DaughterOfTheWind).Energy(9).Enemy(hp: 300);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.State.PlayerBlock = 0;

        fight.Play(0);
        fight.Play(0);

        Assert.Equal(2, fight.State.PlayerBlock);
    }

    /// <summary>
    /// `MrStruggles`: unpowered damage to every enemy equal to the TURN NUMBER, so it
    /// climbs -- 1, then 2, then 3.
    /// </summary>
    [Fact]
    public void MrStrugglesClimbsWithTheTurnNumber()
    {
        // Measured against a plain fight, because turn ONE's damage has already landed by
        // the time the combat is handed back -- reading the hp at that point and calling
        // it "before" loses the first tick.
        var plain = Fight.Encounter(3);
        var fight = Fight.Encounter(3, RelicEffects.MrStruggles);

        for (int i = 0; i < fight.State.Enemies.Count; i++)
        {
            Assert.Equal(plain.State.Enemies[i].Hp - 1, fight.State.Enemies[i].Hp);
        }

        plain.EndTurn();
        fight.EndTurn();

        // Turn two deals two, so three in total.
        for (int i = 0; i < fight.State.Enemies.Count; i++)
        {
            Assert.Equal(plain.State.Enemies[i].Hp - 3, fight.State.Enemies[i].Hp);
        }
    }

    /// <summary>
    /// `BingBong`: every card entering the DECK is cloned to the bottom -- and the clone
    /// must not clone itself, or one card would fill the deck.
    /// </summary>
    [Fact]
    public void BingBongDoublesEveryCardEnteringTheDeck()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(new RelicInstance(RelicEffects.BingBong));
        int before = engine.State.Deck.Count;

        RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(IC.Bash, Upgraded: false)
        );

        // One added, one cloned -- on top of the Bash the starter deck already holds.
        Assert.Equal(before + 2, engine.State.Deck.Count);
        Assert.Equal(3, engine.State.Deck.Count(c => c.DefId == IC.Bash));
    }

    /// <summary>It doubles a CURSE just as happily -- the doll does not read the card.</summary>
    [Fact]
    public void ItDoublesACurseToo()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(new RelicInstance(RelicEffects.BingBong));
        int decay = RunNonCombatEffects.NamedCard("Decay");

        RunNonCombatEffects.AddCardToDeck(engine.State, new CardInstance(decay, false));

        Assert.Equal(2, engine.State.Deck.Count(c => c.DefId == decay));
    }
}

public class ActTwoEventRelicTests
{
    /// <summary>
    /// `ForgottenSoul`: 1 unpowered damage to ONE random enemy per card exhausted --
    /// Charon's Ashes' shape at a tenth of the reach.
    /// </summary>
    [Fact]
    public void ForgottenSoulNicksOneEnemyPerExhaust()
    {
        var fight = Fight.Encounter(3, RelicEffects.ForgottenSoul);
        int before = fight.State.Enemies.Sum(e => e.Hp);

        CardEffects.ExhaustCard(
            fight.State,
            new CardInstance(430, false),
            rng: new System.Random(0)
        );

        Assert.Equal(before - 1, fight.State.Enemies.Sum(e => e.Hp));
    }

    /// <summary>
    /// `RoyalPoison`: 4 unblockable, unpowered damage to its OWN owner on turn one. The
    /// tea party's gift bites once a fight.
    /// </summary>
    [Fact]
    public void RoyalPoisonBitesItsOwnerOnTurnOne()
    {
        var fight = Fight.WithRelics(RelicEffects.RoyalPoison);
        fight.State.PlayerHp = 50;
        fight.State.PlayerBlock = 99;

        RelicEffects.ApplyStartOfPlayerTurn(fight.State, new System.Random(0));

        // Unblockable: the block does not save it.
        Assert.Equal(46, fight.State.PlayerHp);
    }

    /// <summary>`LostWisp`: 8 unpowered to every enemy whenever a POWER is played.</summary>
    [Fact]
    public void LostWispBurnsTheRoomOnAPower()
    {
        int power = GeneratedData.Cards.FindId("Inflame")!.Value;
        var fight = Fight.Encounter(3, RelicEffects.LostWisp).Energy(9);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(power, false));
        var before = fight.State.Enemies.Select(e => e.Hp).ToList();

        fight.Play(0);

        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i] - 8, fight.State.Enemies[i].Hp);
        }
    }

    /// <summary>An Attack is not a Power, and pays nothing.</summary>
    [Fact]
    public void AnAttackDoesNotWakeTheWisp()
    {
        var plain = Fight.Encounter(3).Energy(9);
        var wisp = Fight.Encounter(3, RelicEffects.LostWisp).Energy(9);
        foreach (var fight in new[] { plain, wisp })
        {
            fight.State.Hand.Clear();
            fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
            fight.Play(0);
        }

        Assert.Equal(
            plain.State.Enemies.Select(e => e.Hp),
            wisp.State.Enemies.Select(e => e.Hp)
        );
    }

    /// <summary>
    /// `PollinousCore`: two extra cards on every FOURTH turn's draw, and nothing on the
    /// three between.
    /// </summary>
    [Fact]
    public void PollinousCoreDrawsTwoMoreEveryFourthTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.PollinousCore);
        var extra = new List<int>();

        for (int turn = 0; turn < 4; turn++)
        {
            RelicEffects.TickPollinousCore(fight.State);
            extra.Add(RelicEffects.ExtraHandDraw(fight.State));
        }

        Assert.Equal(new List<int> { 0, 0, 0, 2 }, extra);
    }

    /// <summary>And the clock restarts rather than sticking on.</summary>
    [Fact]
    public void TheCoreResetsAfterItPays()
    {
        var fight = Fight.WithRelics(RelicEffects.PollinousCore);
        for (int turn = 0; turn < 4; turn++)
        {
            RelicEffects.TickPollinousCore(fight.State);
        }

        Assert.Equal(2, RelicEffects.ExtraHandDraw(fight.State));

        RelicEffects.TickPollinousCore(fight.State);
        Assert.Equal(0, RelicEffects.ExtraHandDraw(fight.State));
    }
}

public class WongosMysteryTicketTests
{
    private static RunEngine WithTicket()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(new RelicInstance(RelicEffects.WongosMysteryTicket));
        return engine;
    }

    /// <summary>Five combats, then THREE relics, then it is spent for good.</summary>
    [Fact]
    public void ItPaysThreeRelicsAfterFiveCombats()
    {
        var engine = WithTicket();

        for (int fight = 1; fight <= 4; fight++)
        {
            RelicEffects.CountCombatForWongosTicket(engine.State);
            Assert.False(RelicEffects.WongosTicketPaysOut(engine.State));
        }

        RelicEffects.CountCombatForWongosTicket(engine.State);
        Assert.True(RelicEffects.WongosTicketPaysOut(engine.State));

        RunRewardGenerator.GenerateCombatRewards(engine.State);

        Assert.Equal(3, engine.State.PendingBonusRelicRewards.Count);
        Assert.Equal(3, engine.State.PendingBonusRelicRewards.Distinct().Count());
    }

    [Fact]
    public void ItNeverPaysTwice()
    {
        var engine = WithTicket();
        for (int fight = 1; fight <= 5; fight++)
        {
            RelicEffects.CountCombatForWongosTicket(engine.State);
        }

        RelicEffects.RetireWongosTicket(engine.State);

        Assert.False(RelicEffects.WongosTicketPaysOut(engine.State));
        RelicEffects.CountCombatForWongosTicket(engine.State);
        Assert.False(RelicEffects.WongosTicketPaysOut(engine.State));
    }
}
