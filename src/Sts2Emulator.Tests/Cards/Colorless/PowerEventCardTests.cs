using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `HelloWorld` applies `HelloWorldPower(1)`, which adds that many DISTINCT COMMON cards
/// from the character's own pool to hand each turn. It was applying `InfiniteBlades` —
/// which makes SHIVS — at `upgraded ? 2 : 1`. Wrong power and wrong amount.
/// </summary>
public class HelloWorldTests
{
    private static int Id => GeneratedData.Cards.FindId("HelloWorld")!.Value;

    [Fact]
    public void ItAddsCommonCardsNotShivs()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        fight.Play(0);
        fight.State.Hand.Clear();

        fight.EndTurn();

        var added = fight.State.Hand.Where(card => card.DefId != 430).ToList();
        Assert.NotEmpty(added);
        Assert.DoesNotContain(fight.State.Hand, card => card.DefId == 430);
        Assert.Contains(
            fight.State.Hand,
            card => GeneratedData.Cards.Get(card.DefId).Rarity == CardRarity.Common
        );
    }

    /// <summary>
    /// ALWAYS one — the upgrade adds the INNATE keyword rather than a second stack.
    /// </summary>
    [Fact]
    public void TheUpgradeIsInnateNotASecondCard()
    {
        var plain = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy();
        var upgraded = Fight.Hand(new CardInstance(Id, true)).Energy(9).Enemy();

        plain.Play(0);
        upgraded.Play(0);

        Assert.Equal(
            BuffSystem.Get(plain.State.PlayerBuffs, BuffId.HelloWorld),
            BuffSystem.Get(upgraded.State.PlayerBuffs, BuffId.HelloWorld)
        );
        Assert.Equal(1, BuffSystem.Get(plain.State.PlayerBuffs, BuffId.HelloWorld));
        Assert.True(GeneratedData.Cards.Get(Id).InnateWhenUpgraded);
    }
}

/// <summary>
/// `FeedingFrenzy` applies `FeedingFrenzyPower`, which is a `TemporaryStrengthPower`, at
/// `PowerVar&lt;StrengthPower&gt;(5)` upgrading by 2. FIVE Strength, or seven — and it is
/// taken back at end of turn. It sat in a stack giving permanent Strength 1 or 2: wrong
/// number and wrong duration, in opposite directions.
/// </summary>
public class FeedingFrenzyTests
{
    private static int Id => GeneratedData.Cards.FindId("FeedingFrenzy")!.Value;

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void FiveStrengthOrSeven(bool upgraded, int strength)
    {
        var fight = Fight.Hand(new CardInstance(Id, upgraded)).Energy(9).Enemy();

        fight.Play(0);

        Assert.Equal(strength, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>And it is TEMPORARY — taken back at the end of the turn.</summary>
    [Fact]
    public void ItIsTakenBackAtEndOfTurn()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        fight.Play(0);
        Assert.Equal(5, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));

        fight.EndTurn();

        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }
}

/// <summary>
/// `ToricToughness` gains 5 block AND applies `ToricToughnessPower(2)` — for the next two
/// turns, the same block again when block clears. The power half was missing, which is
/// most of a 2-cost card.
/// </summary>
public class ToricToughnessTests
{
    private static int Id => GeneratedData.Cards.FindId("ToricToughness")!.Value;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(Id, upgraded)).Energy(9).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItBlocksNowAndForTheNextTwoTurns()
    {
        var fight = Played();
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(2, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ToricToughness));

        fight.EndTurn();
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ToricToughness));

        fight.EndTurn();
        Assert.Equal(5, fight.State.PlayerBlock);
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.ToricToughness));
    }

    /// <summary>Two turns, not three — the counter runs out.</summary>
    [Fact]
    public void TheThirdTurnGetsNothing()
    {
        var fight = Played();
        fight.EndTurn();
        fight.EndTurn();
        fight.EndTurn();

        Assert.Equal(0, fight.State.PlayerBlock);
    }

    /// <summary>
    /// `SetBlock` records what was actually GAINED, so Dexterity rides the first grant and
    /// the repeats match it rather than the printed five.
    /// </summary>
    [Fact]
    public void TheRepeatMatchesWhatWasGained()
    {
        var fight = Fight.Hand(new CardInstance(Id, false)).Energy(9).Enemy();
        fight.State.PlayerHp = 900;
        fight.State.PlayerMaxHp = 900;
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Dexterity, 3);
        fight.Play(0);

        int first = fight.State.PlayerBlock;
        fight.EndTurn();

        Assert.Equal(8, first);
        Assert.Equal(first, fight.State.PlayerBlock);
    }
}
