using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The fourteen relics an ACT 1 run can actually be handed and that still did nothing.
/// </summary>
/// <remarks>
/// These were called unreachable, and they were not. `audit_relics.py --reachable` meant
/// "not in `EventRelicPool`", which is a different question -- events happen in Act 1 --
/// and it was also dropping the three relics that are in the event pool AND the shared
/// pool. Eleven of these come from events the emulator already runs (Tea Master, Trash
/// Heap, War Historian Repy, Sunken Statue); three come out of the ordinary shop and chest
/// queue. See E389.
/// </remarks>
public class TeaRelicTests
{
    private static RunEngine Run(params int[] relicIds)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        foreach (int id in relicIds)
        {
            RunNonCombatEffects.ApplyRelicPickup(engine.State, id);
        }

        return engine;
    }

    private static CombatState FirstFight(RunEngine engine)
    {
        engine.State.Phase = RunPhase.Map;
        for (int i = 0; i < engine.State.MapNodeTypes.Length; i++)
        {
            if (engine.State.MapNodeTypes[i] == RunConstants.NodeNormal)
            {
                engine.Step(i, -1, out _, out _, out _);
                break;
            }
        }

        return engine.State.ActiveCombat!;
    }

    /// <summary>
    /// A tea arrives CHARGED: its remaining-combats count is run state, so the relic has
    /// to be handed to the run already holding it rather than charged by its first fight.
    /// </summary>
    [Theory]
    [InlineData("BoneTea", 1)]
    [InlineData("EmberTea", 5)]
    [InlineData("TeaOfDiscourtesy", 1)]
    public void ATeaArrivesWithItsCombatsOnIt(string name, int combats)
    {
        var engine = Run(RunNonCombatEffects.NamedRelic(name));

        Assert.Equal(
            combats,
            engine.State.Relics.First(r => r.DefId == RunNonCombatEffects.NamedRelic(name)).Counter
        );
    }

    /// <summary>`BoneTea`: turn one, UPGRADE EVERY CARD IN HAND. One fight.</summary>
    [Fact]
    public void BoneTeaUpgradesTheWholeOpeningHand()
    {
        var combat = FirstFight(Run(RelicEffects.BoneTea));

        Assert.NotEmpty(combat.Hand);
        Assert.All(
            combat.Hand.Where(c => RunConstants.IsRunCardUpgradable(c with { Upgraded = false })),
            card => Assert.True(card.Upgraded)
        );
    }

    /// <summary>`EmberTea`: Strength 2 at the top of the fight, five fights' worth.</summary>
    [Fact]
    public void EmberTeaPoursTwoStrength()
    {
        var combat = FirstFight(Run(RelicEffects.EmberTea));

        Assert.Equal(2, BuffSystem.Get(combat.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>`TeaOfDiscourtesy`: two Dazed into the draw pile. The free tea's price.</summary>
    [Fact]
    public void TeaOfDiscourtesyBuriesTwoDazed()
    {
        var combat = FirstFight(Run(RelicEffects.TeaOfDiscourtesy));

        Assert.Equal(
            2,
            combat.DrawPile.Count(c => c.DefId == ST.Dazed)
                + combat.Hand.Count(c => c.DefId == ST.Dazed)
        );
    }

    /// <summary>
    /// The count comes home. A tea that spends a combat has one fewer on the RUN's relic
    /// afterwards, or every fight would be its first.
    /// </summary>
    [Fact]
    public void SpendingACombatTravelsBackToTheRun()
    {
        var engine = Run(RelicEffects.EmberTea);
        FirstFight(engine);
        engine.SyncAfterCombatForTest();

        Assert.Equal(4, engine.State.Relics.First(r => r.DefId == RelicEffects.EmberTea).Counter);
    }

    /// <summary>A spent tea is inert -- `IsUsedUp` is `CombatsLeft &lt;= 0`.</summary>
    [Fact]
    public void ASpentTeaPoursNothing()
    {
        var engine = Run(RelicEffects.BoneTea);
        int index = engine.State.Relics.FindIndex(r => r.DefId == RelicEffects.BoneTea);
        engine.State.Relics[index] = engine.State.Relics[index] with { Counter = 0 };

        var combat = FirstFight(engine);

        Assert.Contains(combat.Hand, card => !card.Upgraded);
    }
}

/// <summary>
/// Girya's lifts never reached a fight: the run copied its relic counters into the combat
/// AFTER `CombatFactory.Reset` returned, and `ApplyCombatStart` -- which is where they are
/// read -- runs inside it. Three lifts applied no Strength at all. Found while wiring the
/// teas, which need the same counter for the same reason. E390.
/// </summary>
public class RunRelicCounterTests
{
    [Fact]
    public void GiryasLiftsReachTheFight()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Relics.Add(new RelicInstance(RelicEffects.Girya, 3));

        engine.State.Phase = RunPhase.Map;
        for (int i = 0; i < engine.State.MapNodeTypes.Length; i++)
        {
            if (engine.State.MapNodeTypes[i] == RunConstants.NodeNormal)
            {
                engine.Step(i, -1, out _, out _, out _);
                break;
            }
        }

        Assert.Equal(3, BuffSystem.Get(engine.State.ActiveCombat!.PlayerBuffs, BuffId.Strength));
    }
}

public class BootAndDrillTests
{
    /// <summary>
    /// `TheBoot`: a hit that would take 1..4 off an enemy takes 5. A FLOOR, not a bonus.
    /// </summary>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    [InlineData(9, 9)]
    public void TheBootFloorsSmallHits(int dealt, int expected)
    {
        var plain = Fight.Encounter(3).Enemy(hp: 200);
        var boot = Fight.Encounter(3, RelicEffects.TheBoot).Enemy(hp: 200);

        CardEffects.DealUnpoweredDamage(plain.State, plain.State.Enemies[0], dealt);
        CardEffects.DealPoweredDamage(boot.State, boot.State.Enemies[0], dealt);

        Assert.Equal(200 - expected, boot.State.Enemies[0].Hp);
    }

    /// <summary>A fully blocked hit stays at zero -- the floor is on hp LOST.</summary>
    [Fact]
    public void TheBootDoesNotPunchThroughBlock()
    {
        var fight = Fight.Encounter(3, RelicEffects.TheBoot).Enemy(hp: 200);
        fight.State.Enemies[0].Block = 50;

        CardEffects.DealPoweredDamage(fight.State, fight.State.Enemies[0], 3);

        Assert.Equal(200, fight.State.Enemies[0].Hp);
    }

    /// <summary>
    /// `HandDrill`: Vulnerable 2 on an enemy whose block this hit BROKE -- block that was
    /// there, and a hit that got through it.
    /// </summary>
    [Fact]
    public void HandDrillPunishesABrokenShield()
    {
        var fight = Fight.Encounter(3, RelicEffects.HandDrill).Enemy(hp: 200);
        fight.State.Enemies[0].Block = 5;

        CardEffects.DealPoweredDamage(fight.State, fight.State.Enemies[0], 20);

        Assert.Equal(2, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Vulnerable));
    }

    /// <summary>An enemy with no block to start with did not have it broken.</summary>
    [Fact]
    public void AnUnshieldedEnemyIsNotDrilled()
    {
        var fight = Fight.Encounter(3, RelicEffects.HandDrill).Enemy(hp: 200);

        CardEffects.DealPoweredDamage(fight.State, fight.State.Enemies[0], 20);

        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Vulnerable));
    }

    /// <summary>Nor does a hit that fails to get through.</summary>
    [Fact]
    public void AHitThatDoesNotBreakThroughDrillsNothing()
    {
        var fight = Fight.Encounter(3, RelicEffects.HandDrill).Enemy(hp: 200);
        fight.State.Enemies[0].Block = 50;

        CardEffects.DealPoweredDamage(fight.State, fight.State.Enemies[0], 20);

        Assert.Equal(0, BuffSystem.Get(fight.State.Enemies[0].Buffs, BuffId.Vulnerable));
    }
}

public class RazorToothAndRougeTests
{
    /// <summary>
    /// `RazorTooth`: the Attack or Skill just played is upgraded on the copy, so it lands
    /// in the discard pile upgraded and comes back that way.
    /// </summary>
    [Fact]
    public void RazorToothUpgradesTheCardYouJustPlayed()
    {
        var fight = Fight.WithRelics(RelicEffects.RazorTooth).Energy(9).Enemy(hp: 200);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));

        fight.Play(0);

        Assert.Contains(
            fight.State.DiscardPile,
            card => card.DefId == IC.StrikeIronclad && card.Upgraded
        );
    }

    /// <summary>Powers are neither Attack nor Skill, so they are left alone.</summary>
    [Fact]
    public void APowerIsNotUpgraded()
    {
        int power = GeneratedData.Cards.FindId("Inflame")!.Value;
        var fight = Fight.WithRelics(RelicEffects.RazorTooth).Energy(9).Enemy(hp: 200);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(power, false));

        fight.Play(0);

        Assert.DoesNotContain(
            fight.State.AllCards(),
            card => card.DefId == power && card.Upgraded
        );
    }

    /// <summary>
    /// `SparklingRouge`: Strength 1 and Dexterity 1 when block clears on TURN THREE. Exact
    /// -- not "from turn three on".
    /// </summary>
    [Fact]
    public void SparklingRougePaysOnTurnThreeOnly()
    {
        var fight = Fight.WithRelics(RelicEffects.SparklingRouge);

        fight.EndTurn();
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        fight.EndTurn();
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Dexterity));

        fight.EndTurn();
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

public class HistoryCourseTests
{
    /// <summary>
    /// `HistoryCourse`: from turn two on, a DUPE of the last Attack or Skill played last
    /// turn is auto-played.
    /// </summary>
    [Fact]
    public void ItReplaysLastTurnsLastAttack()
    {
        var fight = Fight.WithRelics(RelicEffects.HistoryCourse).Energy(9).Enemy(hp: 300);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.Play(0);
        int afterFirstTurn = fight.State.Enemies[0].Hp;

        fight.EndTurn();

        Assert.True(
            fight.State.Enemies[0].Hp < afterFirstTurn,
            "turn two should open with a free replay of the Strike"
        );
    }

    /// <summary>Turn one has no last turn to read, so it replays nothing.</summary>
    [Fact]
    public void TurnOneReplaysNothing()
    {
        var plain = Fight.WithRelics().Enemy(hp: 300);
        var course = Fight.WithRelics(RelicEffects.HistoryCourse).Enemy(hp: 300);

        Assert.Equal(plain.State.Enemies[0].Hp, course.State.Enemies[0].Hp);
    }

    /// <summary>
    /// The dupe must not become next turn's answer -- `!e.CardPlay.Card.IsDupe` -- or the
    /// relic would latch onto one card forever off a single play.
    /// </summary>
    [Fact]
    public void TheReplayDoesNotFeedItself()
    {
        var fight = Fight.WithRelics(RelicEffects.HistoryCourse).Energy(9).Enemy(hp: 500);
        fight.State.Hand.Clear();
        fight.State.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        fight.Play(0);

        fight.EndTurn();
        int afterReplay = fight.State.Enemies[0].Hp;
        fight.EndTurn();

        // Turn three: nothing was played by the PLAYER on turn two, so there is nothing
        // to encore.
        Assert.Equal(afterReplay, fight.State.Enemies[0].Hp);
    }
}

public class RunLevelActOneRelicTests
{
    private static RunEngine Run(string name)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        RunNonCombatEffects.ApplyRelicPickup(engine.State, RunNonCombatEffects.NamedRelic(name));
        return engine;
    }

    /// <summary>
    /// `DarkstonePeriapt`: +6 max HP for every CURSE that enters the deck, however it
    /// arrives -- an event's Decay, a Neow drawback, a Normality.
    /// </summary>
    [Fact]
    public void DarkstonePeriaptPaysForEveryCurse()
    {
        var engine = Run("DarkstonePeriapt");
        int max = engine.State.PlayerMaxHp;

        RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(RunNonCombatEffects.NamedCard("Decay"), false)
        );
        RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(RunNonCombatEffects.NamedCard("Doubt"), false)
        );

        Assert.Equal(max + 12, engine.State.PlayerMaxHp);
    }

    /// <summary>An ordinary card pays nothing -- the hook reads the card TYPE.</summary>
    [Fact]
    public void AnOrdinaryCardPaysNothing()
    {
        var engine = Run("DarkstonePeriapt");
        int max = engine.State.PlayerMaxHp;

        RunNonCombatEffects.AddCardToDeck(
            engine.State,
            new CardInstance(IC.StrikeIronclad, false)
        );

        Assert.Equal(max, engine.State.PlayerMaxHp);
    }

    /// <summary>
    /// `MawBank`: 12 gold on entering a room, every room, until the player buys anything.
    /// </summary>
    [Fact]
    public void MawBankPaysOnEveryRoomUntilYouSpend()
    {
        var engine = Run("MawBank");
        int gold = engine.State.Gold;

        RunNonCombatEffects.PayMawBank(engine.State);
        RunNonCombatEffects.PayMawBank(engine.State);
        Assert.Equal(gold + 24, engine.State.Gold);

        RunNonCombatEffects.CloseMawBank(engine.State, goldSpent: 50);
        RunNonCombatEffects.PayMawBank(engine.State);

        Assert.Equal(gold + 24, engine.State.Gold);
    }

    /// <summary>A purchase that cost nothing does not close it: `goldSpent > 0`.</summary>
    [Fact]
    public void AFreePurchaseLeavesTheBankOpen()
    {
        var engine = Run("MawBank");
        int gold = engine.State.Gold;

        RunNonCombatEffects.CloseMawBank(engine.State, goldSpent: 0);
        RunNonCombatEffects.PayMawBank(engine.State);

        Assert.Equal(gold + 12, engine.State.Gold);
    }

    /// <summary>
    /// `SwordOfStone`: FIVE elite victories -- `DynamicVar("Elites", 5m)` -- and it is
    /// REPLACED by Sword of Jade. A replacement, so the relic list keeps its length.
    /// </summary>
    [Fact]
    public void SwordOfStoneBecomesJadeAfterFiveElites()
    {
        var engine = Run("SwordOfStone");
        int relics = engine.State.Relics.Count;

        for (int i = 0; i < 4; i++)
        {
            RunNonCombatEffects.CountEliteVictoryForSwordOfStone(engine.State, wasElite: true);
        }

        Assert.Contains(engine.State.Relics, r => r.DefId == RelicEffects.SwordOfStone);

        RunNonCombatEffects.CountEliteVictoryForSwordOfStone(engine.State, wasElite: true);

        Assert.DoesNotContain(engine.State.Relics, r => r.DefId == RelicEffects.SwordOfStone);
        Assert.Contains(engine.State.Relics, r => r.DefId == RelicEffects.SwordOfJade);
        Assert.Equal(relics, engine.State.Relics.Count);
    }

    /// <summary>An ordinary fight does not count towards it.</summary>
    [Fact]
    public void OnlyElitesCount()
    {
        var engine = Run("SwordOfStone");

        for (int i = 0; i < 10; i++)
        {
            RunNonCombatEffects.CountEliteVictoryForSwordOfStone(engine.State, wasElite: false);
        }

        Assert.Contains(engine.State.Relics, r => r.DefId == RelicEffects.SwordOfStone);
    }

    /// <summary>`SwordOfJade`: Strength 3 at the top of every fight thereafter.</summary>
    [Fact]
    public void SwordOfJadePaysThreeStrengthEveryFight()
    {
        var fight = Fight.WithRelics(RelicEffects.SwordOfJade);

        Assert.Equal(3, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

public class LastingCandyTests
{
    /// <summary>
    /// `IsInTriggeringCombat` is `CombatsSeen > 0 &amp;&amp; CombatsSeen % 2 == 0`, and
    /// `CombatsSeen` counts up as each fight ENDS -- so the second, fourth and sixth
    /// fights' reward screens are the ones that get the extra Power.
    /// </summary>
    /// <remarks>
    /// The screen itself is not modelled: the candy ADDS a fourth option and the emulator's
    /// reward screen has three slots, with `RewardSkipAction` sitting on index 3. See
    /// <c>RelicEffects.UnmodelledInRun</c> for why that is an action-space change rather
    /// than a relic change. The clock is pinned here so only the screen is left.
    /// </remarks>
    [Fact]
    public void TheCandyTriggersOnEverySecondCombat()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunNonCombatEffects.NamedRelic("LastingCandy")
        );

        var triggered = new List<bool>();
        for (int fightNumber = 1; fightNumber <= 4; fightNumber++)
        {
            RelicEffects.CountCombatForLastingCandy(engine.State);
            triggered.Add(RelicEffects.LastingCandyOffersAPower(engine.State));
        }

        Assert.Equal(new List<bool> { false, true, false, true }, triggered);
    }

    /// <summary>
    /// The declared gaps are exactly two, and both are declared for a reason that is not
    /// "nobody got to it": Lasting Candy needs a fourth reward slot, and Fake Snecko Eye
    /// needs a Confused power the emulator has no model for. Pinned so the list cannot
    /// quietly become a place to park work.
    /// </summary>
    [Fact]
    public void TheDeclaredRunGapsAreTheTwoKnownOnes()
    {
        Assert.Equal(2, RelicEffects.UnmodelledInRun.Length);
        Assert.Contains(RelicEffects.LastingCandy, RelicEffects.UnmodelledInRun);
        Assert.Contains(RelicEffects.FakeSneckoEye, RelicEffects.UnmodelledInRun);
    }
}

public class DreamCatcherTests
{
    /// <summary>
    /// `DreamCatcher.TryModifyRestSiteHealRewards`: resting also offers a card reward.
    /// </summary>
    [Fact]
    public void RestingAlsoOffersACard()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        RunNonCombatEffects.ApplyRelicPickup(
            engine.State,
            RunNonCombatEffects.NamedRelic("DreamCatcher")
        );
        engine.State.Phase = RunPhase.Rest;
        engine.State.RestOptionsTaken = 0;

        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);

        Assert.Equal(RunPhase.CardReward, engine.State.Phase);
        Assert.All(engine.State.RewardCards, id => Assert.NotEqual(0, id));
    }

    /// <summary>Without it, resting is just a rest.</summary>
    [Fact]
    public void WithoutItRestingOffersNothing()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Rest;
        engine.State.RestOptionsTaken = 0;

        engine.Step(RunConstants.RestHealAction, -1, out _, out _, out _);

        Assert.NotEqual(RunPhase.CardReward, engine.State.Phase);
    }
}
