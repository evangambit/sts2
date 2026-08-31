using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// `BiiigHug.AfterShuffle`: a SOOT into the draw pile at a random position, on every
/// shuffle, for the whole run. Only its pickup half — remove four chosen cards — was
/// modelled, which made a hug that costs nothing into a hug that only pays.
/// </summary>
public class BiiigHugTests
{
    [Fact]
    public void EveryShuffleAddsASoot()
    {
        var fight = Fight.WithRelics(RelicEffects.BiiigHug).Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(473, false));

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));
        Assert.Equal(1, fight.State.DrawPile.Count(c => c.DefId == ST.Soot));

        fight.State.DiscardPile.AddRange(fight.State.DrawPile);
        fight.State.DrawPile.Clear();
        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.Equal(2, fight.State.DrawPile.Count(c => c.DefId == ST.Soot));
    }

    [Fact]
    public void WithoutItNothingIsAdded()
    {
        var fight = Fight.WithRelics().Energy(9).Enemy(hp: 200);
        fight.State.DrawPile.Clear();
        fight.State.DiscardPile.Add(new CardInstance(473, false));

        CardEffects.ShuffleDiscardIntoDraw(fight.State, new System.Random(0));

        Assert.DoesNotContain(fight.State.DrawPile, c => c.DefId == ST.Soot);
    }
}

/// <summary>
/// Booming Conch's two cards are a `ModifyHandDraw`, not a draw of their own — the third
/// relic carrying that mechanic, after Ring of the Snake and Bag of Preparation, and the
/// second to have been modelled as a separate `DrawCards` at combat start.
/// </summary>
/// <remarks>
/// Its energy IS a combat-start effect (`AfterSideTurnStart`) and stays there. Both halves
/// only pay in an ELITE room, and only on turn one.
/// </remarks>
public class BoomingConchTests
{
    // A real elite encounter, so `IsEliteCombat` is set by the factory BEFORE combat
    // start runs -- setting the flag afterwards is too late for either half of the conch.
    private static Fight Elite(params int[] relics) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.BygoneEffigy, relicIds: relics);

    private static Fight NotElite(params int[] relics) =>
        Fight.Encounter(CombatFactory.ActOneEncounter.DenseVegetation, relicIds: relics);

    [Fact]
    public void TheTwoCardsArePartOfTheHandDraw()
    {
        var plain = Elite();
        var conch = Elite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Hand.Count + 2, conch.State.Hand.Count);
        Assert.Equal(0, conch.State.ExtraCardsDrawnThisTurn);
    }

    [Fact]
    public void OutsideAnEliteItPaysNothing()
    {
        var plain = NotElite();
        var conch = NotElite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Hand.Count, conch.State.Hand.Count);
        Assert.Equal(plain.State.Energy, conch.State.Energy);
    }

    [Fact]
    public void TheEnergyIsStillACombatStartEffect()
    {
        var plain = Elite();
        var conch = Elite(RelicEffects.BoomingConch);

        Assert.Equal(plain.State.Energy + 1, conch.State.Energy);
    }
}
