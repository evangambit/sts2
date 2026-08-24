using System.Collections.Generic;
using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What each ancient offers, read off its <c>GenerateInitialOptions</c>.
/// </summary>
/// <remarks>
/// Every act opens on an ancient; act 1's is Neow and Hive's three are Orobas, Pael and
/// Tezcatara. They all share one shape — three blessings, one drawn from each of three
/// pools, off the ancient's own Rng in pool order — and differ in their pools and in
/// which entries are conditional on the run. Neow is the odd one out and has its own
/// generator: a curse plus a shuffled positive list, not three pools.
///
/// <para>
/// These are written against the decompiled models rather than a capture, because a
/// capture costs an act-2 run and only ever exercises ONE ancient on ONE seed. The single
/// live data point there is that `ACT2TEST01` is offered Pael's Horn, which
/// <see cref="PaelOffersFromEachPoolInOrder"/> covers.
/// </para>
/// </remarks>
public class AncientOptionTests
{
    /// <summary>
    /// One generated run, cloned for every probe. <c>Reset</c> costs ~150ms — map
    /// generation plus the grab bags' shuffles — and these tests sweep hundreds of seeds,
    /// which is how a class like this quietly becomes a minute of the suite. Cloning is
    /// ~0.07ms, and the sweeps only need a different RNG SEED, not a different map.
    /// </summary>
    private static readonly RunEngine Pristine = GeneratePristine();

    private static RunEngine GeneratePristine()
    {
        var engine = new RunEngine();
        engine.Reset("ACT2TEST01");
        return engine;
    }

    /// <summary>A cloned run reseeded, without regenerating anything.</summary>
    private static RunState Seeded(string seed)
    {
        var engine = Pristine.Clone();
        engine.State.Rng = new Core.Rng.RunRngSet(seed);
        // The event stream is cached by name on the state, so a clone would answer with
        // the stream built for the seed it was cloned from.
        engine.State.EventRngStream = null;
        engine.State.EventRngName = null;
        return engine.State;
    }

    private static RunState Deck(params int[] cardIds)
    {
        var engine = Pristine.Clone();
        engine.State.Deck.Clear();
        foreach (int id in cardIds)
        {
            engine.State.Deck.Add(new CardInstance(id, Upgraded: false));
        }

        return engine.State;
    }

    private static RunState StartingDeck() => Pristine.Clone().State;

    private static int[] Options(RunState state, string ancient) =>
        RunNonCombatEffects.GenerateAncientOptions(state, ancient);

    // ---- shape shared by all three -------------------------------------------------

    [Theory]
    [InlineData(RunConstants.AncientOrobas)]
    [InlineData(RunConstants.AncientPael)]
    [InlineData(RunConstants.AncientTezcatara)]
    public void EveryAncientOffersThreeDistinctBlessings(string ancient)
    {
        var options = Options(StartingDeck(), ancient);

        Assert.Equal(3, options.Length);
        Assert.All(options, relic => Assert.NotEqual(0, relic));
        // One per pool, and no pool shares a relic with another.
        Assert.Equal(3, options.Distinct().Count());
    }

    /// <summary>
    /// The ancient's Rng is keyed on its own name, so two ancients on the same seed are
    /// reading different streams rather than the same one twice.
    /// </summary>
    [Fact]
    public void EachAncientReadsItsOwnStream()
    {
        var state = StartingDeck();

        Assert.NotEqual(
            Options(state, RunConstants.AncientPael),
            Options(state, RunConstants.AncientTezcatara)
        );
    }

    /// <summary>
    /// Generating twice must give the same three: an ancient's options are rolled once
    /// when the room is entered, and reading the action mask must not re-roll them. This
    /// is the invariant E2 was about.
    /// </summary>
    [Theory]
    [InlineData(RunConstants.AncientOrobas)]
    [InlineData(RunConstants.AncientPael)]
    [InlineData(RunConstants.AncientTezcatara)]
    public void GeneratingTwiceGivesTheSameOffer(string ancient)
    {
        Assert.Equal(Options(StartingDeck(), ancient), Options(StartingDeck(), ancient));
    }

    // ---- Orobas --------------------------------------------------------------------

    /// <summary>
    /// Orobas draws one from each pool, and its first pool gains either a Prismatic Gem
    /// or a Sea Glass — never both, and never neither.
    /// </summary>
    [Fact]
    public void OrobasOffersFromEachPoolInOrder()
    {
        var options = Options(StartingDeck(), RunConstants.AncientOrobas);

        var pool1 = RunConstants.OrobasPool1.ToArray().ToList();
        pool1.Add(RunConstants.RelicPrismaticGemOption);
        pool1.Add(RunConstants.RelicSeaGlass);
        Assert.Contains(options[0], pool1);
        Assert.Contains(options[1], RunConstants.OrobasPool2.ToArray());
        Assert.Contains(options[2], RunConstants.OrobasPool3.ToArray());
    }

    /// <summary>
    /// Its first pool is four long, not five: the Gem and the Sea Glass are alternatives
    /// decided by a <c>NextFloat() &lt; 1/3</c>, so exactly one of them can ever appear.
    /// </summary>
    [Fact]
    public void OrobasNeverOffersBothTheGemAndTheSeaGlass()
    {
        foreach (string seed in new[] { "ACT2TEST01", "3PFLW9XC5D", "7WGQ2VNJ4M", "AAB", "0" })
        {
            var options = Options(Seeded(seed), RunConstants.AncientOrobas);

            bool gem = options.Contains(RunConstants.RelicPrismaticGemOption);
            bool seaGlass = options.Contains(RunConstants.RelicSeaGlass);
            Assert.False(gem && seaGlass, $"{seed} offered both");
        }
    }

    /// <summary>
    /// Orobas spends two draws before its pools — a character for the Sea Glass and the
    /// gem coin-flip — so its offer must not match what the same stream would give an
    /// ancient that starts drawing immediately. This is what pins the draw ORDER; skipping
    /// the character pick would shift all three picks.
    /// </summary>
    [Fact]
    public void OrobasSpendsTwoDrawsBeforeItsPools()
    {
        var state = StartingDeck();
        var rng = RunNonCombatEffects.EventStream(state, RunConstants.AncientOrobas);
        int callsBefore = rng.CallCount;

        Options(state, RunConstants.AncientOrobas);

        // Two setup draws plus one per pool.
        Assert.Equal(callsBefore + 5, rng.CallCount);
    }

    // ---- Pael ----------------------------------------------------------------------

    [Fact]
    public void PaelOffersFromEachPoolInOrder()
    {
        var options = Options(StartingDeck(), RunConstants.AncientPael);

        Assert.Contains(options[0], RunConstants.PaelPool1.ToArray());
        Assert.Contains(
            options[1],
            new[]
            {
                RunConstants.PaelPool2[0],
                RunConstants.RelicPaelsClaw,
                RunConstants.RelicPaelsTooth,
                RunConstants.RelicPaelsGrowth,
            }
        );
        Assert.Contains(
            options[2],
            new[]
            {
                RunConstants.PaelPool3[0],
                RunConstants.PaelPool3[1],
                RunConstants.RelicPaelsLegion,
            }
        );
    }

    /// <summary>
    /// Pael's Claw needs three cards that can take an enchantment and Pael's Tooth five
    /// removable ones — a starting deck clears both, and a deck of nothing but Ascender's
    /// Bane clears neither. The thresholds change the POOL, so they change the draw.
    /// </summary>
    [Fact]
    public void PaelsConditionalEntriesDependOnTheDeck()
    {
        // Swept rather than compared on one seed: with the bare deck pool 2 is three
        // entries and with the starting deck it is seven, so a single draw can land on
        // Wing either way and prove nothing. What is being checked is MEMBERSHIP.
        var rich = EverySecondOption(StartingDeck());
        var bare = EverySecondOption(
            Deck(RunConstants.CardAscendersBane, RunConstants.CardAscendersBane)
        );

        Assert.Contains(RunConstants.RelicPaelsClaw, rich);
        Assert.Contains(RunConstants.RelicPaelsTooth, rich);
        Assert.DoesNotContain(RunConstants.RelicPaelsClaw, bare);
        Assert.DoesNotContain(RunConstants.RelicPaelsTooth, bare);
        // Wing and Growth survive either way — they are unconditional.
        Assert.Contains(RunConstants.PaelPool2[0], bare);
        Assert.Contains(RunConstants.RelicPaelsGrowth, bare);
    }

    /// <summary>Every relic Pael's SECOND pool can produce for a given deck.</summary>
    private static HashSet<int> EverySecondOption(RunState template)
    {
        var seen = new HashSet<int>();
        foreach (int i in Enumerable.Range(0, 120))
        {
            var state = Seeded($"PAELPOOL{i}");
            state.Deck.Clear();
            state.Deck.AddRange(template.Deck);
            seen.Add(Options(state, RunConstants.AncientPael)[1]);
        }

        return seen;
    }

    /// <summary>
    /// <c>list.AddRange(list)</c> doubles the pool before Growth is appended, which makes
    /// Growth half as likely as anything else. A starting deck's pool 2 is therefore Wing,
    /// Claw and Tooth twice each plus one Growth — seven entries, not four.
    /// </summary>
    [Fact]
    public void PaelsGrowthIsHalfAsLikelyAsTheRestOfItsPool()
    {
        var counts = new Dictionary<int, int>();
        foreach (int i in Enumerable.Range(0, 400))
        {
            int pick = Options(Seeded($"PAELSEED{i}"), RunConstants.AncientPael)[1];
            counts[pick] = counts.GetValueOrDefault(pick) + 1;
        }

        int growth = counts.GetValueOrDefault(RunConstants.RelicPaelsGrowth);
        int wing = counts.GetValueOrDefault(RunConstants.PaelPool2[0]);
        Assert.True(growth > 0, "Growth never came up at all");
        // A seventh against two sevenths. Loose bounds: this is checking the SHAPE of the
        // weighting, not the exact ratio.
        Assert.InRange(growth, wing / 4, wing);
    }

    // ---- Tezcatara -----------------------------------------------------------------

    [Fact]
    public void TezcataraOffersFromEachPoolInOrder()
    {
        var options = Options(StartingDeck(), RunConstants.AncientTezcatara);

        var pool1 = RunConstants.TezcataraPool1.ToArray().ToList();
        pool1.Add(RunConstants.RelicNutritiousSoup);
        Assert.Contains(options[0], pool1);
        Assert.Contains(options[1], RunConstants.TezcataraPool2.ToArray());
        Assert.Contains(options[2], RunConstants.TezcataraPool3.ToArray());
    }

    /// <summary>
    /// Nutritious Soup is in the first pool only while the deck holds a BASIC Strike. A
    /// run that has removed or transformed every Strike loses the option — and, because
    /// the pool shrinks, gets a different draw from the same stream.
    /// </summary>
    [Fact]
    public void TezcataraOffersSoupOnlyWithABasicStrike()
    {
        var withStrikes = StartingDeck();
        var withoutStrikes = Deck(30, 131, 131, 131);

        Assert.Contains(
            RunConstants.RelicNutritiousSoup,
            EveryPossibleFirstOption(RunConstants.AncientTezcatara, withStrikes)
        );
        Assert.DoesNotContain(
            RunConstants.RelicNutritiousSoup,
            Options(withoutStrikes, RunConstants.AncientTezcatara)
        );
    }

    /// <summary>Sweep seeds so the pool's membership is checked, not one draw from it.</summary>
    private static HashSet<int> EveryPossibleFirstOption(string ancient, RunState template)
    {
        var seen = new HashSet<int>();
        foreach (int i in Enumerable.Range(0, 60))
        {
            var state = Seeded($"TEZ{i}");
            state.Deck.Clear();
            state.Deck.AddRange(template.Deck);
            seen.Add(Options(state, ancient)[0]);
        }

        return seen;
    }
}
