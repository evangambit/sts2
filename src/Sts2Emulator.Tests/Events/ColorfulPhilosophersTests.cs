using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Colorful Philosophers: three of the OTHER characters' card pools, and taking one opens
/// three card-reward screens over it -- a Common, an Uncommon and a Rare, three cards
/// each.
/// </summary>
/// <remarks>
/// What was here added a single random IRONCLAD card to the deck and upgraded it when the
/// player picked the third option. The player's own pool, no screen, no rarity split, and
/// an upgrade the event does not grant -- four things wrong in one line.
///
/// The option list is the subtle half. Four pools survive the `character.CardPool !=`
/// filter and the event cuts them to three by REMOVING at random, which is one draw off
/// its stream rather than three, and leaves the survivors in the fixed colour order rather
/// than a shuffled one. Take-three-at-random would give a different set from the same
/// seed.
/// </remarks>
[CoversEvent("ColorfulPhilosophers")]
public class ColorfulPhilosophersTests
{
    private static RunEngine At(string seed = "NXV45HW43K")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = RunConstants.EventColorfulPhilosophers;
        return engine;
    }

    [Fact]
    public void ItOffersThreeOptionsAndOnlyThree()
    {
        var engine = At();
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);

        Assert.Equal(1, mask[0]);
        Assert.Equal(1, mask[1]);
        Assert.Equal(1, mask[2]);
        Assert.Equal(0, mask[RunConstants.EventSkipAction + 1]);
    }

    /// <summary>
    /// Ironclad's own pool is filtered out before the cut, so no option can offer it.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NoOptionOffersThePlayersOwnPool(int option)
    {
        var engine = At();
        engine.Step(option, -1, out _, out _, out _);

        var ironclad = GeneratedData.CardPools.Ironclad.ToArray();
        var others = GeneratedData
            .CardPools.Necrobinder.ToArray()
            .Concat(GeneratedData.CardPools.Regent.ToArray())
            .Concat(GeneratedData.CardPools.Silent.ToArray())
            .Concat(GeneratedData.CardPools.Defect.ToArray())
            .ToHashSet();

        foreach (int card in engine.State.RewardCards.Where(id => id != 0))
        {
            Assert.Contains(card, others);
            Assert.DoesNotContain(card, ironclad);
        }
    }

    /// <summary>Every option's three screens draw from ONE pool, all the way down.</summary>
    [Fact]
    public void AllThreeScreensComeFromTheSamePool()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        var pools = new[]
        {
            GeneratedData.CardPools.Necrobinder.ToArray().ToHashSet(),
            GeneratedData.CardPools.Regent.ToArray().ToHashSet(),
            GeneratedData.CardPools.Silent.ToArray().ToHashSet(),
            GeneratedData.CardPools.Defect.ToArray().ToHashSet(),
        };
        var chosen = pools.Single(pool => pool.Contains(engine.State.RewardCards[0]));

        foreach (int card in AllScreens(engine))
        {
            Assert.Contains(card, chosen);
        }
    }

    /// <summary>
    /// Three screens, one per rarity, in Common / Uncommon / Rare order -- the order the
    /// rewards are constructed in, which is the order they are offered in.
    /// </summary>
    [Fact]
    public void TheThreeScreensAreCommonThenUncommonThenRare()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        CardRarity[] expected = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];
        var screens = Screens(engine);

        Assert.Equal(3, screens.Count);
        for (int i = 0; i < screens.Count; i++)
        {
            Assert.All(
                screens[i],
                card => Assert.Equal(expected[i], GeneratedData.Cards.Get(card).Rarity)
            );
        }
    }

    /// <summary>`CardsVar(3)`: three cards on each screen, all different.</summary>
    [Fact]
    public void EachScreenCarriesThreeDistinctCards()
    {
        var engine = At();
        engine.Step(0, -1, out _, out _, out _);

        foreach (var screen in Screens(engine))
        {
            Assert.Equal(3, screen.Count);
            Assert.Equal(3, screen.Distinct().Count());
        }
    }

    /// <summary>
    /// `Rng.NextInt` on the event's own stream picks which pool to drop, not the player's
    /// Rewards one -- so the option list is stable across a run that has been buying
    /// things.
    /// </summary>
    [Fact]
    public void TheOptionListDoesNotMoveWithTheRewardsStream()
    {
        var plain = At();
        plain.Step(0, -1, out _, out _, out _);

        var spent = At();
        spent.State.PlayerRng.Rewards.NextDouble();
        var mask = new int[RunConstants.MaxActions];
        spent.WriteActionMask(mask);

        Assert.Equal(1, mask[0]);
        Assert.Equal(1, mask[1]);
        Assert.Equal(1, mask[2]);
        Assert.Equal(0, mask[RunConstants.EventSkipAction + 1]);
    }

    /// <summary>
    /// `All(p => p.UnlockState.CharacterCardPools.Count() > 1)`: the profile the emulator
    /// runs has every pool, so the philosophers are never turned away for that reason.
    /// </summary>
    [Fact]
    public void TheUnlockGateIsOpenForTheEmulatorsProfile()
    {
        var engine = At();

        Assert.True(
            RunNonCombatEffects.IsEventAllowedForRun(
                engine.State,
                RunConstants.EventColorfulPhilosophers
            )
        );
    }

    /// <summary>
    /// `GenerateInitialOptions` runs once, when the event opens. Deriving the list again
    /// on every mask write would advance the event's stream on a READ and offer a
    /// different three than the ones on screen -- Tinker Time's bug, and the reason the
    /// survivors are memoised.
    /// </summary>
    [Fact]
    public void LookingAtTheOptionsDoesNotChangeThem()
    {
        var engine = At();
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        engine.WriteActionMask(mask);
        engine.WriteActionMask(mask);

        engine.Step(0, -1, out _, out _, out _);
        var afterLooking = engine.State.RewardCards.ToArray();

        var straight = At();
        straight.Step(0, -1, out _, out _, out _);

        Assert.Equal(straight.State.RewardCards, afterLooking);
    }

    private static System.Collections.Generic.List<System.Collections.Generic.List<int>> Screens(
        RunEngine engine
    )
    {
        var screens = new System.Collections.Generic.List<System.Collections.Generic.List<int>>
        {
            engine.State.RewardCards.Where(id => id != 0).ToList(),
        };
        while (engine.State.PendingCardOffers.Count > 0)
        {
            RunRewardGenerator.OfferFirstPendingCardOffer(engine.State);
            screens.Add(engine.State.RewardCards.Where(id => id != 0).ToList());
        }

        return screens;
    }

    private static System.Collections.Generic.IEnumerable<int> AllScreens(RunEngine engine) =>
        Screens(engine).SelectMany(screen => screen);
}
