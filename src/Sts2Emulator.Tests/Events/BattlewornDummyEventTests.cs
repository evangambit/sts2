using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Battleworn Dummy: three settings, and each one is a FIGHT.
/// </summary>
/// <remarks>
/// A Battle Friend of 75, 150 or 300 HP that never attacks and carries
/// `BattlewornDummyTimeLimitPower` at 3 — three of its own turns and it escapes. Beat the
/// clock and the event pays a potion, two upgrades, or a relic; miss it and the dummy
/// walks off with the reward. The whole event is a damage check against a timer.
///
/// The emulator paid 40/60/80 gold and a card, with no fight at all. The live capture's
/// option text is exact: "Fight a 75 HP dummy. Procure 1 random Potion."
///
/// This is also the first event that RESUMES after a combat — the room was never the
/// fight's (`ShouldGiveRewards => false` on the encounter), so the event pays rather than
/// the reward screen.
/// </remarks>
[CoversEvent("BattlewornDummy")]
public class BattlewornDummyEventTests
{
    private static RunEngine At(string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventBattlewornDummy;
        return engine;
    }

    [Theory]
    [InlineData(0, 75)]
    [InlineData(1, 150)]
    [InlineData(2, 300)]
    public void EachSettingStartsItsOwnFight(int option, int dummyHp)
    {
        var engine = At();

        engine.Step(option, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        var dummy = Assert.Single(engine.State.ActiveCombat!.Enemies);
        Assert.Equal(dummyHp, dummy.MaxHp);
        Assert.Equal(3, BuffSystem.Get(dummy.Buffs, BuffId.BattlewornDummyTimeLimit));
    }

    /// <summary>No gold, and no card — the emulator's whole old payout.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ChoosingASettingPaysNothingUpFront(int option)
    {
        var engine = At();
        int gold = engine.State.Gold;
        int deck = engine.State.Deck.Count;

        engine.Step(option, -1, out _, out _, out _);

        Assert.Equal(gold, engine.State.Gold);
        Assert.Equal(deck, engine.State.Deck.Count);
    }

    /// <summary>Killing it in time pays the setting's reward, and the FIGHT pays nothing.</summary>
    [Fact]
    public void KillingTheThirdDummyPaysARelic()
    {
        var engine = At();
        engine.Step(2, -1, out _, out _, out _);
        int relics = engine.State.Relics.Count;

        engine.State.ActiveCombat!.Enemies[0].Hp = 0;
        engine.Step(engine.State.ActiveCombat.Hand.Count, -1, out _, out _, out _);

        Assert.Equal(relics + 1, engine.State.Relics.Count);
        Assert.NotEqual(RunPhase.Combat, engine.State.Phase);
    }

    [Fact]
    public void KillingTheSecondDummyUpgradesTwoCards()
    {
        var engine = At();
        engine.Step(1, -1, out _, out _, out _);
        int upgraded = engine.State.Deck.Count(c => c.Upgraded);

        engine.State.ActiveCombat!.Enemies[0].Hp = 0;
        engine.Step(engine.State.ActiveCombat.Hand.Count, -1, out _, out _, out _);

        Assert.Equal(upgraded + 2, engine.State.Deck.Count(c => c.Upgraded));
    }

    [Fact]
    public void KillingTheFirstDummyProcuresAPotion()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);
        int potions = engine.State.PotionSlots.Count(p => p != 0);

        engine.State.ActiveCombat!.Enemies[0].Hp = 0;
        engine.Step(engine.State.ActiveCombat.Hand.Count, -1, out _, out _, out _);

        Assert.Equal(potions + 1, engine.State.PotionSlots.Count(p => p != 0));
    }

    /// <summary>
    /// And running out of time pays NOTHING. The dummy escapes rather than dying, so the
    /// run walks away with the HP it spent and none of the reward.
    /// </summary>
    [Fact]
    public void LettingTheClockRunOutPaysNothing()
    {
        var engine = At();
        engine.Step(2, -1, out _, out _, out _);
        int relics = engine.State.Relics.Count;
        int gold = engine.State.Gold;

        // Three of the dummy's turns and it walks off.
        for (int turn = 0; turn < 3; turn++)
        {
            engine.Step(engine.State.ActiveCombat!.Hand.Count, -1, out _, out _, out _);
        }

        Assert.Equal(relics, engine.State.Relics.Count);
        Assert.Equal(gold, engine.State.Gold);
        Assert.NotEqual(RunPhase.Combat, engine.State.Phase);
    }
}

/// <summary>
/// `PerfectFit`, the enchantment Field Of Man-Sized Holes hands out. Its whole rule is
/// `ModifyShuffleOrder`: the card goes to the FRONT of the draw pile on every reshuffle
/// except the initial one.
/// </summary>
/// <remarks>
/// The emulator did not model it, and the event's second option carried an admitted TODO
/// — 12 HP and a relic, invented and belonging to no option of that event. A live capture
/// caught it as 64 HP in the game against 52 here.
/// </remarks>
public class PerfectFitTests
{
    private const int Strike = 472;
    private const int Defend = 131;

    [Fact]
    public void TheEnchantedCardIsFirstAfterAReshuffle()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200).Seed(4);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DiscardPile.Add(new CardInstance(Strike, false));
        }

        fight.State.DiscardPile.Add(
            new CardInstance(Defend, false) { Enchantment = Enchantment.PerfectFit }
        );

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.Equal(Enchantment.PerfectFit, fight.State.DrawPile[0].Enchantment);
    }

    /// <summary>
    /// Two of them both reach the front, and the one found LATER in the shuffled pile ends
    /// up in front — one hook per card, each `Remove` then `Insert(0)`, so the sequence
    /// reverses them. The game's order, not a choice made here.
    /// </summary>
    [Fact]
    public void TwoOfThemBothReachTheFront()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 200).Seed(4);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Add(
            new CardInstance(Strike, false) { Enchantment = Enchantment.PerfectFit }
        );
        fight.State.DiscardPile.Add(
            new CardInstance(Defend, false) { Enchantment = Enchantment.PerfectFit }
        );
        for (int i = 0; i < 4; i++)
        {
            fight.State.DiscardPile.Add(new CardInstance(Strike, false));
        }

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.Equal(Enchantment.PerfectFit, fight.State.DrawPile[0].Enchantment);
        Assert.Equal(Enchantment.PerfectFit, fight.State.DrawPile[1].Enchantment);
    }

    /// <summary>Enter Your Hole enchants a CHOSEN card and costs nothing.</summary>
    [Fact]
    public void EnterYourHoleEnchantsOneChosenCardForFree()
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventFieldOfManSizedHoles;
        int hp = engine.State.PlayerHp;
        int relics = engine.State.Relics.Count;

        engine.Step(1, -1, out _, out _, out _);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);

        engine.Step(0, -1, out _, out _, out _);

        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(relics, engine.State.Relics.Count);
        Assert.Contains(engine.State.Deck, c => c.Enchantment == Enchantment.PerfectFit);
    }
}
