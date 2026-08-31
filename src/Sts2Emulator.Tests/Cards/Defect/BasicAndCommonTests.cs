using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Defect's four basics and twenty commons, read against
// MegaCrit.Sts2.Core.Models.Cards/*.cs. Six were wrong.
//
// Every one of these runs on the by-NAME approximation path rather than an id switch,
// which is worth knowing while reading: the arms are grouped by SHAPE ("these all gain
// block") with per-card `else if` tails, and that grouping is exactly how Turbo lost its
// Void and Momentum Strike's cost change ended up written to a by-value parameter.

public class StrikeDefectTests
{
    private const int StrikeDefect = 471;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void StrikeHits(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(StrikeDefect, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
    }
}

public class DefendDefectTests
{
    private const int DefendDefect = 130;

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void DefendBlocks(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(DefendDefect, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }

    /// <summary>It is Defend-tagged, so Fasten pays out on it (E175).</summary>
    [Fact]
    public void DefendCarriesTheDefendTag()
    {
        var fight = DefectFight.Hand(Card(CL.Fasten), Card(DefendDefect)).Energy(3);
        fight.Play();

        fight.Play();

        Assert.Equal(5 + 4, fight.State.PlayerBlock);
    }
}

public class ZapTests
{
    private const int Zap = 545;

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void ZapChannelsLightningAndTheUpgradeIsTheCost(bool upgraded, int cost)
    {
        var fight = DefectFight.Hand(Card(Zap, upgraded)).Energy(3);
        int before = fight.State.Energy;

        fight.Play();

        Assert.Equal([OrbType.Lightning], fight.State.Orbs.Select(o => o.Type));
        Assert.Equal(before - cost, fight.State.Energy);
    }
}

public class DualcastTests
{
    private const int Dualcast = 156;

    /// <summary>
    /// Dualcast evokes the FRONT orb twice: once without dequeuing and once with. So a
    /// single orb pays out twice and then leaves — it is not "evoke your two front orbs".
    /// </summary>
    [Fact]
    public void DualcastEvokesTheFrontOrbTwice()
    {
        var fight = DefectFight.Hand(Card(Dualcast)).Energy(3).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        // Two Lightning evokes, and the Frost is untouched behind it.
        Assert.Equal(200 - 16, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.PlayerBlock);
        Assert.Equal([OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }

    [Fact]
    public void DualcastWithNoOrbsDoesNothing()
    {
        var fight = DefectFight.Hand(Card(Dualcast)).Energy(3).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200, fight.Enemy0.Hp);
    }
}

public class BallLightningTests
{
    private const int BallLightning = 26;

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 10)]
    public void BallLightningHitsThenChannels(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(BallLightning, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal([OrbType.Lightning], fight.State.Orbs.Select(o => o.Type));
    }
}

public class ColdSnapTests
{
    private const int ColdSnap = 93;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void ColdSnapHitsThenChannels(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(ColdSnap, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal([OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }
}

public class CoolheadedTests
{
    private const int Coolheaded = 104;

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void CoolheadedChannelsThenDraws(bool upgraded, int cards)
    {
        var fight = DefectFight.Hand(Card(Coolheaded, upgraded)).Energy(1);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();

        Assert.Equal([OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
        Assert.Equal(cards, fight.State.Hand.Count);
    }
}

public class BarrageTests
{
    private const int Barrage = 28;

    /// <summary>Barrage hits once per orb HELD, whatever they are.</summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void BarrageHitsOncePerOrb(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Barrage, upgraded)).Energy(1).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        Assert.Equal(200 - damage * 3, fight.Enemy0.Hp);
    }

    [Fact]
    public void BarrageWithNoOrbsDealsNothing()
    {
        var fight = DefectFight.Hand(Card(Barrage)).Energy(1).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200, fight.Enemy0.Hp);
    }
}

public class CompileDriverTests
{
    private const int CompileDriver = 98;

    /// <summary>
    /// Compile Driver draws one per DISTINCT orb type — `group orb by orb.Id` — so three
    /// Frost orbs are one card, not three.
    /// </summary>
    [Fact]
    public void CompileDriverDrawsOncePerDistinctOrbType()
    {
        var fight = DefectFight.Hand(Card(CompileDriver)).Energy(1).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 6; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        fight.Play();

        Assert.Equal(200 - 7, fight.Enemy0.Hp);
        Assert.Equal(2, fight.State.Hand.Count);
    }
}

public class BeamCellTests
{
    private const int BeamCell = 33;

    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(true, 4, 2)]
    public void BeamCellHitsAndMakesVulnerable(bool upgraded, int damage, int vulnerable)
    {
        var fight = DefectFight.Hand(Card(BeamCell, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(vulnerable, fight.EnemyBuffAmount(BuffId.Vulnerable));
    }
}

public class BoostAwayTests
{
    private const int BoostAway = 54;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void BoostAwayBlocksAndDazes(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(BoostAway, upgraded)).Energy(0);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == ST.Dazed);
    }
}

public class LeapTests
{
    private const int Leap = 282;

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public void LeapBlocks(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(Leap, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }
}

public class GunkUpTests
{
    private const int GunkUp = 231;

    /// <summary>`RepeatVar(3)` is not upgraded — the upgrade raises the per-hit damage.</summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public void GunkUpHitsThreeTimesAndSlimes(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(GunkUp, upgraded)).Energy(1).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200 - damage * 3, fight.Enemy0.Hp);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == ST.Slimed);
    }
}

public class SweepingBeamTests
{
    private const int SweepingBeam = 484;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void SweepingBeamHitsEveryEnemyAndDrawsOne(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(SweepingBeam, upgraded)).Energy(1).Enemy(hp: 60);
        fight.Enemy(hp: 60);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(60 - damage, fight.Enemy1.Hp);
        // CardsVar(1) is not upgraded; only the damage is.
        Assert.Single(fight.State.Hand);
    }
}

public class FocusedStrikeTests
{
    private const int FocusedStrike = 201;

    /// <summary>
    /// Focused Strike's Focus is TEMPORARY — `FocusedStrikePower : TemporaryFocusPower`,
    /// handed back at the end of the turn like Piercing Wail's Strength.
    /// </summary>
    [Theory]
    [InlineData(false, 9, 1)]
    [InlineData(true, 11, 2)]
    public void FocusedStrikeHitsAndGivesTemporaryFocus(bool upgraded, int damage, int focus)
    {
        var fight = DefectFight.Hand(Card(FocusedStrike, upgraded)).Energy(1).Enemy(hp: 60);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(focus, fight.PlayerBuffAmount(BuffId.Focus));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

public class ChargeBatteryTests
{
    private const int ChargeBattery = 84;

    /// <summary>
    /// `BlockVar(7m)` +3 and `EnergyVar(1)` — `OnUpgrade` names the block only, so the
    /// energy is 1 at both levels. It arrives at the NEXT turn's energy reset.
    /// </summary>
    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 10)]
    public void BlocksNowAndGivesOneEnergyNextTurn(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(ChargeBattery, upgraded)).Energy(3).Enemy(hp: 60);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);

        fight.EndTurn();

        Assert.Equal(fight.State.MaxEnergy + 1, fight.State.Energy);
    }
}

public class ClawTests
{
    private const int Claw = 89;

    /// <summary>
    /// Every Claw the player owns gains the Increase, so the second Claw of a combat hits
    /// for 5 and the third for 7.
    /// </summary>
    [Theory]
    [InlineData(false, 3, 2)]
    [InlineData(true, 4, 3)]
    public void EveryClawGrows(bool upgraded, int damage, int increase)
    {
        var fight = Fight
            .Hand(Card(Claw, upgraded), Card(Claw, upgraded), Card(Claw, upgraded))
            .Energy(3)
            .Enemy(hp: 200);

        fight.Play();
        Assert.Equal(200 - damage, fight.Enemy0.Hp);

        fight.Play();
        Assert.Equal(200 - damage - (damage + increase), fight.Enemy0.Hp);

        fight.Play();
        Assert.Equal(200 - damage - (damage + increase) - (damage + increase * 2), fight.Enemy0.Hp);
    }
}

/// <summary>
/// Momentum Strike is free for the rest of the combat once it has been played.
/// </summary>
/// <remarks>
/// `base.EnergyCost.SetThisCombat(0)`. The emulator wrote that to `card`, which is a
/// BY-VALUE parameter of the approximation function — so the copy that went to the discard
/// pile was untouched and the card never got cheaper. The change is handed back through
/// the state now, the way `PlayedCardCostBump` already was.
/// </remarks>
public class MomentumStrikeTests
{
    private const int MomentumStrike = 314;

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 13)]
    public void HitsAndIsFreeAfterwards(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(MomentumStrike, upgraded)).Energy(3).Enemy(hp: 200);

        int before = fight.State.Energy;
        fight.Play();
        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Equal(before - 1, fight.State.Energy);

        var played = fight.State.DiscardPile.Single(c => c.DefId == MomentumStrike);
        Assert.Equal(0, played.CostForCombat);

        fight.State.Hand.Add(played);
        before = fight.State.Energy;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == MomentumStrike));

        Assert.Equal(before, fight.State.Energy);
    }
}

/// <summary>
/// Go for the Eyes reads the whole announced MOVE, not just its first intent.
/// </summary>
/// <remarks>
/// `MonsterModel.IntendsToAttack` is `NextMove.Intents.Any(i => i is Attack or DeathBlow)`.
/// An enemy whose move is block-then-attack announces the block first, and the emulator —
/// checking only `CurrentIntent.Type` — read that as "not attacking" and applied no Weak.
/// </remarks>
public class GoForTheEyesTests
{
    private const int GoForTheEyes = 224;

    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(true, 4, 2)]
    public void HitsAndWeakensAnAttacker(bool upgraded, int damage, int weak)
    {
        var fight = DefectFight.Hand(Card(GoForTheEyes, upgraded)).Energy(0).Enemy(hp: 60);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Attack, 5, 1, true);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
    }

    [Fact]
    public void ANonAttackerIsNotWeakened()
    {
        var fight = DefectFight.Hand(Card(GoForTheEyes)).Energy(0).Enemy(hp: 60);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Buff, 0, 0, false);

        fight.Play();

        Assert.Equal(0, fight.EnemyBuffAmount(BuffId.Weak));
    }

    /// <summary>
    /// A move that blocks AND attacks announces the block first. `Any` covers it; reading
    /// only the primary intent does not.
    /// </summary>
    [Fact]
    public void AnEnemyWhoseSecondIntentIsTheAttackIsStillWeakened()
    {
        var fight = DefectFight.Hand(Card(GoForTheEyes)).Energy(0).Enemy(hp: 60);
        fight.Enemy0.CurrentIntent = new Intent(IntentType.Defend, 0, 0, false);
        fight.Enemy0.SecondaryIntent = new Intent(IntentType.Attack, 5, 1, true);

        fight.Play();

        Assert.Equal(1, fight.EnemyBuffAmount(BuffId.Weak));
    }
}

/// <summary>
/// Hologram asks WHICH card comes back from the discard pile.
/// </summary>
public class HologramTests
{
    private const int Hologram = 252;

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public void BlocksThenAsks(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(Hologram, upgraded)).Energy(1);
        fight.State.DiscardPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(SI.Backstab, false));
        fight.State.DiscardPile.Add(new CardInstance(SI.Slice, false));

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
        Assert.Equal(CardSelectionKind.DiscardToHand, fight.Pending!.Kind);
        Assert.Equal(2, fight.Pending.Candidates.Count);
    }

    /// <summary>The pick is the player's, and it is not "whatever was discarded last".</summary>
    [Fact]
    public void TheChosenCardComesBack()
    {
        var fight = DefectFight.Hand(Card(Hologram)).Energy(1);
        fight.State.DiscardPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(SI.Backstab, false));
        fight.State.DiscardPile.Add(new CardInstance(SI.Slice, false));
        fight.Play();

        fight.Choose(0); // the Backstab, which is not the top of the pile

        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Backstab);
        Assert.DoesNotContain(fight.State.DiscardPile, c => c.DefId == SI.Backstab);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Slice);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Hologram, upgraded)).Energy(1);
        fight.State.DiscardPile.Clear();

        fight.Play();

        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Hologram));
    }
}

public class HotfixTests
{
    private const int Hotfix = 253;

    /// <summary>
    /// `PowerVar<FocusPower>(2m)` and `OnUpgrade` only removes Exhaust — so it is Focus 2
    /// at both levels, and TEMPORARY, as `HotfixPower : TemporaryFocusPower`.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TwoTemporaryFocusAtBothLevels(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Hotfix, upgraded)).Energy(0).Enemy(hp: 60);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Hotfix));

        fight.EndTurn();

        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

/// <summary>
/// Lightning Rod channels a Lightning orb at the start of each of the next two turns.
/// </summary>
/// <remarks>
/// `LightningRodPower.AfterEnergyReset` channels and then DECREMENTS, so `PowerVar(2m)` is
/// two TURNS of orbs. The emulator applied `BuffId.Thunder`, which the Lightning EVOKE
/// reads for extra damage — a different power on a different orb event, at a number the
/// card does not have (it read the upgrade as 3, where `OnUpgrade` raises the BLOCK).
/// </remarks>
public class LightningRodTests
{
    private const int LightningRod = 287;

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 7)]
    public void BlocksAndArmsTwoTurnsOfOrbs(bool upgraded, int block)
    {
        var fight = DefectFight.Hand(Card(LightningRod, upgraded)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(block, fight.State.PlayerBlock);
        // Two at BOTH levels: the upgrade raises the block.
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.LightningRod));
        Assert.Empty(fight.State.Orbs);

        fight.EndTurn();
        Assert.Equal([OrbType.Lightning], fight.State.Orbs.Select(o => o.Type));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.LightningRod));

        fight.EndTurn();
        Assert.Equal(2, fight.State.Orbs.Count);
        Assert.Equal(0, fight.PlayerBuffAmount(BuffId.LightningRod));

        fight.EndTurn();
        Assert.Equal(2, fight.State.Orbs.Count);
    }
}

/// <summary>
/// Turbo's Void is the card. Two energy for nothing would be the best card in the game.
/// </summary>
public class TurboTests
{
    private const int Turbo = 518;
    private const int VoidStatus = 10040;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void GivesEnergyAndAVoid(bool upgraded, int energy)
    {
        var fight = DefectFight.Hand(Card(Turbo, upgraded)).Energy(0);

        fight.Play();

        Assert.Equal(energy, fight.State.Energy);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == VoidStatus);
    }
}

/// <summary>
/// Uproar hits twice and then auto-plays a RANDOM Attack out of the draw pile.
/// </summary>
/// <remarks>
/// The card `StableShuffle`s the playable Attacks on `Rng.Shuffle` and takes the first.
/// The emulator took the first in PILE order — deterministic where the game is not, and
/// leaving the shuffle stream undrawn, which desynchronises every roll after it.
/// </remarks>
public class UproarTests
{
    private const int Uproar = 530;

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public void HitsTwiceThenPlaysAnAttack(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Uproar, upgraded)).Energy(2).Enemy(hp: 400);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Slice, false));

        fight.Play();

        Assert.Equal(400 - damage * 2 - 6, fight.Enemy0.Hp);
        Assert.DoesNotContain(fight.State.DrawPile, c => c.DefId == SI.Slice);
    }

    /// <summary>
    /// The auto-played attack ends up in the DISCARD pile, because `CardCmd.AutoPlay` moves
    /// the card to the Play pile and then to its result pile. The emulator took it out of
    /// the draw pile and never put it down, so the card vanished from the combat — three
    /// tests here watched its damage and its absence from the draw pile, and none of them
    /// asked where it went. A live Uproar found it as a discard pile one short.
    /// </summary>
    [Fact]
    public void TheAutoPlayedAttackLandsInTheDiscardPile()
    {
        var fight = DefectFight.Hand(Card(Uproar)).Energy(2).Enemy(hp: 400);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.Slice, false));

        fight.Play();

        // Uproar itself and the Slice it played.
        Assert.Equal(2, fight.State.DiscardPile.Count);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.Slice);
    }

    /// <summary>Which attack it plays is rolled, not the first one in the pile.</summary>
    [Fact]
    public void TheAttackIsRolled()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 12; seed++)
        {
            var fight = DefectFight.Hand(Card(Uproar)).Energy(2).Enemy(hp: 400);
            fight.State.ShuffleRng = new CountingRandom(seed);
            fight.State.DrawPile.Clear();
            fight.State.DrawPile.Add(new CardInstance(SI.Slice, false));
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
            fight.Play();

            // Slice is 6 and Backstab is 11, so the HP left says which one was played.
            seen.Add(fight.Enemy0.Hp);
        }

        Assert.True(seen.Count > 1, "the same attack was played on every shuffle seed");
    }

    /// <summary>A draw pile with no Attacks in it just means the extra play does not happen.</summary>
    [Fact]
    public void NoAttackInTheDrawPileIsFine()
    {
        var fight = DefectFight.Hand(Card(Uproar)).Energy(2).Enemy(hp: 400);
        fight.State.DrawPile.Clear();
        fight.State.DrawPile.Add(new CardInstance(SI.DefendSilent, false));

        fight.Play();

        Assert.Equal(400 - 12, fight.Enemy0.Hp);
    }
}
