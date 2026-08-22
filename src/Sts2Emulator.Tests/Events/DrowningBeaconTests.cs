using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The Drowning Beacon: a potion offered, or Max HP paid for a named relic.
///
/// The emulator had one option doing both halves of both -- Bottle charged the 13 Max HP
/// that belongs to Climb and pushed a potion straight into the belt, and Climb did not
/// exist at all, so the game's accepted option was refused. Bottle offers its potion on a
/// reward screen (<c>RewardsCmd.OfferCustom</c>), which is a decision the player actually
/// makes: they can decline it, or drop a held potion to make room.
/// </summary>
public class DrowningBeaconTests
{
    private static RunEngine AtTheBeacon(string seed = "ABCDEF")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.EventId = RunConstants.EventDrowningBeacon;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    [Fact]
    public void BottlingOffersTheGlowwaterPotionOnARewardScreen()
    {
        var engine = AtTheBeacon();
        int hp = engine.State.PlayerHp;
        int maxHp = engine.State.PlayerMaxHp;

        Assert.Equal(0, engine.Step(0, -1, out _, out _, out _));

        Assert.Equal(RunPhase.RelicReward, engine.State.Phase);
        Assert.Equal(RunNonCombatEffects.GlowwaterPotion, engine.State.RewardPotion);
        Assert.Equal(0, engine.State.RelicReward);
        Assert.Equal(0, engine.State.RewardGold);

        // The potion is offered, not taken: nothing is in the belt until it is claimed.
        Assert.All(engine.State.PotionSlots, slot => Assert.Equal(0, slot));
        Assert.Equal(hp, engine.State.PlayerHp);
        Assert.Equal(maxHp, engine.State.PlayerMaxHp);
    }

    [Fact]
    public void ClimbingCostsThirteenMaxHpAndObtainsTheFresnelLens()
    {
        var engine = AtTheBeacon();
        int maxHp = engine.State.PlayerMaxHp;

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(maxHp - 13, engine.State.PlayerMaxHp);
        Assert.Equal(RunNonCombatEffects.NamedRelic("FresnelLens"), engine.State.Relics[^1].DefId);
    }

    /// <summary>
    /// <c>CreatureCmd.LoseMaxHp</c> only damages by the amount the new cap falls BELOW
    /// current HP. A player at 64/80 keeps all 64 when the cap drops to 67; one at full
    /// health is dragged down with it.
    /// </summary>
    [Fact]
    public void ClimbingOnlyCostsCurrentHpWhenTheCapFallsBelowIt()
    {
        var engine = AtTheBeacon();
        Assert.Equal(64, engine.State.PlayerHp);
        Assert.Equal(80, engine.State.PlayerMaxHp);

        Assert.Equal(0, engine.Step(1, -1, out _, out _, out _));

        Assert.Equal(67, engine.State.PlayerMaxHp);
        Assert.Equal(64, engine.State.PlayerHp);

        var full = AtTheBeacon();
        full.State.PlayerHp = full.State.PlayerMaxHp;
        Assert.Equal(0, full.Step(1, -1, out _, out _, out _));
        Assert.Equal(67, full.State.PlayerMaxHp);
        Assert.Equal(67, full.State.PlayerHp);
    }
}
