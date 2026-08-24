using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Booming Conch (CardsVar(2) + EnergyVar(1), elite rooms only), Stone Cracker
/// (CardsVar(2) upgraded off the draw pile) and Venerable Tea Set (EnergyVar(2)), all
/// read off MegaCrit.Sts2.Core.Models.Relics.
/// </summary>
public class EliteAndUpgradeRelicTests
{
    [Fact]
    public void BoomingConchDoesNothingOutsideAnEliteFight()
    {
        var plain = Fight.WithRelics();
        var withConch = Fight.WithRelics(RelicEffects.BoomingConch);

        Assert.False(withConch.State.IsEliteCombat);
        Assert.Equal(plain.State.Energy, withConch.State.Energy);
        Assert.Equal(plain.State.Hand.Count, withConch.State.Hand.Count);
    }

    /// <summary>
    /// It fires in an elite room, and every act-1 elite has to read as one.
    /// </summary>
    /// <remarks>
    /// The elite test used to be a RANGE over the encounter enum, <c>BygoneEffigy</c> to
    /// <c>WaterfallGiant</c> — true when WaterfallGiant was the last name declared, and
    /// quietly false once Architect and SkulkingColony were appended after it. A live
    /// capture opened a Skulking Colony with seven cards and four energy where the
    /// emulator had five and three (`L9R346P3YD`).
    /// </remarks>
    [Fact]
    public void BoomingConchFiresInEveryActOneElite()
    {
        CombatFactory.ActOneEncounter[] elites =
        [
            CombatFactory.ActOneEncounter.BygoneEffigy,
            CombatFactory.ActOneEncounter.Byrdonis,
            CombatFactory.ActOneEncounter.PhrogParasite,
            CombatFactory.ActOneEncounter.PhantasmalGardeners,
            CombatFactory.ActOneEncounter.SkulkingColony,
            CombatFactory.ActOneEncounter.TerrorEel,
        ];

        foreach (var encounter in elites)
        {
            var plain = Fight.Encounter(encounter);
            var withConch = Fight.Encounter(
                encounter,
                Ascension.DefaultLevel,
                0,
                RelicEffects.BoomingConch
            );

            Assert.True(withConch.State.IsEliteCombat, $"{encounter} did not read as elite");
            Assert.Equal(plain.State.Energy + 1, withConch.State.Energy);
            Assert.Equal(plain.State.Hand.Count + 2, withConch.State.Hand.Count);
        }
    }

    /// <summary>
    /// A boss is not an elite. BoomingConch asks for <c>RoomType.Elite</c> and a boss room
    /// is <c>RoomType.Boss</c>; the old range swept every boss in with the elites.
    /// </summary>
    [Fact]
    public void BoomingConchDoesNothingInABossFight()
    {
        CombatFactory.ActOneEncounter[] bosses =
        [
            CombatFactory.ActOneEncounter.TheKin,
            CombatFactory.ActOneEncounter.WaterfallGiant,
            CombatFactory.ActOneEncounter.CeremonialBeast,
        ];

        foreach (var encounter in bosses)
        {
            var fight = Fight.Encounter(
                encounter,
                Ascension.DefaultLevel,
                0,
                RelicEffects.BoomingConch
            );

            Assert.False(fight.State.IsEliteCombat, $"{encounter} read as an elite");
        }
    }

    [Fact]
    public void StoneCrackerUpgradesTheFirstTwoUpgradableCardsInTheDrawPile()
    {
        var fight = Fight.WithRelics(RelicEffects.StoneCracker);

        // The starter deck's Ascender's Bane cannot be upgraded; two of the rest are.
        Assert.Equal(2, fight.State.DrawPile.Concat(fight.State.Hand).Count(card => card.Upgraded));
    }

    /// <summary>
    /// The relic takes <c>Cards.Where(IsUpgradable).Take(2)</c> — the first two upgradable
    /// cards in pile order, not two at random.
    ///
    /// Comparing two seeded fights would not prove this: a random pick off the same seed
    /// lands on the same cards both times and the test passes either way. Driving the
    /// effect over a pile whose order is known is what makes the rule observable.
    /// </summary>
    [Fact]
    public void StoneCrackerTakesTheFirstTwoUpgradableCardsInPileOrder()
    {
        var state = new CombatState { Relics = [new RelicInstance(RelicEffects.StoneCracker, 0)] };
        state.DrawPile =
        [
            Card(IC.StrikeIronclad),
            Card(IC.DefendIronclad),
            Card(IC.Bash),
            Card(IC.Anger),
        ];

        RelicEffects.ApplyBeforeOpeningHand(state, new Random(0));

        Assert.Equal([true, true, false, false], state.DrawPile.Select(card => card.Upgraded));
    }

    [Fact]
    public void VenerableTeaSetGivesTwoEnergyOnTheFirstTurn()
    {
        var plain = Fight.WithRelics();
        var withTeaSet = Fight.WithRelics(RelicEffects.VenerableTeaSetActive);

        Assert.Equal(plain.State.Energy + 2, withTeaSet.State.Energy);
    }

    [Fact]
    public void TheTeaSetDoesNotPayOutAgainOnLaterTurns()
    {
        var plain = Fight.WithRelics();
        var withTeaSet = Fight.WithRelics(RelicEffects.VenerableTeaSetActive);

        withTeaSet.EndTurn();
        plain.EndTurn();

        Assert.Equal(plain.State.Energy, withTeaSet.State.Energy);
    }
}
