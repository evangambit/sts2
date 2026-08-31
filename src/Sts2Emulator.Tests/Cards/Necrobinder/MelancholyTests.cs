using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/Melancholy.cs: `GainsBlock`, BlockVar(13) upgrading by
// 4, at 3 energy. It had been sharing High Five's Osty-attack body, which gains nothing,
// and a live capture caught the missing thirteen block.
//
// `AfterDeath` does `EnergyCost.AddThisCombat(-Energy.IntValue)` on the CARD, so every
// creature death makes each copy in a COMBAT PILE one cheaper — including copies still in
// the draw pile, which is the point of the card.
public class MelancholyTests
{
    private const int Melancholy = 301;
    private const int Strike = 473;

    [Fact]
    public void ItGainsThirteenBlock()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Melancholy, false));

        fight.Play(0);

        Assert.Equal(13, fight.State.PlayerBlock);
    }

    [Fact]
    public void TheUpgradeGainsSeventeen()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Melancholy, true));

        fight.Play(0);

        Assert.Equal(17, fight.State.PlayerBlock);
    }

    [Fact]
    public void EachDeathMakesItOneCheaper()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 3);
        fight.State.Hand.Add(new CardInstance(Melancholy, false));
        Assert.Equal(3, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));

        fight.State.Hand.Add(new CardInstance(Strike, false));
        fight.Play(1, target: 0);

        Assert.Equal(2, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));
    }

    /// <summary>Every combat pile, because the hook's own test is `pile.IsCombatPile`.</summary>
    [Fact]
    public void ACopyInTheDrawPileGetsCheaperToo()
    {
        var fight = Fight.Hand().Energy(9).Draw(new CardInstance(Melancholy, false)).Enemy(hp: 3);
        fight.State.Hand.Add(new CardInstance(Strike, false));

        fight.Play(0, target: 0);

        var inDraw = fight.State.DrawPile.First(card => card.DefId == Melancholy);
        Assert.Equal(-1, inDraw.CostBump);
    }

    [Fact]
    public void NoDeathLeavesItAtItsPrintedCost()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Melancholy, false));
        fight.State.Hand.Add(new CardInstance(Strike, false));

        fight.Play(1, target: 0);

        Assert.Equal(3, CombatEngine.EffectiveCost(fight.State.Hand[0], fight.State));
    }
}
