using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The shared pool's rares. Same story as its commons: the reward code could hand any of
// these over and nothing would happen.

public class TungstenRodTests
{
    [Fact]
    public void EveryInstanceOfHpLossIsOneSmaller()
    {
        var fight = Fight.WithRelics(RelicEffects.TungstenRod);
        fight.State.PlayerBlock = 0;
        int hp = fight.State.PlayerHp;

        CardEffects.DealDamageToPlayer(fight.State, 10);

        Assert.Equal(hp - 9, fight.State.PlayerHp);
    }

    /// <summary>`Math.Max(0m, ...)` — one damage becomes none, not negative healing.</summary>
    [Fact]
    public void OneDamageBecomesNone()
    {
        var fight = Fight.WithRelics(RelicEffects.TungstenRod);
        int hp = fight.State.PlayerHp;

        CardEffects.DealDamageToPlayer(fight.State, 1);

        Assert.Equal(hp, fight.State.PlayerHp);
    }
}

public class BeatingRemnantTests
{
    /// <summary>
    /// A cap on the TURN's total unblocked damage, not on one hit — `Math.Min(amount,
    /// 20 - DamageReceivedThisTurn)`.
    /// </summary>
    [Fact]
    public void TwentyDamageATurnAndNoMore()
    {
        var fight = Fight.WithRelics(RelicEffects.BeatingRemnant);
        fight.State.PlayerHp = 500;
        fight.State.PlayerMaxHp = 500;
        fight.State.PlayerBlock = 0;

        CardEffects.DealDamageToPlayer(fight.State, 15);
        Assert.Equal(485, fight.State.PlayerHp);

        // Only five of the next fifteen can land.
        CardEffects.DealDamageToPlayer(fight.State, 15);
        Assert.Equal(480, fight.State.PlayerHp);

        CardEffects.DealDamageToPlayer(fight.State, 15);
        Assert.Equal(480, fight.State.PlayerHp);
    }

    [Fact]
    public void TheCapResetsWithTheTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.BeatingRemnant);
        fight.State.PlayerHp = 500;
        fight.State.PlayerMaxHp = 500;
        CardEffects.DealDamageToPlayer(fight.State, 40);
        Assert.Equal(480, fight.State.PlayerHp);

        fight.EndTurn();
        int afterEnemies = fight.State.PlayerHp;
        fight.State.PlayerBlock = 0;

        CardEffects.DealDamageToPlayer(fight.State, 15);

        Assert.True(afterEnemies - fight.State.PlayerHp > 0);
    }
}

public class ChandelierTests
{
    /// <summary>Turn THREE only.</summary>
    [Fact]
    public void ThreeEnergyOnTheThirdTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.Chandelier);
        fight.State.PlayerHp = 999;
        int baseline = fight.State.MaxEnergy;

        fight.EndTurn();
        Assert.Equal(baseline, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(baseline + 3, fight.State.Energy);

        fight.EndTurn();
        Assert.Equal(baseline, fight.State.Energy);
    }
}

public class BellowsTests
{
    /// <summary>The OPENING hand is upgraded, and only the opening hand.</summary>
    [Fact]
    public void TheOpeningHandArrivesUpgraded()
    {
        var fight = Fight.WithRelics(RelicEffects.Bellows);

        Assert.NotEmpty(fight.State.Hand);
        Assert.All(fight.State.Hand, c => Assert.True(c.Upgraded));
    }

    [Fact]
    public void LaterHandsAreNot()
    {
        var fight = Fight.WithRelics(RelicEffects.Bellows);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Contains(fight.State.Hand, c => !c.Upgraded);
    }
}

public class IceCreamTests
{
    /// <summary>
    /// `ShouldPlayerResetEnergy` is false from turn two onwards, so energy CARRIES rather
    /// than refilling. Turn one still resets, which is what puts energy on the board.
    /// </summary>
    [Fact]
    public void EnergyCarriesFromTurnTwoOnwards()
    {
        // Carrying is only visible once something has been SPENT: an unspent turn looks
        // identical either way, because the carried amount and the refill are both the
        // maximum. So spend one and check the turn starts short.
        var fight = Fight.WithRelics(RelicEffects.IceCream);
        fight.State.PlayerHp = 999;
        fight.State.Hand = [Card(SI.StrikeSilent)];
        int max = fight.State.MaxEnergy;

        fight.Play();
        Assert.Equal(max - 1, fight.State.Energy);

        fight.EndTurn();

        Assert.Equal(max - 1, fight.State.Energy);
    }
}

public class GamePieceTests
{
    [Fact]
    public void PlayingAPowerDrawsACard()
    {
        var fight = Fight.WithRelics(RelicEffects.GamePiece);
        fight.State.Hand = [Card(IC.Inflame), Card(SI.Slice)];
        fight.State.Energy = 9;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play(); // the Power
        Assert.Equal(2, fight.State.Hand.Count); // the Slice, plus the card it drew

        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == SI.Slice));
        Assert.Single(fight.State.Hand); // an attack draws nothing
    }
}

public class IntimidatingHelmetTests
{
    /// <summary>
    /// Four unpowered block when the card actually COST two or more —
    /// `cardPlay.Resources.EnergyValue`, so a free play pays nothing.
    /// </summary>
    [Fact]
    public void ACostlyCardGivesBlock()
    {
        var fight = Fight.WithRelics(RelicEffects.IntimidatingHelmet);
        fight.State.Hand = [Card(SI.LegSweep), Card(SI.Slice)];
        fight.State.Energy = 9;

        fight.Play();
        Assert.Equal(11 + 4, fight.State.PlayerBlock);

        int before = fight.State.PlayerBlock;
        fight.Play();
        Assert.Equal(before, fight.State.PlayerBlock);
    }
}

public class RainbowRingTests
{
    /// <summary>
    /// One Attack, one Skill and one Power in a turn pays 1 Strength and 1 Dexterity —
    /// once a turn, `ActivationCountThisTurn < 1`.
    /// </summary>
    [Fact]
    public void AllThreeTypesInATurnPaysOnce()
    {
        var fight = Fight.WithRelics(RelicEffects.RainbowRing);
        fight.State.PlayerHp = 999;
        fight.State.Energy = 99;
        fight.State.Hand =
        [
            Card(SI.Slice),
            Card(SI.DefendSilent),
            Card(IC.Inflame),
            Card(SI.Slice),
            Card(SI.DefendSilent),
            Card(IC.Inflame),
        ];

        fight.Play();
        fight.Play();
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Dexterity));

        fight.Play(); // the Power completes the set
        // Dexterity rather than Strength, because Inflame grants Strength of its own and
        // the two would be indistinguishable.
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));

        // A second set in the same turn pays nothing.
        while (fight.State.Hand.Count > 0)
        {
            fight.Play(0);
        }

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Dexterity));
    }

    [Fact]
    public void TheCountersResetWithTheTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.RainbowRing);
        fight.State.PlayerHp = 999;
        fight.State.Energy = 99;
        fight.State.Hand = [Card(SI.Slice), Card(SI.DefendSilent)];
        fight.Play();
        fight.Play();

        fight.EndTurn();

        Assert.Equal(0, fight.State.RainbowRingAttacks);
        Assert.False(fight.State.RainbowRingPaidThisTurn);
    }
}

public class SturdyClampTests
{
    /// <summary>
    /// Block survives the turn start but is trimmed to ten — not Barricade, which keeps
    /// all of it.
    /// </summary>
    [Fact]
    public void BlockSurvivesButOnlyTenOfIt()
    {
        var fight = Fight.WithRelics(RelicEffects.SturdyClamp);
        fight.State.PlayerHp = 999;
        fight.State.PlayerBlock = 40;

        fight.EndTurn();

        Assert.Equal(10, fight.State.PlayerBlock);
    }

    [Fact]
    public void LessThanTenIsKeptWhole()
    {
        var fight = Fight.WithRelics(RelicEffects.SturdyClamp);
        fight.State.PlayerHp = 999;
        fight.State.Enemies.Clear();
        fight.State.Enemies.Add(
            new EnemyState
            {
                DefId = 16,
                Hp = 200,
                MaxHp = 200,
            }
        );
        fight.State.PlayerBlock = 6;

        fight.EndTurn();

        Assert.Equal(6, fight.State.PlayerBlock);
    }
}

public class UnceasingTopTests
{
    /// <summary>Emptying the hand during the play phase draws a card.</summary>
    [Fact]
    public void AnEmptiedHandDrawsOne()
    {
        var fight = Fight.WithRelics(RelicEffects.UnceasingTop);
        fight.State.Hand = [Card(SI.Slice)];
        fight.State.Energy = 9;
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play();

        Assert.Equal([SI.Backstab], fight.State.Hand.Select(c => c.DefId));
    }
}

public class VexingPuzzleboxTests
{
    [Fact]
    public void AFreeCardArrivesOnTheFirstTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.VexingPuzzlebox);

        // The opening five, plus the Puzzlebox's one.
        Assert.Equal(6, fight.State.Hand.Count);
        Assert.Contains(fight.State.Hand, c => c.FreeThisTurn);
    }

    [Fact]
    public void AndOnlyOnTheFirstTurn()
    {
        var fight = Fight.WithRelics(RelicEffects.VexingPuzzlebox);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Equal(5, fight.State.Hand.Count);
    }
}

public class TheCourierTests
{
    [Fact]
    public void EverythingInTheShopIsAFifthOff()
    {
        var plain = new RunState();
        Assert.Equal(100, RelicEffects.ModifyMerchantPrice(plain, 100));

        var couriered = new RunState { Relics = [new RelicInstance(RelicEffects.TheCourier)] };
        Assert.Equal(80, RelicEffects.ModifyMerchantPrice(couriered, 100));
        Assert.Equal(60, RelicEffects.ModifyMerchantPrice(couriered, 75));
    }
}

public class PrayerWheelTests
{
    /// <summary>An extra card reward after a MONSTER room, and not after an elite.</summary>
    [Fact]
    public void AnExtraRewardAfterAMonsterRoom()
    {
        var state = new RunState { Relics = [new RelicInstance(RelicEffects.PrayerWheel)] };

        Assert.True(RelicEffects.AddsExtraCardReward(state, RunConstants.NodeNormal));
        Assert.False(RelicEffects.AddsExtraCardReward(state, RunConstants.NodeElite));
        Assert.False(RelicEffects.AddsExtraCardReward(state, RunConstants.NodeBoss));
    }
}

public class WhiteStarTests
{
    /// <summary>An extra card reward after an ELITE room, and not after a monster.</summary>
    [Fact]
    public void AnExtraRewardAfterAnElite()
    {
        var state = new RunState { Relics = [new RelicInstance(RelicEffects.WhiteStar)] };

        Assert.True(RelicEffects.AddsExtraCardReward(state, RunConstants.NodeElite));
        Assert.False(RelicEffects.AddsExtraCardReward(state, RunConstants.NodeNormal));
    }
}
