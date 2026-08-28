using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// 1-cost Power: `PowerVar&lt;AccuracyPower&gt;(4m)`, +2 on upgrade.
/// </summary>
/// <remarks>
/// `AccuracyPower.ModifyDamageAdditive` pays its amount to a powered attack from a card
/// tagged `CardTag.Shiv` dealt by its owner — so it raises Shivs and nothing else. That is
/// what `BuffId.ShivDamage` models, and this pins it: the emulator's arm was already
/// right, and it is here because the guard asks every implemented card for a suite and
/// because Accuracy is the third card keyed off the Shiv tag, beside Phantom Blades and
/// Fan of Knives.
/// </remarks>
public class AccuracyTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void ItRaisesEveryShiv(bool upgraded, int bonus)
    {
        var fight = Fight.Hand(Card(SI.Accuracy, upgraded), Card(SI.Shiv)).Energy(9).Enemy(hp: 60);

        fight.Play();
        Assert.Equal(bonus, fight.PlayerBuffAmount(BuffId.ShivDamage));

        fight.Play();

        Assert.Equal(60 - (4 + bonus), fight.Enemy0.Hp);
    }

    /// <summary>It is keyed to the Shiv TAG, so an ordinary attack is untouched.</summary>
    [Fact]
    public void AnOrdinaryAttackIsNotRaised()
    {
        var fight = Fight.Hand(Card(SI.Accuracy), Card(SI.StrikeSilent)).Energy(9).Enemy(hp: 60);

        fight.Play();
        fight.Play();

        Assert.Equal(60 - 6, fight.Enemy0.Hp);
    }

    /// <summary>`PowerStackType.Counter`: two Accuracies add up.</summary>
    [Fact]
    public void TwoCopiesStack()
    {
        var fight = Fight.Hand(Card(SI.Accuracy), Card(SI.Accuracy)).Energy(9);

        fight.Play();
        fight.Play();

        Assert.Equal(8, fight.PlayerBuffAmount(BuffId.ShivDamage));
    }
}
