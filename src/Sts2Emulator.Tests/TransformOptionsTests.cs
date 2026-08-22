using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What a card can be transformed into.
///
/// CardFactory.GetDefaultTransformationOptions draws from the original card's own pool,
/// keeps Common, Uncommon and Rare, and drops the original and the multiplayer-only
/// cards. One hand-written list used to stand for all of it, and it was wrong in both
/// directions at once -- two Ancients in, one Rare out.
/// </summary>
public class TransformOptionsTests
{
    [Fact]
    public void AnIroncladCardBecomesAnotherIroncladCard()
    {
        var options = RunRewardGenerator.TransformOptionsFor(IC.Bash);

        Assert.All(
            options,
            cardId => Assert.Contains(cardId, GeneratedData.CardPools.Ironclad.ToArray())
        );
    }

    [Fact]
    public void TheOriginalIsNotOneOfItsOwnOptions()
    {
        Assert.DoesNotContain(IC.Bash, RunRewardGenerator.TransformOptionsFor(IC.Bash));
    }

    [Fact]
    public void OnlyTheRaritiesARunIsHandedAreOffered()
    {
        var options = RunRewardGenerator.TransformOptionsFor(IC.Bash);

        Assert.All(
            options,
            cardId =>
                Assert.Contains(
                    GeneratedData.Cards.Get(cardId).Rarity,
                    (CardRarity[])[CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare]
                )
        );
    }

    [Fact]
    public void AncientCardsAreNotOffered()
    {
        // Break and Corruption are Ancient, and the old list carried both.
        var options = RunRewardGenerator.TransformOptionsFor(IC.Bash);
        int? corruption = GeneratedData.Cards.FindId("Corruption");

        Assert.NotNull(corruption);
        Assert.DoesNotContain(corruption!.Value, options);
    }

    [Fact]
    public void MultiplayerOnlyCardsAreNotOffered()
    {
        var options = RunRewardGenerator.TransformOptionsFor(IC.Bash);

        Assert.All(
            options,
            cardId => Assert.False(GeneratedData.Cards.Get(cardId).MultiplayerOnly)
        );
    }

    [Fact]
    public void TheWholeIroncladPoolIsReachable()
    {
        // 80: the Ironclad pool's Common, Uncommon and Rare cards, less Bash itself,
        // which is Basic and so was never in the count.
        Assert.Equal(80, RunRewardGenerator.TransformOptionsFor(IC.Bash).Length);
    }

    [Fact]
    public void ACurseBecomesAnotherCurse()
    {
        // A Curse keeps its own pool and skips the rarity filter, which is the only way
        // the options are non-empty at all -- no curse is Common, Uncommon or Rare.
        var options = RunRewardGenerator.TransformOptionsFor(IC.AscendersBane);

        Assert.NotEmpty(options);
        Assert.All(
            options,
            cardId => Assert.Contains(cardId, GeneratedData.CardPools.Curse.ToArray())
        );
        Assert.DoesNotContain(IC.AscendersBane, options);
    }
}
