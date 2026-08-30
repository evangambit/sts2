using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Three act-one events whose emulator arms were placeholders — none of the six options
/// did anything the model does.
/// </summary>
/// <remarks>
/// They were the first three of the eighteen events with no live fixture and no test, and
/// all three were invented: Bugslayer paid gold and a random upgraded card where the model
/// adds one named card, Lost Wisp healed as if at a rest site, and Hungry For Mushrooms
/// handed out max HP and a potion where the model hands out two relics.
///
/// The capture tests in <c>EventCaptures.g.cs</c> assert the outcomes against the game.
/// What is here is the half a capture cannot show: that the mushrooms' HP swing belongs to
/// the RELIC rather than to the event, so it lands however the relic is obtained.
/// </remarks>
[CoversEvent("Bugslayer")]
[CoversEvent("LostWisp")]
[CoversEvent("HungryForMushrooms")]
public class MushroomsWispBugslayerTests
{
    private static RunEngine At(int eventId, string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = eventId;
        return engine;
    }

    private static int Card(string name) => RunNonCombatEffects.NamedCard(name);

    private static int Relic(string name) => RunNonCombatEffects.NamedRelic(name);

    // ── Bugslayer ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "Exterminate")]
    [InlineData(1, "Squash")]
    public void BugslayerAddsOneNamedCardAndNothingElse(int option, string card)
    {
        var engine = At(RunConstants.EventBugslayer);
        int goldBefore = engine.State.Gold;
        int hpBefore = engine.State.PlayerHp;
        int deckBefore = engine.State.Deck.Count;

        engine.Step(option, -1, out _, out _, out _);

        Assert.Equal(goldBefore, engine.State.Gold);
        Assert.Equal(hpBefore, engine.State.PlayerHp);
        Assert.Equal(deckBefore + 1, engine.State.Deck.Count);
        Assert.Equal(Card(card), engine.State.Deck[^1].DefId);
        Assert.False(engine.State.Deck[^1].Upgraded);
    }

    // ── Lost Wisp ────────────────────────────────────────────────────────────

    [Fact]
    public void ClaimTakesTheRelicAndTheCurse()
    {
        var engine = At(RunConstants.EventLostWisp);
        int goldBefore = engine.State.Gold;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Contains(engine.State.Relics, r => r.DefId == Relic("LostWisp"));
        Assert.Contains(engine.State.Deck, c => c.DefId == Card("Decay"));
        Assert.Equal(goldBefore, engine.State.Gold);
    }

    /// <summary>
    /// Search is `GoldVar(60)` jittered by the EVENT's own stream, not by `Rng.UpFront` —
    /// the shared `EventGoldAmount` helper paid 72 where the game paid 70.
    /// </summary>
    [Fact]
    public void SearchPaysSixtyGoldGiveOrTakeFifteen()
    {
        var engine = At(RunConstants.EventLostWisp);
        int before = engine.State.Gold;

        engine.Step(1, -1, out _, out _, out _);

        int paid = engine.State.Gold - before;
        Assert.InRange(paid, 45, 75);
        Assert.DoesNotContain(engine.State.Deck, c => c.DefId == Card("Decay"));
        Assert.Empty(engine.State.Relics.Where(r => r.DefId == Relic("LostWisp")));
    }

    // ── Hungry For Mushrooms ─────────────────────────────────────────────────

    [Fact]
    public void EachOptionTakesItsOwnMushroom()
    {
        var big = At(RunConstants.EventHungryForMushrooms);
        big.Step(0, -1, out _, out _, out _);
        Assert.Contains(big.State.Relics, r => r.DefId == Relic("BigMushroom"));

        var fragrant = At(RunConstants.EventHungryForMushrooms);
        fragrant.Step(1, -1, out _, out _, out _);
        Assert.Contains(fragrant.State.Relics, r => r.DefId == Relic("FragrantMushroom"));
    }

    /// <summary>
    /// `MaxHpVar(20)`, and `GainMaxHp` heals by the same amount — the live capture went
    /// from 64/80 to 84/100. The emulator used to give 7 max HP from the event.
    /// </summary>
    [Fact]
    public void BigMushroomIsTwentyMaxHpAndTheHealThatComesWithIt()
    {
        var engine = At(RunConstants.EventHungryForMushrooms);
        int maxBefore = engine.State.PlayerMaxHp;
        int hpBefore = engine.State.PlayerHp;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(maxBefore + 20, engine.State.PlayerMaxHp);
        Assert.Equal(hpBefore + 20, engine.State.PlayerHp);
    }

    /// <summary>
    /// `HpLossVar(15)` and two upgradable deck cards upgraded off `Rng.Niche` — with NO
    /// type filter, unlike War Paint's Skills and Whetstone's Attacks.
    /// </summary>
    [Fact]
    public void FragrantMushroomCostsFifteenAndUpgradesTwo()
    {
        var engine = At(RunConstants.EventHungryForMushrooms);
        int hpBefore = engine.State.PlayerHp;
        int upgradedBefore = engine.State.Deck.Count(c => c.Upgraded);

        engine.Step(1, -1, out _, out _, out _);

        Assert.Equal(hpBefore - 15, engine.State.PlayerHp);
        Assert.Equal(upgradedBefore + 2, engine.State.Deck.Count(c => c.Upgraded));
    }

    /// <summary>
    /// The payload is the RELIC's, not the event's: obtaining it any other way does the
    /// same thing. That is the half of the reading a capture of the event cannot show.
    /// </summary>
    [Fact]
    public void TheMushroomsPayOutWhereverTheyComeFrom()
    {
        var engine = At(RunConstants.EventHungryForMushrooms);
        int maxBefore = engine.State.PlayerMaxHp;

        RunNonCombatEffects.ApplyRelicPickup(engine.State, Relic("BigMushroom"));

        Assert.Equal(maxBefore + 20, engine.State.PlayerMaxHp);
    }
}

/// <summary>
/// Two more of the eighteen. Infested Automaton's options both add ONE card from the
/// character's own pool, filtered — a Power, or anything printed at zero energy that is
/// not an X card — where the emulator opened a card-reward screen or charged 10 HP for a
/// relic. The Lantern Key had its two options the wrong way round AND wrong: it handed out
/// the key card and a relic for free, where the game pays flat gold for returning it and
/// makes you beat a knight to keep it.
/// </summary>
[CoversEvent("InfestedAutomaton")]
[CoversEvent("TheLanternKey")]
public class AutomatonAndLanternKeyTests
{
    private static RunEngine At(int eventId, string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = eventId;
        return engine;
    }

    [Fact]
    public void StudyAddsAPowerAndTouchCoreAddsAZeroCostCard()
    {
        var study = At(RunConstants.EventInfestedAutomaton);
        int before = study.State.Deck.Count;
        study.Step(0, -1, out _, out _, out _);

        Assert.Equal(before + 1, study.State.Deck.Count);
        Assert.Equal(CardType.Power, GeneratedData.Cards.Get(study.State.Deck[^1].DefId).Type);

        var core = At(RunConstants.EventInfestedAutomaton);
        core.Step(1, -1, out _, out _, out _);

        var added = GeneratedData.Cards.Get(core.State.Deck[^1].DefId);
        Assert.Equal(0, added.Cost);
        Assert.False(added.HasEnergyCostX);
    }

    /// <summary>Neither option costs HP or gold, and neither opens a screen.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NeitherAutomatonOptionChargesAnything(int option)
    {
        var engine = At(RunConstants.EventInfestedAutomaton);
        int hp = engine.State.PlayerHp;
        int gold = engine.State.Gold;

        engine.Step(option, -1, out _, out _, out _);

        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(gold, engine.State.Gold);
        Assert.DoesNotContain(engine.State.Relics, r => r.DefId != 36);
    }

    /// <summary>`GoldVar(100)` flat — `Gold.BaseValue`, with none of Lost Wisp's jitter.</summary>
    [Fact]
    public void ReturningTheKeyPaysExactlyOneHundred()
    {
        var engine = At(RunConstants.EventTheLanternKey);
        int before = engine.State.Gold;

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(before + 100, engine.State.Gold);
        Assert.DoesNotContain(
            engine.State.Deck,
            c => c.DefId == RunNonCombatEffects.NamedCard("LanternKey")
        );
    }

    /// <summary>Keeping it opens a page whose ONE option is the fight.</summary>
    [Fact]
    public void KeepingTheKeyOpensTheFightPage()
    {
        var engine = At(RunConstants.EventTheLanternKey);

        engine.Step(1, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Event, engine.State.Phase);
        Assert.Equal(1, engine.State.EventPage);
        Assert.DoesNotContain(
            engine.State.Deck,
            c => c.DefId == RunNonCombatEffects.NamedCard("LanternKey")
        );
    }

    /// <summary>
    /// And the fight is a real combat whose reward is the key — a `SpecialCardReward`, so
    /// it is owed outright rather than rolled among three.
    /// </summary>
    [Fact]
    public void TheFightOwesTheKeyAsItsReward()
    {
        var engine = At(RunConstants.EventTheLanternKey);
        engine.Step(1, -1, out _, out _, out _);

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.Equal(
            RunNonCombatEffects.NamedCard("LanternKey"),
            engine.State.PendingSpecialCardReward
        );
    }
}
