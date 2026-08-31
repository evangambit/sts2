using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class InfernalBladeTests
{
    [Fact]
    public void AddsRandomAttackFreeThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
        Assert.True(state.Hand[0].FreeThisTurn);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.InfernalBlade);
    }

    [Fact]
    public void GeneratedAttackCanBePlayedWithoutEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 50,
                MaxHp = 50,
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(41, state.Enemies[0].Hp);
        // AshenStrike mentions Exhaust only in a hover tip; it declares no Exhaust
        // keyword and its OnPlay just deals damage, so it discards.
        Assert.Contains(
            state.DiscardPile,
            card => card.DefId == IC.AshenStrike && !card.FreeThisTurn
        );
    }

    [Fact]
    public void UpgradedCostsZero()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.InfernalBlade, true)];
        state.Energy = 0;

        Assert.Contains(0, CombatEngine.ValidActions(state));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.AshenStrike, state.Hand[0].DefId);
    }

    /// <summary>
    /// The pool is `CardFactory.FilterForCombat` over the character's own cards: Attacks
    /// that are `CanBeGeneratedInCombat` and are not Basic, Ancient or Event. A hand-kept
    /// copy of that answer had drifted in both directions at once, and the two errors
    /// cancelled in COUNT so the shuffle still consumed the right number of draws.
    /// </summary>
    [Fact]
    public void ThePoolExcludesAncientAndIncludesTheCommonItHadDropped()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 300; seed++)
        {
            var fight = Fight.Hand(new CardInstance(IC.InfernalBlade, false)).Energy(3);
            fight.State.CardGenerationRng = new CountingRandom(seed);
            fight.Play(0);
            foreach (var c in fight.State.Hand)
            {
                seen.Add(c.DefId);
            }
        }

        // Break is Ancient; the game never rolls it here.
        Assert.DoesNotContain(IC.Break, seen);
        // Iron Wave is Common and was absent from the hand-written list entirely.
        Assert.Contains(IC.IronWave, seen);
        // Feed declares CanBeGeneratedInCombat => false.
        Assert.DoesNotContain(IC.Feed, seen);
        // Strike is Basic.
        Assert.DoesNotContain(IC.StrikeIronclad, seen);
    }

    /// <summary>
    /// It rolls on `Rng.CombatCardGeneration`, not on whatever Random the play happened to
    /// carry — a card reading the wrong stream desynchronises every later draw.
    /// </summary>
    [Fact]
    public void ItRollsOnTheCardGenerationStream()
    {
        var fight = Fight.Hand(new CardInstance(IC.InfernalBlade, false)).Energy(3);
        var stream = new CountingRandom(7);
        fight.State.CardGenerationRng = stream;

        fight.Play(0);

        Assert.True(stream.CallCount > 0, "the generation stream should have been drawn from");
    }

    /// <summary>
    /// Stoke rolls over the whole character pool rather than just its Attacks, and its
    /// hand-written copy was the wrong SIZE — 83 where the game has 80 — so every roll was
    /// over the wrong range rather than occasionally landing on the wrong card.
    /// </summary>
    [Fact]
    public void StokesPoolExcludesWhatFilterForCombatDrops()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 400; seed++)
        {
            var fight = Fight
                .Hand(new CardInstance(IC.Stoke, false), new CardInstance(IC.Bludgeon, false))
                .Energy(3);
            fight.State.CardGenerationRng = new CountingRandom(seed);
            fight.Play(0);
            foreach (var c in fight.State.Hand)
            {
                seen.Add(c.DefId);
            }
        }

        Assert.NotEmpty(seen);
        // Ancient rarity, and two that declare CanBeGeneratedInCombat => false.
        Assert.DoesNotContain(IC.Break, seen);
        Assert.DoesNotContain(IC.Corruption, seen);
        Assert.DoesNotContain(IC.Feed, seen);
        Assert.DoesNotContain(IC.NotYet, seen);
    }
}
