using System.Reflection;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Forking a run for search. Two things have to hold: the copy must share nothing
/// with the original, and resampling must change only what the agent has not been
/// shown. See docs/agent-interface.md.
///
/// The clones are hand-written because the core is NativeAOT, which makes them
/// drift-prone -- a field added to a state class and not added to Clone is a copy
/// that silently shares it. The two guards here walk every field by reflection, so
/// they fail on the new field rather than on the bug it causes months later.
/// </summary>
public class StateCloningTests
{
    private static RunEngine RunInCombat(string seed = "QS2GYXRKWN")
    {
        var engine = new RunEngine();
        engine.Reset(seed);
        engine.StartCombat(
            RunConstants.StarterDeckIds,
            RunConstants.SlitheringStranglerEncounterId,
            [],
            playerHp: 64,
            playerMaxHp: 80,
            potionIds: [],
            playerGold: 99
        );
        return engine;
    }

    private static IEnumerable<FieldInfo> PublicFields(object instance) =>
        instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

    private static void AssertSharesNothing(object original, object copy)
    {
        foreach (var field in PublicFields(original))
        {
            object? left = field.GetValue(original);
            object? right = field.GetValue(copy);
            if (left is null || field.FieldType.IsValueType || left is string)
            {
                continue;
            }

            Assert.False(
                ReferenceEquals(left, right),
                $"{original.GetType().Name}.{field.Name} is shared with the clone; "
                    + "add it to StateCloning"
            );
        }
    }

    [Fact]
    public void CombatStateCloneSharesNoMutableField()
    {
        var engine = RunInCombat();

        AssertSharesNothing(engine.State.ActiveCombat!, engine.State.ActiveCombat!.Clone());
    }

    [Fact]
    public void RunStateCloneSharesNoMutableField()
    {
        var engine = RunInCombat();

        AssertSharesNothing(engine.State, engine.State.Clone());
    }

    [Fact]
    public void EveryScalarFieldSurvivesTheClone()
    {
        // Set each scalar to something that is not its default, so a field the clone
        // forgets comes back as the default and fails here.
        var engine = RunInCombat();
        var combat = engine.State.ActiveCombat!;
        int stamp = 1;
        foreach (var field in PublicFields(combat))
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValue(combat, stamp++);
            }
            else if (field.FieldType == typeof(bool))
            {
                field.SetValue(combat, true);
            }
        }

        var copy = combat.Clone();

        foreach (var field in PublicFields(combat))
        {
            if (field.FieldType == typeof(int) || field.FieldType == typeof(bool))
            {
                Assert.Equal(field.GetValue(combat), field.GetValue(copy));
            }
        }
    }

    [Fact]
    public void EveryCollectionFieldSurvivesTheClone()
    {
        // Identity alone cannot catch a forgotten list: the clone's object initializer
        // gives it a fresh empty one rather than the original's, so it looks unshared
        // while having quietly dropped its contents. Fill every collection first, then
        // insist the copy still has them.
        var engine = RunInCombat();
        var combat = engine.State.ActiveCombat!;
        foreach (var field in PublicFields(combat))
        {
            if (field.GetValue(combat) is System.Collections.IList { Count: 0 } list)
            {
                var elementType = field.FieldType.IsArray
                    ? field.FieldType.GetElementType()
                    : field.FieldType.GetGenericArguments().FirstOrDefault();
                if (elementType is null || field.FieldType.IsArray)
                {
                    continue;
                }

                try
                {
                    list.Add(Activator.CreateInstance(elementType));
                }
                catch (MissingMethodException)
                {
                    // No parameterless constructor; the other guards cover this field.
                }
            }
        }

        var copy = combat.Clone();

        foreach (var field in PublicFields(combat))
        {
            if (field.GetValue(combat) is System.Collections.IList original)
            {
                var copied = Assert.IsAssignableFrom<System.Collections.IList>(
                    field.GetValue(copy)
                );
                Assert.True(
                    original.Count == copied.Count,
                    $"CombatState.{field.Name} lost its contents in the clone "
                        + $"({original.Count} -> {copied.Count}); add it to StateCloning"
                );
            }
        }
    }

    [Fact]
    public void MutatingTheCopyLeavesTheOriginalAlone()
    {
        var engine = RunInCombat();
        var copy = engine.Clone();
        int deckBefore = engine.State.Deck.Count;
        int handBefore = engine.State.ActiveCombat!.Hand.Count;

        copy.State.Deck.Clear();
        copy.State.ActiveCombat!.Hand.Clear();
        copy.State.ActiveCombat.Enemies[0].Hp = 1;

        Assert.Equal(deckBefore, engine.State.Deck.Count);
        Assert.Equal(handBefore, engine.State.ActiveCombat.Hand.Count);
        Assert.NotEqual(1, engine.State.ActiveCombat.Enemies[0].Hp);
    }

    [Fact]
    public void AClonedStreamPicksUpWhereTheOriginalIs()
    {
        var engine = RunInCombat();
        var original = engine.State.ActiveCombat!.ShuffleRng!;
        original.Next(100);
        original.Next(100);

        var copy = engine.State.ActiveCombat.Clone().ShuffleRng!;

        Assert.Equal(original.CallCount, copy.CallCount);
        Assert.Equal(original.Next(1000), copy.Next(1000));
    }

    [Fact]
    public void AFaithfulForkPlaysOutExactlyLikeTheOriginal()
    {
        var engine = RunInCombat();

        var first = engine.Clone();
        var second = engine.Clone();
        for (int i = 0; i < 6; i++)
        {
            first.Step(0, 0, out _, out _, out _);
            second.Step(0, 0, out _, out _, out _);
        }

        Assert.Equal(first.State.ActiveCombat!.PlayerHp, second.State.ActiveCombat!.PlayerHp);
        Assert.Equal(
            first.State.ActiveCombat.DrawPile.Select(c => c.DefId),
            second.State.ActiveCombat.DrawPile.Select(c => c.DefId)
        );
    }

    [Fact]
    public void ResamplingLeavesEverythingTheAgentCanSeeAlone()
    {
        var engine = RunInCombat();
        var combat = engine.State.ActiveCombat!;

        var copy = engine.Clone(resampleSeed: 4242);
        var copyCombat = copy.State.ActiveCombat!;

        Assert.Equal(combat.PlayerHp, copyCombat.PlayerHp);
        Assert.Equal(combat.Hand.Select(c => c.DefId), copyCombat.Hand.Select(c => c.DefId));
        Assert.Equal(combat.DrawPile.Count, copyCombat.DrawPile.Count);
        Assert.Equal(engine.State.Gold, copy.State.Gold);
        Assert.Equal(engine.State.Deck.Count, copy.State.Deck.Count);
        // Composition is visible; order is not.
        Assert.Equal(
            combat.DrawPile.Select(c => c.DefId).OrderBy(id => id),
            copyCombat.DrawPile.Select(c => c.DefId).OrderBy(id => id)
        );
    }

    [Fact]
    public void ResamplingReordersTheUnknownDrawPile()
    {
        // A pile of distinct cards, so a reshuffle is visible as an order change.
        var engine = RunInCombat();
        var combat = engine.State.ActiveCombat!;
        combat.DrawPile.Clear();
        combat.ForgetDrawOrder();
        foreach (int cardId in (int[])[10, 20, 30, 40, 50, 60, 70, 80, 90, 100])
        {
            combat.DrawPile.Add(new CardInstance(cardId, false));
        }

        var copy = engine.Clone(resampleSeed: 7);

        Assert.NotEqual(
            combat.DrawPile.Select(c => c.DefId),
            copy.State.ActiveCombat!.DrawPile.Select(c => c.DefId)
        );
    }

    [Fact]
    public void ResamplingLeavesTheCardsThePlayerPlacedWhereTheyAre()
    {
        var engine = RunInCombat();
        var combat = engine.State.ActiveCombat!;
        combat.DrawPile.Clear();
        combat.ForgetDrawOrder();
        foreach (int cardId in (int[])[10, 20, 30, 40, 50, 60, 70, 80])
        {
            combat.DrawPile.Add(new CardInstance(cardId, false));
        }

        combat.TopDeck(new CardInstance(IC.Bash, false));
        combat.BottomDeck(new CardInstance(IC.Anger, false));

        var copy = engine.Clone(resampleSeed: 7).State.ActiveCombat!;

        Assert.Equal(IC.Bash, copy.DrawPile[0].DefId);
        Assert.Equal(IC.Anger, copy.DrawPile[^1].DefId);
        Assert.Equal(1, copy.KnownTopCount);
        Assert.Equal(1, copy.KnownBottomCount);
    }

    [Fact]
    public void ResamplingChangesWhatTheRunHasNotPaidOutYet()
    {
        var engine = RunInCombat();

        var first = engine.Clone(resampleSeed: 1);
        var second = engine.Clone(resampleSeed: 2);

        Assert.NotEqual(first.State.Rng.Seed, second.State.Rng.Seed);
        Assert.NotEqual(first.State.Rng.Seed, engine.State.Rng.Seed);
        // The streams still stand where the run left them, so the combat's sync-back
        // of call counts stays coherent.
        Assert.Equal(engine.State.Rng.Shuffle.CallCount, first.State.Rng.Shuffle.CallCount);
    }
}
