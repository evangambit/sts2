using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Shared ids for the Necrobinder cards whose emulator arms were already right when they
/// were read against the decompiled source. Each is pinned in its own class below, so the
/// reading is a test rather than a note — a correct card with no test is a card the next
/// refactor is free to break.
/// </summary>
internal static class NB
{
    public const int Strike = 473;
    public const int Defend = 132;
    public const int Reap = 384;
    public const int Fear = 182;
    public const int Scourge = 409;
    public const int Parse = 345;
    public const int Wisp = 542;
    public const int Spur = 458;
    public const int PullAggro = 370;
    public const int Reanimate = 383;
    public const int Reave = 386;
    public const int Severance = 423;
    public const int EndOfDays = 162;
    public const int NegativePulse = 320;
    public const int HighFive = 251;
    public const int Squeeze = 460;
    public const int GlimpseBeyond = 221;
    public const int Poke = 357;
    public const int Soul = 446;

    public static Fight One(int defId, bool upgraded = false, int hp = 500) =>
        Fight.Hand(new CardInstance(defId, upgraded)).Energy(9).Enemy(hp: hp);
}

// Strike: DamageVar(6) upgrading by 3, one target.
public class StrikeNecrobinderTests
{
    [Fact]
    public void ItHitsForSixAndNineUpgraded()
    {
        var fight = NB.One(NB.Strike);
        fight.Play();
        Assert.Equal(494, fight.Enemy0.Hp);

        var up = NB.One(NB.Strike, upgraded: true);
        up.Play();
        Assert.Equal(491, up.Enemy0.Hp);
    }
}

// Defend: BlockVar(5) upgrading by 3, and nothing else — it had shared Undeath's case.
public class DefendNecrobinderTests
{
    [Fact]
    public void ItGainsFiveBlockAndEightUpgraded()
    {
        var fight = NB.One(NB.Defend);
        fight.Play();
        Assert.Equal(5, fight.State.PlayerBlock);

        var up = NB.One(NB.Defend, upgraded: true);
        up.Play();
        Assert.Equal(8, up.State.PlayerBlock);
    }
}

// Reap: DamageVar(27) upgrading by 6, one target, Retain.
public class ReapTests
{
    [Fact]
    public void ItHitsForTwentySevenAndIsRetained()
    {
        var fight = NB.One(NB.Reap);
        fight.Play();

        Assert.Equal(473, fight.Enemy0.Hp);
        Assert.True(new CardInstance(NB.Reap, false).IsRetained());
    }

    [Fact]
    public void ItHitsOnlyTheTarget()
    {
        var fight = Fight.Hand(new CardInstance(NB.Reap, false))
            .Energy(9)
            .Enemy(hp: 500)
            .Enemy(hp: 500);

        fight.Play();

        Assert.Equal(500, fight.Enemy1.Hp);
    }
}

// Fear: DamageVar(7) upgrading by 1, Vulnerable 1 upgrading by 1, on the target. Ethereal.
public class FearTests
{
    [Fact]
    public void ItHitsAndMakesTheTargetVulnerable()
    {
        var fight = Fight.Hand(new CardInstance(NB.Fear, false))
            .Energy(9)
            .Enemy(hp: 500)
            .Enemy(hp: 500);

        fight.Play();

        Assert.Equal(493, fight.Enemy0.Hp);
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Vulnerable));
        Assert.True(new CardInstance(NB.Fear, false).IsEthereal());
    }

    [Fact]
    public void UpgradedItIsEightAndTwo()
    {
        var fight = NB.One(NB.Fear, upgraded: true);

        fight.Play();

        Assert.Equal(492, fight.Enemy0.Hp);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }
}

// Scourge: Doom 13 upgrading by 3 on the target, then draw 1 upgrading by 1.
public class ScourgeTests
{
    [Fact]
    public void ItDoomsTheTargetAndDraws()
    {
        var fight = Fight.Hand(new CardInstance(NB.Scourge, false))
            .Energy(9)
            .Enemy(hp: 500)
            .Enemy(hp: 500);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));
        fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));

        fight.Play();

        Assert.Equal(13, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
        Assert.Single(fight.State.Hand);
    }

    [Fact]
    public void UpgradedItIsSixteenAndTwoCards()
    {
        var fight = NB.One(NB.Scourge, upgraded: true);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));
        fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));

        fight.Play();

        Assert.Equal(16, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
        Assert.Equal(2, fight.State.Hand.Count);
    }
}

// Parse: draw 3 upgrading by 1. Ethereal.
public class ParseTests
{
    [Fact]
    public void ItDrawsThreeAndIsEthereal()
    {
        var fight = NB.One(NB.Parse);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));
        }

        fight.Play();

        Assert.Equal(3, fight.State.Hand.Count);
        Assert.True(new CardInstance(NB.Parse, false).IsEthereal());
    }

    [Fact]
    public void UpgradedItDrawsFour()
    {
        var fight = NB.One(NB.Parse, upgraded: true);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(NB.Strike, false));
        }

        fight.Play();

        Assert.Equal(4, fight.State.Hand.Count);
    }
}

// Wisp: gain 1 energy; upgrading buys RETAIN, not a second energy. Exhaust.
public class WispTests
{
    [Fact]
    public void ItGainsOneEnergyEvenUpgraded()
    {
        var fight = NB.One(NB.Wisp);
        fight.State.Energy = 0;
        fight.Play();
        Assert.Equal(1, fight.State.Energy);

        var up = NB.One(NB.Wisp, upgraded: true);
        up.State.Energy = 0;
        up.Play();
        Assert.Equal(1, up.State.Energy);
        Assert.True(new CardInstance(NB.Wisp, true).IsRetained());
    }
}

// Spur: summon 3 upgrading by 2, then HEAL Osty 5 upgrading by 2. Retain.
public class SpurTests
{
    [Fact]
    public void ItSummonsThenHeals()
    {
        var fight = NB.One(NB.Spur);
        CardEffects.SummonOsty(fight.State, 10);
        fight.State.OstyHp = 2;

        fight.Play();

        // Summon on a living pet grows it by the amount -- 13 max, 5 current -- and the
        // heal then takes it to 10.
        Assert.Equal(13, fight.State.OstyMaxHp);
        Assert.Equal(10, fight.State.OstyHp);
    }

    [Fact]
    public void OnNoOstyItSummonsThree()
    {
        var fight = NB.One(NB.Spur);

        fight.Play();

        Assert.Equal(3, fight.State.OstyMaxHp);
    }
}

// PullAggro: summon 4 upgrading by 1, then block 7 upgrading by 2.
public class PullAggroTests
{
    [Fact]
    public void ItSummonsAndBlocks()
    {
        var fight = NB.One(NB.PullAggro);
        fight.Play();
        Assert.Equal(4, fight.State.OstyHp);
        Assert.Equal(7, fight.State.PlayerBlock);

        var up = NB.One(NB.PullAggro, upgraded: true);
        up.Play();
        Assert.Equal(5, up.State.OstyHp);
        Assert.Equal(9, up.State.PlayerBlock);
    }
}

// Reanimate: summon 20 upgrading by 5. Exhaust, and nothing else.
public class ReanimateTests
{
    [Fact]
    public void ItSummonsTwenty()
    {
        var fight = NB.One(NB.Reanimate);
        fight.State.Hand.RemoveAll(c => c.DefId != NB.Reanimate);

        fight.Play();

        Assert.Equal(20, fight.State.OstyHp);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Empty(fight.State.Hand);
    }
}

// Reave: 9 damage upgrading by 2, then one Soul into the DRAW pile — upgraded if Reave was.
// The Soul is written INSIDE a `CardCmd.PreviewCardPileAdd(...)` call, which `card_pair.py`
// used to drop entirely; see GraveWardenTests for the card that cost.
public class ReaveTests
{
    [Fact]
    public void ItHitsAndMakesASoul()
    {
        var fight = NB.One(NB.Reave);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(NB.Soul, Assert.Single(fight.State.DrawPile).DefId);
    }
}

// Severance: 13 damage upgrading by 5, then a Soul each to draw, discard and hand.
public class SeveranceTests
{
    [Fact]
    public void ItSpreadsThreeSouls()
    {
        var fight = NB.One(NB.Severance);
        fight.State.DrawPile.Clear();
        fight.State.Hand.RemoveAll(c => c.DefId != NB.Severance);

        fight.Play();

        Assert.Equal(487, fight.Enemy0.Hp);
        Assert.Equal(NB.Soul, fight.State.DrawPile.Single().DefId);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == NB.Soul);
        Assert.Equal(NB.Soul, fight.State.Hand.Single().DefId);
    }
}

// End of Days: Doom 29 upgrading by 8 to every hittable enemy, then DoomKill.
public class EndOfDaysTests
{
    [Fact]
    public void ItDoomsEveryoneAndKillsTheDoomed()
    {
        var fight = Fight.Hand(new CardInstance(NB.EndOfDays, false))
            .Energy(9)
            .Enemy(hp: 20)
            .Enemy(hp: 500);

        fight.Play();

        Assert.Equal(0, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
        Assert.Equal(29, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
    }
}

// Negative Pulse: block 5 upgrading by 1, Doom 7 upgrading by 4 to every hittable enemy.
public class NegativePulseTests
{
    [Fact]
    public void ItBlocksAndDoomsEveryone()
    {
        var fight = Fight.Hand(new CardInstance(NB.NegativePulse, false))
            .Energy(9)
            .Enemy(hp: 500)
            .Enemy(hp: 500);

        fight.Play();

        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(7, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
        Assert.Equal(7, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Doom));
    }

    [Fact]
    public void UpgradedItIsSixAndEleven()
    {
        var fight = NB.One(NB.NegativePulse, upgraded: true);

        fight.Play();

        Assert.Equal(6, fight.State.PlayerBlock);
        Assert.Equal(11, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Doom));
    }
}

// High Five: OstyDamage 11 upgrading by 2 at ALL opponents, then Vulnerable 2 upgrading by
// 1 on all of them — and nothing at all without a living Osty.
public class HighFiveTests
{
    [Fact]
    public void ItHitsEveryoneAndMakesThemVulnerable()
    {
        var fight = Fight.Hand(new CardInstance(NB.HighFive, false))
            .Energy(9)
            .Enemy(hp: 500)
            .Enemy(hp: 500);
        CardEffects.SummonOsty(fight.State, 10);

        fight.Play();

        Assert.Equal(489, fight.Enemy0.Hp);
        Assert.Equal(489, fight.Enemy1.Hp);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
        Assert.Equal(2, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void WithNoOstyItDoesNothing()
    {
        var fight = NB.One(NB.HighFive);

        fight.Play();

        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.Equal(0, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }
}

// Squeeze: CalculationBase 25 (upgrading by 5) plus ExtraDamage 5 (upgrading by 1) per
// OTHER card tagged OstyAttack among the player's cards, inside the missing-Osty guard.
public class SqueezeTests
{
    [Fact]
    public void ItScalesWithTheOtherOstyAttacks()
    {
        var fight = NB.One(NB.Squeeze);
        CardEffects.SummonOsty(fight.State, 10);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(NB.Poke, false));
        fight.State.DrawPile.Add(new CardInstance(NB.Poke, false));

        fight.Play();

        Assert.Equal(465, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithNoOstyItDealsNothing()
    {
        var fight = NB.One(NB.Squeeze);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Equal(500, fight.Enemy0.Hp);
    }
}

// Glimpse Beyond: 3 Souls upgrading by 1 into the DRAW pile, one set per living player
// teammate — which in a solo run is one. Exhaust, and MultiplayerOnly.
public class GlimpseBeyondTests
{
    [Fact]
    public void ItMakesThreeSouls()
    {
        var fight = NB.One(NB.GlimpseBeyond);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Equal(3, fight.State.DrawPile.Count);
        Assert.All(fight.State.DrawPile, c => Assert.Equal(NB.Soul, c.DefId));
        Assert.Single(fight.State.ExhaustPile);
    }

    [Fact]
    public void UpgradedItMakesFour()
    {
        var fight = NB.One(NB.GlimpseBeyond, upgraded: true);
        fight.State.DrawPile.Clear();

        fight.Play();

        Assert.Equal(4, fight.State.DrawPile.Count);
    }
}
