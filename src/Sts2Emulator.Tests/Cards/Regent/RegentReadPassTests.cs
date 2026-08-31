using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>Ids for the Regent cards read against the source in the reading pass.</summary>
internal static class RG
{
    public const int Bombardment = 52;
    public const int BundleOfJoy = 68;
    public const int CrashLanding = 110;
    public const int MeteorShower = 304;
    public const int HeirloomHammer = 243;
    public const int KinglyKick = 274;
    public const int KinglyPunch = 275;
    public const int MakeItSo = 293;
    public const int ManifestAuthority = 296;
    public const int Radiate = 377;
    public const int SevenStars = 422;
    public const int ShiningStrike = 429;
    public const int SolarStrike = 445;
    public const int GatherLight = 214;
    public const int Comet = 96;
    public const int GammaBlast = 212;
    public const int Strike = 474;
    public const int Defend = 133;
    public const int Debris = 128;

    public static Fight Fresh(int stars = 9) => RegentBoard.Fresh(stars);
}

// MegaCrit.Sts2.Core.Models.Cards/Bombardment.cs: 18 damage upgrading by 6, Exhaust — and
// `AfterAutoPrePlayPhaseEnteredEarly` auto-plays it whenever it is sitting in the owner's
// EXHAUST pile as the play phase opens. So exhausting it is the POINT: play it once and it
// fires free every turn afterwards. The emulator had a plain attack.
public class BombardmentTests
{
    [Fact]
    public void ItHitsForEighteenAndTwentyFourUpgraded()
    {
        var fight = RG.Fresh().PlayCard(RG.Bombardment);
        Assert.Equal(482, fight.Enemy0.Hp);

        var up = RG.Fresh().PlayCard(RG.Bombardment, upgraded: true);
        Assert.Equal(476, up.Enemy0.Hp);
    }

    [Fact]
    public void ItExhaustsItself()
    {
        var fight = RG.Fresh().PlayCard(RG.Bombardment);

        Assert.Equal(RG.Bombardment, Assert.Single(fight.State.ExhaustPile).DefId);
    }

    [Fact]
    public void FromTheExhaustPileItFiresAgainEveryTurn()
    {
        var fight = RG.Fresh().PlayCard(RG.Bombardment);

        fight.EndTurn();
        Assert.Equal(464, fight.Enemy0.Hp);

        fight.EndTurn();
        Assert.Equal(446, fight.Enemy0.Hp);
    }

    /// <summary>It stays in the exhaust pile — the replay does not consume it.</summary>
    [Fact]
    public void TheCopyStaysExhausted()
    {
        var fight = RG.Fresh().PlayCard(RG.Bombardment);
        fight.EndTurn();

        Assert.Equal(RG.Bombardment, Assert.Single(fight.State.ExhaustPile).DefId);
    }

    /// <summary>Sitting in hand it does nothing on its own — the hook reads the EXHAUST pile.</summary>
    [Fact]
    public void InHandItDoesNothingAtTurnStart()
    {
        var fight = RG.Fresh();
        fight.State.Hand.Add(new CardInstance(RG.Bombardment, false));

        fight.EndTurn();

        Assert.Equal(500, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/BundleOfJoy.cs: `CardsVar(3)` upgrading by 1 DISTINCT
// cards from the COLOURLESS pool into hand, rolled on CombatCardGeneration.
//
// The emulator added ONE card from the character's own class pool — and passed `upgraded`
// into the helper's `freeThisTurn` parameter, so an upgraded Bundle of Joy made its single
// wrong card free as well.
public class BundleOfJoyTests
{
    private static bool IsColorless(int defId) =>
        GeneratedData.CardPools.Colorless.IndexOf(defId) >= 0;

    [Fact]
    public void ItAddsThreeColorlessCards()
    {
        var fight = RG.Fresh().PlayCard(RG.BundleOfJoy);

        Assert.Equal(3, fight.State.Hand.Count);
        Assert.All(fight.State.Hand, c => Assert.True(IsColorless(c.DefId)));
    }

    [Fact]
    public void UpgradedItAddsFour()
    {
        var fight = RG.Fresh().PlayCard(RG.BundleOfJoy, upgraded: true);

        Assert.Equal(4, fight.State.Hand.Count);
    }

    /// <summary>DISTINCT — `GetDistinctForCombat` shuffles and takes, so no repeats.</summary>
    [Fact]
    public void TheCardsAreDistinct()
    {
        var fight = RG.Fresh().PlayCard(RG.BundleOfJoy);

        Assert.Equal(3, fight.State.Hand.Select(c => c.DefId).Distinct().Count());
    }

    /// <summary>They are ordinary cards: not free, and not upgraded.</summary>
    [Fact]
    public void TheyAreNeitherFreeNorUpgraded()
    {
        var fight = RG.Fresh().PlayCard(RG.BundleOfJoy, upgraded: true);

        Assert.All(
            fight.State.Hand,
            c =>
            {
                Assert.False(c.FreeThisTurn);
                Assert.False(c.Upgraded);
            }
        );
    }

    [Fact]
    public void ItExhausts()
    {
        var fight = RG.Fresh().PlayCard(RG.BundleOfJoy);

        Assert.Equal(RG.BundleOfJoy, Assert.Single(fight.State.ExhaustPile).DefId);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/CrashLanding.cs: 21 damage upgrading by 5 at ALL
// opponents, then DEBRIS to FILL THE HAND — `MaxCardsInHand - hand.Count` of them.
//
// The emulator hit one enemy and added two random REGENT cards. The drawback is the whole
// cost of a one-energy 21-to-everyone.
public class CrashLandingTests
{
    [Fact]
    public void ItHitsEveryEnemy()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RG.CrashLanding);

        Assert.Equal(479, fight.Enemy0.Hp);
        Assert.Equal(479, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradedItHitsForTwentySix()
    {
        var fight = RG.Fresh().PlayCard(RG.CrashLanding, upgraded: true);

        Assert.Equal(474, fight.Enemy0.Hp);
    }

    [Fact]
    public void ItFillsTheHandWithDebris()
    {
        var fight = RG.Fresh().PlayCard(RG.CrashLanding);

        Assert.Equal(CardEffects.MaxCardsInHand, fight.State.Hand.Count);
        Assert.All(fight.State.Hand, c => Assert.Equal(RG.Debris, c.DefId));
    }

    /// <summary>Room is counted after the attack, so a fuller hand gets less Debris.</summary>
    [Fact]
    public void AFullerHandGetsLessDebris()
    {
        var fight = RG.Fresh();
        for (int i = 0; i < 6; i++)
        {
            fight.State.Hand.Add(new CardInstance(RG.Strike, false));
        }

        fight.PlayCard(RG.CrashLanding);

        Assert.Equal(4, fight.State.Hand.Count(c => c.DefId == RG.Debris));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/MeteorShower.cs: `TargetingAllOpponents` for 14 upgrading
// by 7, and Weak 2 and Vulnerable 2 to `HittableEnemies`. It shared Comet's and Gamma
// Blast's single-target body, which is right for those two and not for this one.
public class MeteorShowerTests
{
    private static Fight Two() =>
        RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

    [Fact]
    public void ItHitsAndDebuffsEveryEnemy()
    {
        var fight = Two().PlayCard(RG.MeteorShower);

        Assert.Equal(486, fight.Enemy0.Hp);
        Assert.Equal(486, fight.Enemy1.Hp);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
        Assert.Equal(2, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradedItHitsForTwentyOne()
    {
        var fight = Two().PlayCard(RG.MeteorShower, upgraded: true);

        Assert.Equal(479, fight.Enemy0.Hp);
        Assert.Equal(479, fight.Enemy1.Hp);
    }
}

// Comet and Gamma Blast keep the single-target body Meteor Shower was stacked onto, which
// is right for both of them: five stars for 33/44 with Weak 3 and Vulnerable 3, and three
// stars for 13/18 with Weak 2 and Vulnerable 2, each on `cardPlay.Target` alone.
public class CometTests
{
    [Fact]
    public void ItHitsAndDebuffsTheTargetOnly()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RG.Comet);

        Assert.Equal(467, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
        Assert.Equal(3, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
        Assert.Equal(3, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
    }

    [Fact]
    public void UpgradingBuysDamageNotDebuff()
    {
        var fight = RG.Fresh().PlayCard(RG.Comet, upgraded: true);

        Assert.Equal(456, fight.Enemy0.Hp);
        Assert.Equal(3, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }
}

public class GammaBlastTests
{
    [Fact]
    public void ItHitsAndDebuffsTheTargetOnly()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RG.GammaBlast);

        Assert.Equal(487, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void UpgradingBuysDamageNotDebuff()
    {
        var fight = RG.Fresh().PlayCard(RG.GammaBlast, upgraded: true);

        Assert.Equal(482, fight.Enemy0.Hp);
        Assert.Equal(2, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
    }
}

// MegaCrit.Sts2.Core.Models.Cards/KinglyKick.cs: 27 damage upgrading by 8, and an
// `AfterCardDrawn` on ITSELF that is `EnergyCost.AddThisCombat(-1)` — it gets a point
// cheaper every time this COPY is drawn, for the rest of the combat. The emulator had a
// plain four-cost attack.
public class KinglyKickTests
{
    private static Fight WithItInTheDrawPile()
    {
        var fight = RG.Fresh();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(RG.KinglyKick, false));
        return fight;
    }

    [Fact]
    public void UndrawnItCostsFour()
    {
        var fight = RG.Fresh();

        Assert.Equal(
            4,
            CombatEngine.EffectiveCost(new CardInstance(RG.KinglyKick, false), fight.State)
        );
    }

    [Fact]
    public void EachDrawTakesOneOffThisCopy()
    {
        var fight = WithItInTheDrawPile();

        CardEffects.DrawCards(fight.State, 1, new System.Random(0));
        Assert.Equal(3, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));

        // Back to the draw pile and drawn again.
        var once = fight.State.Hand[0];
        fight.State.Hand.Clear();
        fight.State.DrawPile.Add(once);
        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(2, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));
    }

    /// <summary>Per COPY: a second Kingly Kick is still full price.</summary>
    [Fact]
    public void ASecondCopyIsStillFullPrice()
    {
        var fight = WithItInTheDrawPile();
        CardEffects.DrawCards(fight.State, 1, new System.Random(0));

        Assert.Equal(
            4,
            CombatEngine.EffectiveCost(new CardInstance(RG.KinglyKick, false), fight.State)
        );
    }

    [Fact]
    public void ItHitsForTwentySevenAndThirtyFiveUpgraded()
    {
        Assert.Equal(473, RG.Fresh().PlayCard(RG.KinglyKick).Enemy0.Hp);
        Assert.Equal(465, RG.Fresh().PlayCard(RG.KinglyKick, upgraded: true).Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/KinglyPunch.cs: 8 damage upgrading by 2, and an
// `AfterCardDrawn` on ITSELF that adds `IntVar("Increase", 4)` — 6 upgraded — to this
// COPY's damage, for good.
//
// The emulator scaled it by `CardsPlayedThisCombat`, a player-wide counter standing in for
// a per-copy one.
public class KinglyPunchTests
{
    private static Fight Drawn(int times, bool upgraded = false)
    {
        var fight = RG.Fresh();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(RG.KinglyPunch, upgraded));
        for (int i = 0; i < times; i++)
        {
            CardEffects.DrawCards(fight.State, 1, new System.Random(0));
            if (i < times - 1)
            {
                fight.State.DrawPile.Add(fight.State.Hand[0]);
                fight.State.Hand.Clear();
            }
        }

        return fight;
    }

    [Fact]
    public void DrawnOnceItHitsForTwelve()
    {
        var fight = Drawn(1);

        fight.Play(0, target: 0);

        Assert.Equal(488, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachDrawAddsFourMore()
    {
        var fight = Drawn(3);

        fight.Play(0, target: 0);

        Assert.Equal(480, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedEachDrawAddsSix()
    {
        var fight = Drawn(2, upgraded: true);

        fight.Play(0, target: 0);

        Assert.Equal(478, fight.Enemy0.Hp);
    }

    /// <summary>Cards played this combat are not the number — that was the old reading.</summary>
    [Fact]
    public void CardsPlayedDoNotFeedIt()
    {
        var fight = Drawn(1);
        fight.State.CardsPlayedThisCombat = 7;

        fight.Play(0, target: 0);

        Assert.Equal(488, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Radiate.cs: 3 damage upgrading by 1, at ALL opponents,
// hit once per STAR GAINED this turn — the sum of the positive `StarsModifiedEntry`
// amounts. Not the stars held: spending them does not reduce it and starting the turn with
// a pile does not raise it, and with no gains it deals NOTHING.
//
// The emulator hit one enemy `Math.Max(1, state.Stars)` times.
public class RadiateTests
{
    private static Fight Two(int stars = 9) =>
        RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), stars);

    [Fact]
    public void WithNoGainsThisTurnItDealsNothing()
    {
        var fight = Two().PlayCard(RG.Radiate);

        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
    }

    [Fact]
    public void ItHitsEveryEnemyOncePerStarGained()
    {
        var fight = Two(stars: 0);
        CardEffects.GainStars(fight.State, 4);

        fight.PlayCard(RG.Radiate);

        Assert.Equal(488, fight.Enemy0.Hp);
        Assert.Equal(488, fight.Enemy1.Hp);
    }

    /// <summary>Spending the stars does not take the hits away.</summary>
    [Fact]
    public void SpendingTheStarsKeepsTheHits()
    {
        var fight = Two(stars: 0);
        CardEffects.GainStars(fight.State, 4);
        fight.State.Stars = 0;

        fight.PlayCard(RG.Radiate);

        Assert.Equal(488, fight.Enemy0.Hp);
    }

    [Fact]
    public void UpgradedEachHitIsFour()
    {
        var fight = Two(stars: 0);
        CardEffects.GainStars(fight.State, 2);

        fight.PlayCard(RG.Radiate, upgraded: true);

        Assert.Equal(492, fight.Enemy0.Hp);
    }

    /// <summary>The counter is per TURN.</summary>
    [Fact]
    public void ItResetsEachTurn()
    {
        var fight = Two(stars: 0);
        CardEffects.GainStars(fight.State, 4);

        fight.EndTurn();

        Assert.Equal(0, fight.State.StarsGainedThisTurn);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SevenStars.cs: seven damage, `WithHitCount(7)`, at ALL
// opponents. Seven stars to play, and the upgrade buys a point of ENERGY. The emulator put
// all seven hits on one enemy.
public class SevenStarsTests
{
    [Fact]
    public void ItHitsEveryEnemySevenTimes()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RG.SevenStars);

        Assert.Equal(451, fight.Enemy0.Hp);
        Assert.Equal(451, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradingBuysCostNotDamage()
    {
        var fight = RG.Fresh();

        Assert.Equal(
            2,
            CombatEngine.EffectiveCost(new CardInstance(RG.SevenStars, false), fight.State)
        );
        Assert.Equal(
            1,
            CombatEngine.EffectiveCost(new CardInstance(RG.SevenStars, true), fight.State)
        );

        fight.PlayCard(RG.SevenStars, upgraded: true);
        Assert.Equal(451, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/HeirloomHammer.cs: 20 damage upgrading by 5, then a
// COLOURLESS card CHOSEN from hand is CLONED into hand — `CardSelectCmd.FromHand` filtered
// to `c.VisualCardPool.IsColorless`, `RepeatVar(1)` copies, and the upgrade buys damage
// rather than a second copy. The original stays where it is.
//
// The emulator added one or two RANDOM REGENT cards: it neither asked nor copied.
public class HeirloomHammerTests
{
    // Two colourless cards that exist in the pool, for the candidate filter to keep.
    private static int FirstColorless => GeneratedData.CardPools.Colorless[0];
    private static int SecondColorless => GeneratedData.CardPools.Colorless[1];

    private static Fight WithHand()
    {
        var fight = RG.Fresh();
        fight.State.Hand.Add(new CardInstance(RG.Strike, false));
        fight.State.Hand.Add(new CardInstance(FirstColorless, false));
        fight.State.Hand.Add(new CardInstance(SecondColorless, false));
        return fight;
    }

    [Fact]
    public void ItHitsForTwentyAndTwentyFiveUpgraded()
    {
        Assert.Equal(480, RG.Fresh().PlayCard(RG.HeirloomHammer).Enemy0.Hp);
        Assert.Equal(475, RG.Fresh().PlayCard(RG.HeirloomHammer, upgraded: true).Enemy0.Hp);
    }

    [Fact]
    public void ItOffersOnlyTheColorlessCards()
    {
        var fight = WithHand().PlayCard(RG.HeirloomHammer);

        Assert.NotNull(fight.Pending);
        Assert.Equal(CardSelectionKind.CloneColorlessInHand, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    [Fact]
    public void ThePickIsClonedAndTheOriginalStays()
    {
        var fight = WithHand().PlayCard(RG.HeirloomHammer);
        int before = fight.State.Hand.Count;

        fight.Choose(1);

        Assert.Equal(before + 1, fight.State.Hand.Count);
        Assert.Equal(2, fight.State.Hand.Count(c => c.DefId == SecondColorless));
        Assert.Null(fight.Pending);
    }

    /// <summary>One copy, upgraded or not — the upgrade buys damage.</summary>
    [Fact]
    public void UpgradingDoesNotMakeASecondCopy()
    {
        var fight = WithHand().PlayCard(RG.HeirloomHammer, upgraded: true);
        fight.Choose(0);

        Assert.Equal(2, fight.State.Hand.Count(c => c.DefId == FirstColorless));
    }

    /// <summary>The clone is of the CARD — upgrade included, turn-local state not.</summary>
    [Fact]
    public void TheCloneMatchesTheCardButNotItsTurnState()
    {
        var fight = RG.Fresh();
        fight.State.Hand.Add(new CardInstance(FirstColorless, true) { FreeThisTurn = true });
        fight.PlayCard(RG.HeirloomHammer);

        fight.Choose(0);

        var clone = fight.State.Hand.Last(c => c.DefId == FirstColorless);
        Assert.True(clone.Upgraded);
        Assert.False(clone.FreeThisTurn);
    }

    /// <summary>No colourless card in hand is no screen at all.</summary>
    [Fact]
    public void WithNoColorlessCardItAsksNothing()
    {
        var fight = RG.Fresh();
        fight.State.Hand.Add(new CardInstance(RG.Strike, false));

        fight.PlayCard(RG.HeirloomHammer);

        Assert.Null(fight.Pending);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/MakeItSo.cs: 6 damage upgrading by 3, and an
// `AfterCardPlayedLate` that returns it to HAND on every third SKILL the owner plays in a
// turn — `CardPlaysFinished` counted for the turn, `% Cards(3) == 0`, and only when the
// card is not already in hand. The emulator had a plain attack.
public class MakeItSoTests
{
    private static Fight WithItInTheDiscard()
    {
        var fight = RG.Fresh();
        fight.State.DiscardPile.Add(new CardInstance(RG.MakeItSo, false));
        for (int i = 0; i < 4; i++)
        {
            fight.State.Hand.Add(new CardInstance(RG.Defend, false));
        }

        return fight;
    }

    [Fact]
    public void ItHitsForSixAndNineUpgraded()
    {
        Assert.Equal(494, RG.Fresh().PlayCard(RG.MakeItSo).Enemy0.Hp);
        Assert.Equal(491, RG.Fresh().PlayCard(RG.MakeItSo, upgraded: true).Enemy0.Hp);
    }

    [Fact]
    public void TheThirdSkillPullsItBack()
    {
        var fight = WithItInTheDiscard();

        fight.Play(0);
        fight.Play(0);
        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == RG.MakeItSo);

        fight.Play(0);
        Assert.Contains(fight.State.Hand, c => c.DefId == RG.MakeItSo);
        Assert.DoesNotContain(fight.State.DiscardPile, c => c.DefId == RG.MakeItSo);
    }

    /// <summary>It is the turn's total, so the SIXTH Skill pulls it back too.</summary>
    [Fact]
    public void TheSixthSkillPullsItBackAgain()
    {
        var fight = WithItInTheDiscard();
        for (int i = 0; i < 4; i++)
        {
            fight.State.Hand.Add(new CardInstance(RG.Defend, false));
        }

        for (int i = 0; i < 3; i++)
        {
            fight.Play(0);
        }

        // Send it away again, then finish the turn's sixth Skill.
        fight.State.Hand.RemoveAll(c => c.DefId == RG.MakeItSo);
        fight.State.DiscardPile.Add(new CardInstance(RG.MakeItSo, false));
        for (int i = 0; i < 3; i++)
        {
            fight.Play(0);
        }

        Assert.Contains(fight.State.Hand, c => c.DefId == RG.MakeItSo);
    }

    /// <summary>ATTACKS do not count — it is Skills.</summary>
    [Fact]
    public void AttacksDoNotPullItBack()
    {
        var fight = RG.Fresh();
        fight.State.DiscardPile.Add(new CardInstance(RG.MakeItSo, false));
        for (int i = 0; i < 3; i++)
        {
            fight.State.Hand.Add(new CardInstance(RG.Strike, false));
        }

        for (int i = 0; i < 3; i++)
        {
            fight.Play(0, target: 0);
        }

        Assert.DoesNotContain(fight.State.Hand, c => c.DefId == RG.MakeItSo);
    }

    /// <summary>It comes back from the DRAW pile too — `Pile.Type != Hand`, not "discard".</summary>
    [Fact]
    public void ItComesBackFromTheDrawPile()
    {
        var fight = RG.Fresh();
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(RG.MakeItSo, false));
        for (int i = 0; i < 3; i++)
        {
            fight.State.Hand.Add(new CardInstance(RG.Defend, false));
        }

        for (int i = 0; i < 3; i++)
        {
            fight.Play(0);
        }

        Assert.Contains(fight.State.Hand, c => c.DefId == RG.MakeItSo);
        Assert.Empty(fight.State.DrawPile);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/ManifestAuthority.cs: block 7 upgrading by 1, then ONE
// distinct COLOURLESS card into hand — upgraded if Manifest Authority was, which is the
// only other thing its upgrade buys. The emulator added a random REGENT card.
public class ManifestAuthorityTests
{
    private static bool IsColorless(int defId) =>
        GeneratedData.CardPools.Colorless.IndexOf(defId) >= 0;

    [Fact]
    public void ItGainsSevenBlockAndEightUpgraded()
    {
        Assert.Equal(7, RG.Fresh().PlayCard(RG.ManifestAuthority).State.PlayerBlock);
        Assert.Equal(
            8,
            RG.Fresh().PlayCard(RG.ManifestAuthority, upgraded: true).State.PlayerBlock
        );
    }

    [Fact]
    public void ItAddsOneColorlessCard()
    {
        var fight = RG.Fresh().PlayCard(RG.ManifestAuthority);

        var added = Assert.Single(fight.State.Hand);
        Assert.True(IsColorless(added.DefId));
        Assert.False(added.Upgraded);
    }

    [Fact]
    public void UpgradedTheCardIsUpgradedToo()
    {
        var fight = RG.Fresh().PlayCard(RG.ManifestAuthority, upgraded: true);

        Assert.True(Assert.Single(fight.State.Hand).Upgraded);
    }
}

/// <summary>
/// Ids for the Regent cards whose arms were already right when they were read. Each is
/// pinned below, so the reading is a test rather than a note.
/// </summary>
internal static class RGOk
{
    public const int CloakOfStars = 92;
    public const int Devastate = 143;
    public const int FallingStar = 179;
    public const int GatherLight = 214;
    public const int Glitterstream = 222;
    public const int GuidingStar = 230;
    public const int Hegemony = 242;
    public const int KnockoutBlow = 278;
    public const int Patter = 347;
    public const int CelestialMight = 81;
}

// Strike and Defend: DamageVar(6) upgrading by 3, BlockVar(5) upgrading by 3.
public class StrikeRegentTests
{
    [Fact]
    public void StrikeHitsForSixAndNineUpgraded()
    {
        Assert.Equal(494, RG.Fresh().PlayCard(RG.Strike).Enemy0.Hp);
        Assert.Equal(491, RG.Fresh().PlayCard(RG.Strike, upgraded: true).Enemy0.Hp);
    }
}

public class DefendRegentTests
{
    [Fact]
    public void DefendGainsFiveBlockAndEightUpgraded()
    {
        Assert.Equal(5, RG.Fresh().PlayCard(RG.Defend).State.PlayerBlock);
        Assert.Equal(8, RG.Fresh().PlayCard(RG.Defend, upgraded: true).State.PlayerBlock);
    }
}

// CloakOfStars: one star, block 7 upgrading by 3.
public class CloakOfStarsTests
{
    [Fact]
    public void ItGainsSevenBlockForOneStar()
    {
        var fight = RG.Fresh(stars: 3).PlayCard(RGOk.CloakOfStars);

        Assert.Equal(7, fight.State.PlayerBlock);
        Assert.Equal(2, fight.State.Stars);
    }

    [Fact]
    public void UpgradedItGainsTen()
    {
        Assert.Equal(10, RG.Fresh().PlayCard(RGOk.CloakOfStars, upgraded: true).State.PlayerBlock);
    }
}

// Devastate: four stars, 30 damage upgrading by 10, one target.
public class DevastateTests
{
    [Fact]
    public void ItHitsOneEnemyForThirty()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RGOk.Devastate);

        Assert.Equal(470, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
        Assert.Equal(5, fight.State.Stars);
    }

    [Fact]
    public void UpgradedItHitsForForty()
    {
        Assert.Equal(460, RG.Fresh().PlayCard(RGOk.Devastate, upgraded: true).Enemy0.Hp);
    }
}

// FallingStar: two stars, 8 damage upgrading by 4, Weak 1 and Vulnerable 1 on the target
// — neither of which upgrades.
public class FallingStarTests
{
    [Fact]
    public void ItHitsAndDebuffsTheTargetOnly()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RGOk.FallingStar);

        Assert.Equal(492, fight.Enemy0.Hp);
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Weak));
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
        Assert.Equal(0, BuffSystem.Get(fight.Enemy1.Buffs, BuffId.Weak));
    }

    [Fact]
    public void UpgradingBuysDamageNotDebuff()
    {
        var fight = RG.Fresh().PlayCard(RGOk.FallingStar, upgraded: true);

        Assert.Equal(488, fight.Enemy0.Hp);
        Assert.Equal(1, BuffSystem.Get(fight.Enemy0.Buffs, BuffId.Vulnerable));
    }
}

// GatherLight: block 8 upgrading by 3, and one star. The star does not upgrade.
public class GatherLightTests
{
    [Fact]
    public void ItBlocksAndGainsOneStar()
    {
        var fight = RG.Fresh(stars: 0).PlayCard(RGOk.GatherLight);

        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(1, fight.State.Stars);
    }

    [Fact]
    public void UpgradedItIsElevenBlockAndStillOneStar()
    {
        var fight = RG.Fresh(stars: 0).PlayCard(RGOk.GatherLight, upgraded: true);

        Assert.Equal(11, fight.State.PlayerBlock);
        Assert.Equal(1, fight.State.Stars);
    }
}

// Glitterstream: block 11 upgrading by 2, plus BlockNextTurn 5 upgrading by 2.
public class GlitterstreamTests
{
    [Fact]
    public void ItBlocksNowAndNextTurn()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Glitterstream);

        Assert.Equal(11, fight.State.PlayerBlock);
        Assert.Equal(5, fight.PlayerBuffAmount(BuffId.BlockNextTurn));
    }

    [Fact]
    public void UpgradedBothHalvesGrow()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Glitterstream, upgraded: true);

        Assert.Equal(13, fight.State.PlayerBlock);
        Assert.Equal(7, fight.PlayerBuffAmount(BuffId.BlockNextTurn));
    }
}

// GuidingStar: two stars, 12 damage upgrading by 1, draw 2 upgrading by 1.
public class GuidingStarTests
{
    private static Fight WithDrawPile(int cards)
    {
        var fight = RG.Fresh();
        fight.State.DrawPile.Clear();
        for (int i = 0; i < cards; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(RG.Strike, false));
        }

        return fight;
    }

    [Fact]
    public void ItHitsAndDrawsTwo()
    {
        var fight = WithDrawPile(5).PlayCard(RGOk.GuidingStar);

        Assert.Equal(488, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Hand.Count);
    }

    [Fact]
    public void UpgradedItIsThirteenAndThreeCards()
    {
        var fight = WithDrawPile(5).PlayCard(RGOk.GuidingStar, upgraded: true);

        Assert.Equal(487, fight.Enemy0.Hp);
        Assert.Equal(3, fight.State.Hand.Count);
    }
}

// Hegemony: 15 damage upgrading by 3, and 2 energy next turn upgrading by 1.
public class HegemonyTests
{
    [Fact]
    public void ItHitsAndBanksTwoEnergy()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Hegemony);

        Assert.Equal(485, fight.Enemy0.Hp);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
    }

    [Fact]
    public void UpgradedBothHalvesGrow()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Hegemony, upgraded: true);

        Assert.Equal(482, fight.Enemy0.Hp);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.NextTurnEnergy));
    }
}

// KnockoutBlow: 30 damage upgrading by 8, and five stars ONLY if the target died.
public class KnockoutBlowTests
{
    [Fact]
    public void AKillPaysFiveStars()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 20), 0);

        fight.PlayCard(RGOk.KnockoutBlow);

        Assert.Equal(0, fight.Enemy0.Hp);
        Assert.Equal(5, fight.State.Stars);
    }

    [Fact]
    public void ASurvivorPaysNothing()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500), 0);

        fight.PlayCard(RGOk.KnockoutBlow);

        Assert.Equal(470, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.Stars);
    }
}

// Patter: block 8 upgrading by 2, Vigor 2 upgrading by 1.
public class PatterTests
{
    [Fact]
    public void ItBlocksAndGivesVigor()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Patter);

        Assert.Equal(8, fight.State.PlayerBlock);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Vigor));
    }

    [Fact]
    public void UpgradedBothHalvesGrow()
    {
        var fight = RG.Fresh().PlayCard(RGOk.Patter, upgraded: true);

        Assert.Equal(10, fight.State.PlayerBlock);
        Assert.Equal(3, fight.PlayerBuffAmount(BuffId.Vigor));
    }
}

// SolarStrike: 9 damage upgrading by 1, 1 star upgrading by 1.
public class SolarStrikeTests
{
    [Fact]
    public void ItHitsAndGainsAStar()
    {
        var fight = RG.Fresh(stars: 0).PlayCard(RG.SolarStrike);

        Assert.Equal(491, fight.Enemy0.Hp);
        Assert.Equal(1, fight.State.Stars);
    }

    [Fact]
    public void UpgradedBothHalvesGrow()
    {
        var fight = RG.Fresh(stars: 0).PlayCard(RG.SolarStrike, upgraded: true);

        Assert.Equal(490, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Stars);
    }
}

// ShiningStrike: 8 damage upgrading by 3, two stars, and it returns to the TOP of the draw
// pile — the `!Keywords.Contains(Exhaust)` guard falls out of the play path's ordering,
// which puts the exhaust branch ahead of the draw-pile one.
public class ShiningStrikeTests
{
    [Fact]
    public void ItHitsGainsTwoStarsAndGoesBackOnTop()
    {
        var fight = RG.Fresh(stars: 0);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(RG.Strike, false));

        fight.PlayCard(RG.ShiningStrike);

        Assert.Equal(492, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Stars);
        Assert.Equal(RG.ShiningStrike, fight.State.DrawPile[0].DefId);
        Assert.Empty(fight.State.DiscardPile);
    }

    [Fact]
    public void UpgradedItHitsForEleven()
    {
        Assert.Equal(489, RG.Fresh().PlayCard(RG.ShiningStrike, upgraded: true).Enemy0.Hp);
    }
}

// CelestialMight: 6 damage, `WithHitCount(RepeatVar(3))`, one target. The upgrade buys a
// fourth HIT rather than damage.
public class CelestialMightTests
{
    [Fact]
    public void ItHitsOneEnemyThreeTimes()
    {
        var fight = RegentBoard.WithStars(Fight.Hand().Energy(9).Enemy(hp: 500).Enemy(hp: 500), 9);

        fight.PlayCard(RGOk.CelestialMight);

        Assert.Equal(482, fight.Enemy0.Hp);
        Assert.Equal(500, fight.Enemy1.Hp);
    }

    [Fact]
    public void UpgradingBuysAFourthHit()
    {
        Assert.Equal(476, RG.Fresh().PlayCard(RGOk.CelestialMight, upgraded: true).Enemy0.Hp);
    }
}
