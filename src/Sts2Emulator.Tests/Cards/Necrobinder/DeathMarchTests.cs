using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/DeathMarch.cs: CalculationBase 8 (upgrading by 1) plus
// ExtraDamage 4 (upgrading by 2) for every `CardDrawnEntry` this turn with
// `!e.FromHandDraw` — the cards drawn ON TOP of the opening hand.
//
// The emulator counted `DrawnCardsSinceAutomationProc`, which is Automation's own counter
// and resets when Automation fires, and never upgraded the base. Nothing carried the
// "was this the hand draw" bit at all, which is also why Speedster fired on the opening
// five — see SpeedsterTests.
public class DeathMarchTests
{
    private const int DeathMarch = 125;
    private const int StrikeNecrobinder = 473;

    private static Fight Drawn(int extraDraws, bool upgraded = false)
    {
        var fight = Fight.Hand(new CardInstance(DeathMarch, upgraded)).Energy(9).Enemy(hp: 500);
        for (int i = 0; i < extraDraws; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeNecrobinder, false));
        }

        CardEffects.DrawCards(fight.State, extraDraws, new System.Random(0));
        return fight;
    }

    [Fact]
    public void WithNoExtraDrawsItIsTheBaseAlone()
    {
        var fight = Drawn(0);

        fight.Play();

        Assert.Equal(492, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachExtraDrawAddsFour()
    {
        var fight = Drawn(3);

        fight.Play();

        Assert.Equal(480, fight.Enemy0.Hp);
    }

    /// <summary>Upgrading raises BOTH terms: 9 base, 6 per card.</summary>
    [Fact]
    public void UpgradedItIsNinePlusSixEach()
    {
        var fight = Drawn(2, upgraded: true);

        fight.Play();

        Assert.Equal(479, fight.Enemy0.Hp);
    }

    /// <summary>The opening hand does not count — `FromHandDraw` is exactly that draw.</summary>
    [Fact]
    public void TheHandDrawDoesNotCount()
    {
        var fight = Fight.Hand(new CardInstance(DeathMarch, false)).Energy(9).Enemy(hp: 500);
        for (int i = 0; i < 5; i++)
        {
            fight.State.DrawPile.Add(new CardInstance(StrikeNecrobinder, false));
        }

        CardEffects.DrawCards(fight.State, 5, new System.Random(0), fromHandDraw: true);

        Assert.Equal(0, fight.State.ExtraCardsDrawnThisTurn);
    }

    /// <summary>It is this TURN's draws, so the counter resets across the turn boundary.</summary>
    [Fact]
    public void ItResetsEachTurn()
    {
        var fight = Drawn(3);

        fight.EndTurn();

        Assert.Equal(0, fight.State.ExtraCardsDrawnThisTurn);
    }
}
