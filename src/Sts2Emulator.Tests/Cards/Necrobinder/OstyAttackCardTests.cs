using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Shared setup for the Necrobinder's OstyAttack cards, all of which put their whole body
/// inside <c>if (!Osty.CheckMissingWithAnim(Owner))</c> — with no pet they do NOTHING,
/// riders included. Four of them had let part of the body escape that guard.
/// </summary>
internal static class OstyFight
{
    public const int Fetch = 187;
    public const int Flatten = 198;
    public const int Poke = 357;
    public const int Rattle = 382;
    public const int Protector = 368;
    public const int Unleash = 524;
    public const int Sacrifice = 405;
    public const int StrikeNecrobinder = 473;

    public static Fight WithOsty(
        int defId,
        bool upgraded = false,
        int ostyHp = 10,
        int? maxHp = null
    )
    {
        var fight = Fight.Hand(new CardInstance(defId, upgraded)).Energy(9).Enemy(hp: 500);
        CardEffects.SummonOsty(fight.State, maxHp ?? ostyHp);
        fight.State.OstyHp = ostyHp;
        return fight;
    }

    public static Fight WithoutOsty(int defId) =>
        Fight.Hand(new CardInstance(defId, false)).Energy(9).Enemy(hp: 500);
}

// MegaCrit.Sts2.Core.Models.Cards/Poke.cs: OstyDamageVar(6) upgrading by 3, one target.
public class PokeTests
{
    [Fact]
    public void ItHitsForSixAndNineUpgraded()
    {
        var fight = OstyFight.WithOsty(OstyFight.Poke);
        fight.Play();
        Assert.Equal(494, fight.Enemy0.Hp);

        var up = OstyFight.WithOsty(OstyFight.Poke, upgraded: true);
        up.Play();
        Assert.Equal(491, up.Enemy0.Hp);
    }

    [Fact]
    public void WithNoOstyItDoesNothing()
    {
        var fight = OstyFight.WithoutOsty(OstyFight.Poke);

        fight.Play();

        Assert.Equal(500, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Fetch.cs: OstyDamage 3 upgrading by 3, then one card —
// but only if THIS copy has not already been played this turn (`HasBeenPlayedThisTurn`, a
// history query about the card itself). The emulator drew every time, which on a
// nought-cost card is an unbounded draw engine.
public class FetchTests
{
    [Fact]
    public void ItDrawsOnItsFirstPlayOfTheTurn()
    {
        var fight = OstyFight.WithOsty(OstyFight.Fetch);
        fight.State.DrawPile.Add(new CardInstance(OstyFight.StrikeNecrobinder, false));

        fight.Play();

        Assert.Equal(497, fight.Enemy0.Hp);
        Assert.Single(fight.State.Hand);
    }

    /// <summary>
    /// The same copy played again is damage only — it comes back out of the discard with
    /// its `PlayedThisTurn` intact.
    /// </summary>
    [Fact]
    public void TheSameCopyPlayedAgainDrawsNothing()
    {
        var fight = OstyFight.WithOsty(OstyFight.Fetch);
        fight.State.DrawPile.Add(new CardInstance(OstyFight.StrikeNecrobinder, false));
        fight.State.DrawPile.Add(new CardInstance(OstyFight.StrikeNecrobinder, false));
        fight.Play();
        fight.State.Hand.Clear();

        // Straight out of the discard, the way Graveblast would put it there.
        var recovered = fight.State.DiscardPile[^1];
        fight.State.DiscardPile.RemoveAt(fight.State.DiscardPile.Count - 1);
        fight.State.Hand.Add(recovered);

        fight.Play();

        Assert.Empty(fight.State.Hand);
    }

    /// <summary>A SECOND Fetch is a different copy, so it still draws.</summary>
    [Fact]
    public void ASecondFetchStillDraws()
    {
        var fight = OstyFight.WithOsty(OstyFight.Fetch);
        fight.State.Hand.Add(new CardInstance(OstyFight.Fetch, false));
        for (int i = 0; i < 4; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(OstyFight.StrikeNecrobinder, false));
        }

        fight.Play();
        fight.Play();

        // Two Fetches out of hand and two cards drawn back into it. One draw would leave
        // one card, which is what a per-CARD rather than per-COPY reading would give.
        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void ItStartsDrawingAgainNextTurn()
    {
        var fight = OstyFight.WithOsty(OstyFight.Fetch);
        fight.State.DrawPile.Add(new CardInstance(OstyFight.StrikeNecrobinder, false));
        fight.Play();

        fight.EndTurn();

        Assert.All(fight.State.DiscardPile, c => Assert.False(c.PlayedThisTurn));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Flatten.cs: OstyDamage 12 upgrading by 4, and
// `EnergyCost.SetThisTurn(0)` once Osty has attacked this turn — from `AfterAttack`, and
// again from `AfterCardEnteredCombat` for a copy that arrives after the swing.
public class FlattenTests
{
    [Fact]
    public void ItCostsTwoUntilTheOstyHasSwung()
    {
        var fight = OstyFight.WithOsty(OstyFight.Flatten);

        Assert.Equal(
            2,
            CombatEngine.EffectiveCost(new CardInstance(OstyFight.Flatten, false), fight.State)
        );

        fight.State.Hand.Insert(0, new CardInstance(OstyFight.Poke, false));
        fight.Play();

        Assert.Equal(
            0,
            CombatEngine.EffectiveCost(new CardInstance(OstyFight.Flatten, false), fight.State)
        );
    }

    [Fact]
    public void ItIsDearAgainNextTurn()
    {
        var fight = OstyFight.WithOsty(OstyFight.Poke);
        fight.Play();
        fight.EndTurn();

        Assert.Equal(
            2,
            CombatEngine.EffectiveCost(new CardInstance(OstyFight.Flatten, false), fight.State)
        );
    }

    [Fact]
    public void ItHitsForTwelveAndSixteenUpgraded()
    {
        var fight = OstyFight.WithOsty(OstyFight.Flatten);
        fight.Play();
        Assert.Equal(488, fight.Enemy0.Hp);

        var up = OstyFight.WithOsty(OstyFight.Flatten, upgraded: true);
        up.Play();
        Assert.Equal(484, up.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Rattle.cs: OstyDamage 7 upgrading by 2, hit
// `1 + Osty's attacks this turn` times. The emulator hit once, always.
public class RattleTests
{
    [Fact]
    public void ItHitsOnceOnItsOwn()
    {
        var fight = OstyFight.WithOsty(OstyFight.Rattle);

        fight.Play();

        Assert.Equal(493, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItHitsTwiceAfterAnEarlierOstySwing()
    {
        var fight = OstyFight.WithOsty(OstyFight.Rattle);
        fight.State.Hand.Insert(0, new CardInstance(OstyFight.Poke, false));

        fight.Play();
        fight.Play();

        Assert.Equal(500 - 6 - 14, fight.Enemy0.Hp);
    }

    /// <summary>
    /// One `WithHitCount` attack is one entry, so two Rattles are 1 then 2 hits — not 1
    /// then 2 then 4, which counting each hit would give.
    /// </summary>
    [Fact]
    public void TwoRattlesAreOneHitThenTwo()
    {
        var fight = OstyFight.WithOsty(OstyFight.Rattle);
        fight.State.Hand.Add(new CardInstance(OstyFight.Rattle, false));

        fight.Play();
        fight.Play();

        Assert.Equal(500 - 7 - 14, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Protector.cs: CalculationBase 10 (upgrading by 5) plus 1
// per point of Osty's MAX hp, inside the missing-Osty guard — which the emulator's arm was
// outside, so the base paid out with no pet.
public class ProtectorTests
{
    [Fact]
    public void ItScalesWithOstyMaxHp()
    {
        var fight = OstyFight.WithOsty(OstyFight.Protector, ostyHp: 4, maxHp: 12);

        fight.Play();

        Assert.Equal(478, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithNoOstyItDealsNothing()
    {
        var fight = OstyFight.WithoutOsty(OstyFight.Protector);
        CardEffects.SummonOsty(fight.State, 12);
        fight.State.OstyHp = 0;

        fight.Play();

        Assert.Equal(500, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Unleash.cs: CalculationBase 6 (upgrading by 3) plus
// Osty's CURRENT hp.
public class UnleashTests
{
    [Fact]
    public void ItScalesWithOstyCurrentHp()
    {
        var fight = OstyFight.WithOsty(OstyFight.Unleash, ostyHp: 4, maxHp: 12);

        fight.Play();

        Assert.Equal(490, fight.Enemy0.Hp);
    }

    [Fact]
    public void WithNoOstyItDealsNothing()
    {
        var fight = OstyFight.WithoutOsty(OstyFight.Unleash);

        fight.Play();

        Assert.Equal(500, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Sacrifice.cs: block is Osty's MaxHp × 2, then the pet
// dies — and the whole body is inside the missing-Osty guard. The emulator tested
// `OstyMaxHp`, which outlives the pet.
public class SacrificeTests
{
    [Fact]
    public void ItTradesTheOstyForTwiceItsMaxHp()
    {
        var fight = OstyFight.WithOsty(OstyFight.Sacrifice, ostyHp: 3, maxHp: 12);

        fight.Play();

        Assert.Equal(24, fight.State.PlayerBlock);
        Assert.Equal(0, fight.State.OstyHp);
    }

    [Fact]
    public void WithADeadOstyItGainsNoBlock()
    {
        var fight = OstyFight.WithoutOsty(OstyFight.Sacrifice);
        CardEffects.SummonOsty(fight.State, 12);
        fight.State.OstyHp = 0;

        fight.Play();

        Assert.Equal(0, fight.State.PlayerBlock);
    }
}
