using System.Linq;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Fake Merchant: six of nine fake relics on a stall at 50 gold apiece, or a Foul
/// Potion in the merchant's face and take the lot off his corpse.
/// </summary>
/// <remarks>
/// This was the last event in <c>EventCoverageTests.Pending</c> with a note saying it
/// needed "a shop-shaped capture", and the note was pointing at the real problem: the
/// event has no options at all. <c>GenerateInitialOptions</c> returns
/// <c>Array.Empty</c> and <c>LayoutType</c> is <c>Custom</c>, because the stall is a
/// <c>MerchantInventory</c> -- the same class the real shop uses -- rather than a list of
/// choices. Six slots do not fit in the three an event gets, so it has a phase.
///
/// The emulator's stand-in took one relic off the pool queue for free. Wrong relic, wrong
/// price, wrong count, and it skipped the only interesting decision in the room.
///
/// That decision is the throw. <c>FoulPotionThrown</c> hands over Fake Merchant's Rug AND
/// every relic still on the shelf, so buying at 50 is buying a relic out of your own loot
/// pile -- the stall is worth strictly more robbed than shopped, and the only cost is a
/// 175-hp fight and the potion.
/// </remarks>
[CoversEvent("FakeMerchant")]
public class FakeMerchantTests
{
    private static RunEngine AtTheStall(int gold = 500, bool withFoulPotion = false)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventFakeMerchant;
        engine.State.Gold = gold;
        if (withFoulPotion)
        {
            engine.State.PotionSlots[0] = RunNonCombatEffects.NamedPotion("FoulPotion");
        }

        engine.Step(0, -1, out _, out _, out _);
        return engine;
    }

    private static int[] Mask(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return mask;
    }

    [Fact]
    public void WalkingUpToTheStallOpensItsOwnScreen()
    {
        var engine = AtTheStall();

        Assert.Equal(RunPhase.FakeMerchant, engine.State.Phase);
    }

    /// <summary>
    /// `_inventoryRelics.UnstableShuffle(Rng).Take(6)`: six of the nine, all distinct.
    /// </summary>
    [Fact]
    public void TheStallCarriesSixOfTheNineFakes()
    {
        var engine = AtTheStall();
        var stall = RunNonCombatEffects.FakeMerchantStock(engine.State);

        Assert.Equal(6, stall.Count);
        Assert.Equal(6, stall.Distinct().Count());
        foreach (int relicId in stall)
        {
            Assert.StartsWith("Fake", GeneratedData.Relics.Get(relicId).Name);
        }
    }

    /// <summary>
    /// The game's own comment: "don't add FakeMerchantsRug to this list, it's a combat
    /// reward, not a relic you can buy."
    /// </summary>
    [Fact]
    public void TheRugIsNotForSale()
    {
        var engine = AtTheStall();

        Assert.DoesNotContain(
            RunNonCombatEffects.NamedRelic("FakeMerchantsRug"),
            RunNonCombatEffects.FakeMerchantStock(engine.State)
        );
    }

    /// <summary>`BeforeEventStarted` rolls the shelf once; looking again does not reroll it.</summary>
    [Fact]
    public void TheShelfDoesNotReshuffleWhenThePlayerLooksAgain()
    {
        var engine = AtTheStall();
        var first = RunNonCombatEffects.FakeMerchantStock(engine.State).ToList();

        Mask(engine);
        Mask(engine);

        Assert.Equal(first, RunNonCombatEffects.FakeMerchantStock(engine.State));
    }

    [Fact]
    public void EverySlotCostsFifty()
    {
        var engine = AtTheStall(gold: 500);
        int relics = engine.State.Relics.Count;
        int wanted = RunNonCombatEffects.FakeMerchantStock(engine.State)[2];

        Assert.Equal(0, engine.Step(2, -1, out _, out _, out _));

        Assert.Equal(450, engine.State.Gold);
        Assert.Equal(relics + 1, engine.State.Relics.Count);
        Assert.Contains(engine.State.Relics, relic => relic.DefId == wanted);
    }

    /// <summary>
    /// A bought slot is emptied rather than removed: the indices have to hold still for
    /// the rest of the visit, and an emptied slot is also one fewer relic the fight pays.
    /// </summary>
    [Fact]
    public void ABoughtSlotGoesQuietAndTheOthersKeepTheirIndices()
    {
        var engine = AtTheStall();
        var before = RunNonCombatEffects.FakeMerchantStock(engine.State).ToList();

        engine.Step(2, -1, out _, out _, out _);
        var after = RunNonCombatEffects.FakeMerchantStock(engine.State);

        Assert.Equal(0, after[2]);
        Assert.Equal(before[3], after[3]);
        Assert.Equal(0, Mask(engine)[2]);
        Assert.Equal(-1, engine.Step(2, -1, out _, out _, out _));
    }

    [Fact]
    public void ARunThatCannotAffordASlotIsNotOfferedIt()
    {
        var engine = AtTheStall(gold: 49);

        Assert.Equal(0, Mask(engine)[0]);
        Assert.Equal(-1, engine.Step(0, -1, out _, out _, out _));
    }

    /// <summary>The throw only exists while the belt holds a Foul Potion.</summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void ThrowingIsOfferedOnlyWithAFoulPotionInTheBelt(bool carrying, int offered)
    {
        var engine = AtTheStall(withFoulPotion: carrying);

        Assert.Equal(offered, Mask(engine)[RunConstants.FakeMerchantThrowAction]);
    }

    [Fact]
    public void ThrowingSpendsThePotionAndStartsTheFight()
    {
        var engine = AtTheStall(withFoulPotion: true);

        engine.Step(RunConstants.FakeMerchantThrowAction, -1, out _, out _, out _);

        Assert.Equal(RunPhase.Combat, engine.State.Phase);
        Assert.DoesNotContain(
            RunNonCombatEffects.NamedPotion("FoulPotion"),
            engine.State.PotionSlots.ToArray()
        );
    }

    /// <summary>
    /// The rug first, then every relic still on the shelf. Robbing a full stall is seven
    /// relics for one potion.
    /// </summary>
    [Fact]
    public void TheFightPaysTheRugAndTheWholeShelf()
    {
        var engine = AtTheStall(withFoulPotion: true);
        var shelf = RunNonCombatEffects.FakeMerchantStock(engine.State).ToList();

        engine.Step(RunConstants.FakeMerchantThrowAction, -1, out _, out _, out _);

        Assert.Equal(
            new[] { RunNonCombatEffects.NamedRelic("FakeMerchantsRug") }.Concat(shelf).ToList(),
            engine.State.PendingBonusRelicRewards
        );
    }

    /// <summary>A relic already bought is one the corpse cannot pay for again.</summary>
    [Fact]
    public void BuyingFirstMakesTheRobberySmaller()
    {
        var engine = AtTheStall(withFoulPotion: true);
        engine.Step(2, -1, out _, out _, out _);

        engine.Step(RunConstants.FakeMerchantThrowAction, -1, out _, out _, out _);

        Assert.Equal(6, engine.State.PendingBonusRelicRewards.Count);
    }

    [Fact]
    public void LeavingIsAlwaysOnTheTable()
    {
        var engine = AtTheStall(gold: 0);

        Assert.Equal(1, Mask(engine)[RunConstants.FakeMerchantLeaveAction]);
        engine.Step(RunConstants.FakeMerchantLeaveAction, -1, out _, out _, out _);
        Assert.NotEqual(RunPhase.FakeMerchant, engine.State.Phase);
    }

    /// <summary>
    /// `Gold >= 100 || holds a Foul Potion`. The potion half is the price of the ROBBERY,
    /// not of a purchase, so a broke player carrying one still gets shown the stall.
    /// </summary>
    [Theory]
    [InlineData(100, false, true)]
    [InlineData(99, false, false)]
    [InlineData(0, true, true)]
    public void TheStallOnlyOpensForACustomerOrAnArmedRobber(
        int gold,
        bool foulPotion,
        bool allowed
    )
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Gold = gold;
        if (foulPotion)
        {
            engine.State.PotionSlots[0] = RunNonCombatEffects.NamedPotion("FoulPotion");
        }

        Assert.Equal(
            allowed,
            RunNonCombatEffects.IsEventAllowedForRun(engine.State, RunConstants.EventFakeMerchant)
        );
    }
}
