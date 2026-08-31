using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// Defect's twenty-five rares and two ancients, read against
// MegaCrit.Sts2.Core.Models.Cards/*.cs. Five were wrong -- the lowest rate of any Defect
// batch, and the reverse of what rarity would predict.

public class AdaptiveStrikeTests
{
    private const int AdaptiveStrike = 5;

    /// <summary>A clone of itself, free for the combat, into the discard.</summary>
    [Theory]
    [InlineData(false, 18)]
    [InlineData(true, 23)]
    public void HitsAndLeavesAFreeCopy(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(AdaptiveStrike, upgraded)).Energy(2).Enemy(hp: 200);

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        var copy = fight.State.DiscardPile.Where(c => c.DefId == AdaptiveStrike).ToList();
        Assert.Equal(2, copy.Count); // the played card, plus the clone
        Assert.Contains(copy, c => c.CostForCombat == 0);
    }
}

public class AllForOneTests
{
    private const int AllForOne = 12;

    /// <summary>
    /// Every 0-cost Attack, Skill or Power in the DISCARD pile comes back. Statuses and
    /// curses do not — the filter is `(uint)(type - 1) &lt;= 2u`.
    /// </summary>
    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 14)]
    public void HitsThenRecallsTheFreeCards(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(AllForOne, upgraded)).Energy(2).Enemy(hp: 200);
        fight.State.DiscardPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(SI.Slice, false)); // 0-cost attack
        fight.State.DiscardPile.Add(new CardInstance(SI.StrikeSilent, false)); // 1-cost
        fight.State.DiscardPile.Add(new CardInstance(ST.Dazed, false)); // status

        fight.Play();

        Assert.Equal(200 - damage, fight.Enemy0.Hp);
        Assert.Contains(fight.State.Hand, c => c.DefId == SI.Slice);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == SI.StrikeSilent);
        Assert.Contains(fight.State.DiscardPile, c => c.DefId == ST.Dazed);
    }
}

public class BufferTests
{
    private const int Buffer = 63;

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void PreventsThatManyInstancesOfHpLoss(bool upgraded, int charges)
    {
        var fight = DefectFight.Hand(Card(Buffer, upgraded)).Energy(2);
        fight.Play();
        Assert.Equal(charges, fight.PlayerBuffAmount(BuffId.Buffer));

        int hp = fight.State.PlayerHp;
        CardEffects.LoseHp(fight.State, 10);

        Assert.Equal(hp, fight.State.PlayerHp);
        Assert.Equal(charges - 1, fight.PlayerBuffAmount(BuffId.Buffer));
    }
}

public class ConsumingShadowTests
{
    private const int ConsumingShadow = 101;

    /// <summary>
    /// `RepeatVar(2)` +1 Dark orbs, and a power that evokes the LAST orb once at the end
    /// of every turn.
    /// </summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void ChannelsDarkOrbsAndEvokesTheLastEachTurn(bool upgraded, int orbs)
    {
        var fight = DefectFight.Hand(Card(ConsumingShadow, upgraded)).Energy(2).Enemy(hp: 400);
        fight.State.PlayerHp = 999;
        fight.State.OrbCapacity = 6;

        fight.Play();
        Assert.Equal(orbs, fight.State.Orbs.Count);
        Assert.All(fight.State.Orbs, o => Assert.Equal(OrbType.Dark, o.Type));

        fight.EndTurn();

        Assert.Equal(orbs - 1, fight.State.Orbs.Count);
        Assert.True(fight.Enemy0.Hp < 400);
    }
}

public class CoolantTests
{
    private const int Coolant = 103;

    /// <summary>Block per DISTINCT orb type, at the start of each turn, unpowered.</summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void BlocksPerDistinctOrbTypeEachTurn(bool upgraded, int per)
    {
        var fight = DefectFight.Hand(Card(Coolant, upgraded)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Barricade, 1);
        fight.Play();
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);

        int before = fight.State.PlayerBlock;
        fight.EndTurn();

        // Two distinct types, plus the two Frost passives at end of turn.
        Assert.Equal(before + per * 2 + 4, fight.State.PlayerBlock);
    }
}

public class CreativeAiTests
{
    private const int CreativeAi = 111;

    [Fact]
    public void APowerCardArrivesEachTurn()
    {
        var fight = DefectFight.Hand(Card(CreativeAi)).Energy(3);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        fight.Play();

        fight.EndTurn();

        Assert.Contains(
            fight.State.Hand,
            c => GeneratedData.Cards.Get(c.DefId).Type == CardType.Power
        );
    }
}

public class DefragmentTests
{
    private const int Defragment = 137;

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void GrantsPermanentFocus(bool upgraded, int focus)
    {
        var fight = DefectFight.Hand(Card(Defragment, upgraded)).Energy(1).Enemy(hp: 60);
        fight.State.PlayerHp = 999;
        fight.Play();
        Assert.Equal(focus, fight.PlayerBuffAmount(BuffId.Focus));

        fight.EndTurn();

        Assert.Equal(focus, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

public class EchoFormTests
{
    private const int EchoForm = 159;

    /// <summary>
    /// The first card each turn plays twice — and playing ECHO FORM is that card, so nothing
    /// doubles on the turn it lands.
    /// </summary>
    /// <remarks>
    /// `EchoFormPower.ModifyCardPlayCount` counts `CardPlaysStarted` this turn and adds a
    /// play only while that count is under its Amount. The count is settled when the play is
    /// SET UP, the same moment Burst's is, so two things follow: Echo Form does not double
    /// ITSELF, because its power does not exist yet; and the next card sees a count of one
    /// and gets nothing either.
    ///
    /// This test used to assert that the card after Echo Form played twice, which is what
    /// the emulator did when it read the count AFTER the card resolved — and reading it
    /// there also made Echo Form apply ITSELF twice, which is how a live capture found it.
    /// </remarks>
    [Fact]
    public void NothingDoublesOnTheTurnEchoFormLands()
    {
        var fight = DefectFight
            .Hand(Card(EchoForm), Card(SI.Slice), Card(SI.Slice))
            .Energy(9)
            .Enemy(hp: 400);

        fight.Play();
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.EchoForm));

        fight.Play();
        Assert.Equal(400 - 6, fight.Enemy0.Hp);

        fight.Play();
        Assert.Equal(400 - 12, fight.Enemy0.Hp);
    }

    /// <summary>Next turn the first card really does play twice.</summary>
    [Fact]
    public void TheFirstCardOfTheNextTurnPlaysTwice()
    {
        var fight = DefectFight.Hand(Card(EchoForm)).Energy(9).Enemy(hp: 400);
        fight.Play();
        fight.EndTurn();

        fight.State.Energy = 9;
        fight.State.Hand.Add(Card(SI.Slice));
        fight.State.Hand.Add(Card(SI.Slice));

        fight.Play(fight.State.Hand.Count - 2);
        Assert.Equal(400 - 12, fight.Enemy0.Hp);

        fight.Play(fight.State.Hand.Count - 1);
        Assert.Equal(400 - 12 - 6, fight.Enemy0.Hp);
    }
}

public class FlakCannonTests
{
    private const int FlakCannon = 194;

    /// <summary>
    /// Exhausts every Status card outside the exhaust pile and hits once per status, at
    /// rolled targets.
    /// </summary>
    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 11)]
    public void OneHitPerStatusExhausted(bool upgraded, int damage)
    {
        var fight = Fight
            .Hand(Card(FlakCannon, upgraded), Card(ST.Dazed), Card(ST.Slimed))
            .Energy(2)
            .Enemy(hp: 400);
        fight.State.DiscardPile.Add(new CardInstance(ST.Wound, false));

        fight.Play();

        Assert.Equal(400 - damage * 3, fight.Enemy0.Hp);
        Assert.Equal(
            3,
            fight.State.ExhaustPile.Count(c => c.DefId is ST.Dazed or ST.Slimed or ST.Wound)
        );
    }

    [Fact]
    public void WithNoStatusesItDealsNothing()
    {
        var fight = DefectFight.Hand(Card(FlakCannon)).Energy(2).Enemy(hp: 400);
        fight.Play();
        Assert.Equal(400, fight.Enemy0.Hp);
    }
}

/// <summary>
/// Genetic Algorithm starts at ONE block and grows by three every play.
/// </summary>
/// <remarks>
/// `BlockVar(CurrentBlock)` where CurrentBlock starts at 1, plus `IntVar("Increase", 3m)`
/// added on every play — permanently, on the card. The extractor reads 0 for the base
/// because the var is a property rather than a literal, so the emulator blocked NOTHING
/// however many times it was played.
/// </remarks>
public class GeneticAlgorithmTests
{
    private const int GeneticAlgorithm = 216;

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void ItStartsAtOneAndGrows(bool upgraded, int increase)
    {
        var fight = Fight
            .Hand(Card(GeneticAlgorithm, upgraded), Card(GeneticAlgorithm, upgraded))
            .Energy(3);

        fight.Play();
        Assert.Equal(1, fight.State.PlayerBlock);

        // The growth rides on the COPY, so the second card is still a fresh one.
        fight.Play();
        Assert.Equal(1 + 1, fight.State.PlayerBlock);

        // The card Exhausts, so the grown copy lands there rather than in the discard.
        var played = fight.State.ExhaustPile.First(c => c.DefId == GeneticAlgorithm);
        Assert.Equal(increase, played.BonusBlock);
    }

    /// <summary>
    /// The grown copy blocks more if it comes back. Within one combat it usually does not
    /// — the card Exhausts — which is the point of the half that is NOT modelled: the game
    /// also buffs `base.DeckVersion`, so the growth carries into the next fight. Combat
    /// builds its card list fresh from the deck here, so that half is run state.
    /// </summary>
    [Fact]
    public void TheGrownCopyBlocksMore()
    {
        var fight = DefectFight.Hand(Card(GeneticAlgorithm)).Energy(9);
        fight.Play();

        var grown = fight.State.ExhaustPile.Single(c => c.DefId == GeneticAlgorithm);
        fight.State.Hand.Add(grown);
        int before = fight.State.PlayerBlock;
        fight.Play(fight.State.Hand.FindIndex(c => c.DefId == GeneticAlgorithm));

        Assert.Equal(before + 4, fight.State.PlayerBlock);
    }
}

/// <summary>
/// Helix Drill hits once per point of energy SPENT this turn.
/// </summary>
/// <remarks>
/// The count is the turn's `EnergySpentEntry` total minus the drill's own cost, which is
/// zero. The emulator read `state.Energy` — the energy REMAINING — which is very nearly
/// the opposite: it paid most on a turn where nothing had been spent, and nothing on the
/// turn the card is designed for.
/// </remarks>
public class HelixDrillTests
{
    private const int HelixDrill = 244;

    [Fact]
    public void NothingSpentMeansNoHits()
    {
        var fight = DefectFight.Hand(Card(HelixDrill)).Energy(3).Enemy(hp: 400);
        fight.Play();
        Assert.Equal(400, fight.Enemy0.Hp);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public void OneHitPerEnergyAlreadySpent(bool upgraded, int damage)
    {
        var fight = Fight
            .Hand(Card(SI.StrikeSilent), Card(SI.DefendSilent), Card(HelixDrill, upgraded))
            .Energy(9)
            .Enemy(hp: 400);
        fight.Play(); // 1 energy
        fight.Play(); // 1 energy
        int before = fight.Enemy0.Hp;

        fight.Play();

        Assert.Equal(before - damage * 2, fight.Enemy0.Hp);
    }
}

public class HyperbeamTests
{
    private const int Hyperbeam = 256;

    /// <summary>`PowerVar<FocusPower>(3m)` is NOT upgraded — only the damage is.</summary>
    [Theory]
    [InlineData(false, 28)]
    [InlineData(true, 36)]
    public void HitsEveryoneAndCostsThreeFocus(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Hyperbeam, upgraded)).Energy(2).Enemy(hp: 400);
        fight.Enemy(hp: 400);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 5);

        fight.Play();

        Assert.Equal(400 - damage, fight.Enemy0.Hp);
        Assert.Equal(400 - damage, fight.Enemy1.Hp);
        Assert.Equal(2, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

public class IceLanceTests
{
    private const int IceLance = 258;

    [Theory]
    [InlineData(false, 19)]
    [InlineData(true, 24)]
    public void HitsThenChannelsThreeFrost(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(IceLance, upgraded)).Energy(3).Enemy(hp: 400);
        fight.State.OrbCapacity = 5;

        fight.Play();

        Assert.Equal(400 - damage, fight.Enemy0.Hp);
        Assert.Equal(3, fight.State.Orbs.Count(o => o.Type == OrbType.Frost));
    }
}

public class MachineLearningTests
{
    private const int MachineLearning = 291;

    [Fact]
    public void DrawsOneMoreEachTurn()
    {
        var fight = DefectFight.Hand(Card(MachineLearning)).Energy(1);
        fight.State.PlayerHp = 999;
        fight.State.DrawPile.Clear();
        for (int i = 0; i < 20; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(SI.Backstab, false));
        }

        fight.Play();
        fight.EndTurn();

        Assert.Equal(6, fight.State.Hand.Count);
    }

    [Fact]
    public void TheUpgradeMakesItInnate()
    {
        Assert.True(GeneratedData.Cards.Get(MachineLearning).InnateWhenUpgraded);
    }
}

public class MeteorStrikeTests
{
    private const int MeteorStrike = 305;

    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 30)]
    public void HitsHardAndChannelsThreePlasma(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(MeteorStrike, upgraded)).Energy(5).Enemy(hp: 400);
        fight.State.OrbCapacity = 5;

        fight.Play();

        Assert.Equal(400 - damage, fight.Enemy0.Hp);
        Assert.Equal(3, fight.State.Orbs.Count(o => o.Type == OrbType.Plasma));
    }
}

public class MultiCastTests
{
    private const int MultiCast = 317;

    /// <summary>
    /// Evokes the FRONT orb X times, dequeuing only on the last — so one orb pays out X
    /// times and then leaves.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void TheFrontOrbEvokesOncePerEnergy(bool upgraded, int times)
    {
        var fight = DefectFight.Hand(Card(MultiCast, upgraded)).Energy(3).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        Assert.Equal(400 - 8 * times, fight.Enemy0.Hp);
        Assert.Equal([OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }
}

public class IgnitionTests
{
    private const int Ignition = 259;

    /// <summary>
    /// MultiplayerOnly, and it channels a Plasma orb on the TARGET ally — which in a
    /// single-player fight is always the player. Shares Fusion's arm for that reason.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ChannelsPlasmaAndTheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Ignition, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal([OrbType.Plasma], fight.State.Orbs.Select(o => o.Type));
        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Ignition));
    }
}

public class RainbowTests
{
    private const int Rainbow = 379;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ChannelsOneOfEachAndTheUpgradeRemovesExhaust(bool upgraded, bool exhausts)
    {
        var fight = DefectFight.Hand(Card(Rainbow, upgraded)).Energy(2);

        fight.Play();

        Assert.Equal(
            [OrbType.Lightning, OrbType.Frost, OrbType.Dark],
            fight.State.Orbs.Select(o => o.Type)
        );
        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == Rainbow));
    }
}

public class RebootTests
{
    private const int Reboot = 387;

    /// <summary>Hand into the draw pile, shuffle, then draw. `CardsVar(4)` upgrades by TWO.</summary>
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void ShufflesEverythingBackAndDraws(bool upgraded, int cards)
    {
        var fight = DefectFight.Hand(Card(Reboot, upgraded), Card(SI.Backstab)).Energy(0);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Clear();
        for (int i = 0; i < 10; i++)
        {
            fight.State.DiscardPile.Add(new CardInstance(SI.Slice, false));
        }

        fight.Play();

        Assert.Equal(cards, fight.State.Hand.Count);
        Assert.Empty(fight.State.DiscardPile);
    }
}

public class ShatterTests
{
    private const int Shatter = 428;

    /// <summary>Hits everything, then evokes the front orb TWICE per orb held.</summary>
    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 11)]
    public void HitsAllThenDoubleEvokesTheWholeRing(bool upgraded, int damage)
    {
        var fight = DefectFight.Hand(Card(Shatter, upgraded)).Energy(1).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        fight.Play();

        // The card's damage, then two orbs evoking twice each for 8.
        Assert.Equal(400 - damage - 32, fight.Enemy0.Hp);
        Assert.Empty(fight.State.Orbs);
    }
}

public class SignalBoostTests
{
    private const int SignalBoost = 435;
    private const int Capacitor = 78;

    /// <summary>The next POWER card plays twice, then the charge is spent.</summary>
    [Fact]
    public void TheNextPowerPlaysTwice()
    {
        var fight = DefectFight.Hand(Card(SignalBoost), Card(Capacitor), Card(Capacitor)).Energy(9);
        fight.Play();

        fight.Play();
        Assert.Equal(3 + 4, fight.State.OrbCapacity);

        fight.Play();
        Assert.Equal(3 + 4 + 2, fight.State.OrbCapacity);
    }
}

/// <summary>
/// Spinner's Glass orb on play is the WHOLE upgrade.
/// </summary>
/// <remarks>
/// `if (base.IsUpgraded) { await OrbCmd.Channel&lt;GlassOrb&gt;(...); }` — the emulator
/// channelled it at both levels, so the upgrade did nothing and the base card was a rare's
/// worth better than it is.
/// </remarks>
public class SpinnerTests
{
    private const int Spinner = 452;

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void OnlyTheUpgradedCopyChannelsOnPlay(bool upgraded, int orbs)
    {
        var fight = DefectFight.Hand(Card(Spinner, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(orbs, fight.State.Orbs.Count);
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Spinner));
    }

    [Fact]
    public void AGlassOrbArrivesEachTurnEitherWay()
    {
        var fight = DefectFight.Hand(Card(Spinner)).Energy(1).Enemy(hp: 400);
        fight.State.PlayerHp = 999;
        fight.Play();

        fight.EndTurn();

        Assert.Contains(fight.State.Orbs, o => o.Type == OrbType.Glass);
    }
}

public class SupercriticalTests
{
    private const int Supercritical = 480;

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void GivesEnergyAndExhausts(bool upgraded, int energy)
    {
        var fight = DefectFight.Hand(Card(Supercritical, upgraded)).Energy(0);
        fight.Play();
        Assert.Equal(energy, fight.State.Energy);
        Assert.Contains(fight.State.ExhaustPile, c => c.DefId == Supercritical);
    }
}

public class TrashToTreasureTests
{
    private const int TrashToTreasure = 515;
    private const int GunkUp = 231;

    /// <summary>A generated Status channels a RANDOM orb — five types, on the orb stream.</summary>
    [Fact]
    public void AGeneratedStatusChannelsARandomOrb()
    {
        var fight = DefectFight.Hand(Card(TrashToTreasure), Card(GunkUp)).Energy(9).Enemy(hp: 400);
        fight.Play();
        Assert.Empty(fight.State.Orbs);

        fight.Play();

        Assert.Single(fight.State.Orbs);
    }

    /// <summary>Glass is in the roll, as `GetRandomOrb` covers all five valid orbs.</summary>
    [Fact]
    public void GlassIsInTheRoll()
    {
        var seen = new HashSet<OrbType>();
        for (int seed = 0; seed < 40; seed++)
        {
            var fight = DefectFight
                .Hand(Card(TrashToTreasure), Card(GunkUp))
                .Energy(9)
                .Enemy(hp: 400);
            fight.State.OrbGenerationRng = new CountingRandom(seed);
            fight.Play();
            fight.Play();
            seen.UnionWith(fight.State.Orbs.Select(o => o.Type));
        }

        Assert.Contains(OrbType.Glass, seen);
    }
}

public class VoltaicTests
{
    private const int Voltaic = 536;

    /// <summary>One Lightning orb per Lightning channelled EARLIER in this combat.</summary>
    [Fact]
    public void ChannelsOnePerLightningAlreadyChannelled()
    {
        var fight = DefectFight.Hand(Card(Voltaic)).Energy(3);
        fight.State.OrbCapacity = 10;
        for (int i = 0; i < 3; i++)
        {
            CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        }

        fight.Play();

        Assert.Equal(6, fight.State.Orbs.Count(o => o.Type == OrbType.Lightning));
    }
}

/// <summary>
/// Biased Cognition gives 4 Focus and takes one back every turn.
/// </summary>
/// <remarks>
/// Two vars, and the emulator read one: `PowerVar&lt;FocusPower&gt;(4m)` +1 is the Focus,
/// and `PowerVar&lt;BiasedCognitionPower&gt;(1m)` is the DRAIN. Without it an Ancient card
/// was 4 Focus for one energy and no downside at all.
/// </remarks>
public class BiasedCognitionTests
{
    private const int BiasedCognition = 39;

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public void GrantsFocusThenBleedsIt(bool upgraded, int focus)
    {
        var fight = DefectFight.Hand(Card(BiasedCognition, upgraded)).Energy(1).Enemy(hp: 200);
        fight.State.PlayerHp = 999;

        fight.Play();
        Assert.Equal(focus, fight.PlayerBuffAmount(BuffId.Focus));
        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.BiasedCognition));

        fight.EndTurn();
        Assert.Equal(focus - 1, fight.PlayerBuffAmount(BuffId.Focus));

        fight.EndTurn();
        Assert.Equal(focus - 2, fight.PlayerBuffAmount(BuffId.Focus));
    }
}

public class QuadcastTests
{
    private const int Quadcast = 375;

    /// <summary>The front orb evokes four times and then leaves.</summary>
    [Fact]
    public void TheFrontOrbEvokesFourTimes()
    {
        var fight = DefectFight.Hand(Card(Quadcast)).Energy(1).Enemy(hp: 400);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        fight.Play();

        Assert.Equal(400 - 32, fight.Enemy0.Hp);
        Assert.Equal([OrbType.Frost], fight.State.Orbs.Select(o => o.Type));
    }

    [Fact]
    public void WithNoOrbsItDoesNothing()
    {
        var fight = DefectFight.Hand(Card(Quadcast)).Energy(1).Enemy(hp: 400);
        fight.Play();
        Assert.Equal(400, fight.Enemy0.Hp);
    }
}
