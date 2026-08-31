using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/SpectrumShift.cs: CardsVar 1, and the upgrade is a
// discount. `SpectrumShiftPower.BeforeHandDraw` puts that many DISTINCT colourless cards
// into hand every turn, rolled on the card-generation stream.
public class SpectrumShiftTests
{
    private const int SpectrumShift = 450;

    private static Fight Played(bool upgraded = false)
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SpectrumShift, upgraded));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItGivesNoCardsOnTheTurnItIsPlayed()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.SpectrumShift));
        Assert.Empty(fight.State.Hand);
    }

    [Fact]
    public void EachTurnBringsAColourlessCard()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Played();
        fight.EndTurn();

        Assert.Equal(plain + 1, fight.State.Hand.Count);
        Assert.Contains(fight.State.Hand, c => GeneratedData.CardPools.Colorless.Contains(c.DefId));
    }

    /// <summary>
    /// Every turn, not once. Counted as colourless cards rather than hand size, because two
    /// turns of drawing runs into the ten-card cap and the cap would hide the second one.
    /// </summary>
    [Fact]
    public void ItKeepsGiving()
    {
        var fight = Played();

        fight.EndTurn();
        int afterOne = fight.State.Hand.Count(c =>
            GeneratedData.CardPools.Colorless.Contains(c.DefId)
        );

        fight.EndTurn();
        int afterTwo = fight.State.Hand.Count(c =>
            GeneratedData.CardPools.Colorless.Contains(c.DefId)
        );

        // One EACH turn -- last turn's left hand with the rest of it at the flush, so what
        // is in hand does not accumulate. Which pile it ended up in is not this card's
        // business: an empty draw pile reshuffles the discard back mid-turn.
        Assert.Equal(1, afterOne);
        Assert.Equal(1, afterTwo);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/SwordSage.cs: one stack, and every SOVEREIGN BLADE gains
// that many REPLAYS — applied to the blades already held and to any that enter combat
// afterwards, and taken back if the power is removed.
public class SwordSageTests
{
    private const int SwordSage = 487;
    private const int RefineBlade = 389; // Forge 9

    [Fact]
    public void ABladeAlreadyHeldGainsAReplay()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(RefineBlade, false));
        fight.Play(0);

        fight.State.Hand.Add(new CardInstance(SwordSage, false));
        fight.Play(fight.State.Hand.Count - 1);

        var blade = fight.State.Hand.First(c => c.DefId == RegentBoard.SovereignBlade);
        Assert.Equal(1, blade.ReplayCount);
    }

    /// <summary>And one forged afterwards arrives with it.</summary>
    [Fact]
    public void ABladeForgedAfterwardsHasItToo()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SwordSage, false));
        fight.Play(0);

        fight.State.Hand.Add(new CardInstance(RefineBlade, false));
        fight.Play(0);

        var blade = fight.State.Hand.First(c => c.DefId == RegentBoard.SovereignBlade);
        Assert.Equal(1, blade.ReplayCount);
    }

    /// <summary>The replay is real: the blade attacks twice.</summary>
    [Fact]
    public void TheBladePlaysTwice()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(SwordSage, false));
        fight.Play(0);
        fight.State.Hand.Add(new CardInstance(RefineBlade, false));
        fight.Play(0);
        int index = fight.State.Hand.FindIndex(c => c.DefId == RegentBoard.SovereignBlade);

        fight.Play(index, target: 0);

        // Ten forged to nineteen, twice.
        Assert.Equal(500 - 38, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Tyranny.cs: one stack, and the upgrade adds Innate.
// `TyrannyPower` draws one more every turn AND exhausts one card CHOSEN from hand at the
// start of it — the draw is what it pays and the exhaust is what it costs.
public class TyrannyTests
{
    private const int Tyranny = 520;

    private static Fight Played()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Tyranny, false));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAsksNothingOnTheTurnItIsPlayed()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Tyranny));
        Assert.Null(fight.Pending);
    }

    [Fact]
    public void NextTurnItDrawsOneMoreAndAsksWhichToExhaust()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.EndTurn();
        int plain = control.State.Hand.Count;

        var fight = Played();
        fight.EndTurn();

        Assert.Equal(plain + 1, fight.State.Hand.Count);
        Assert.Equal(CardSelectionKind.ExhaustFromHandRepeated, fight.Pending!.Kind);
    }

    [Fact]
    public void TheChosenCardIsExhausted()
    {
        var fight = Played();
        fight.EndTurn();
        int handBefore = fight.State.Hand.Count;

        fight.Choose(0);

        Assert.Equal(handBefore - 1, fight.State.Hand.Count);
        Assert.Single(fight.State.ExhaustPile);
        Assert.Null(fight.Pending);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Stardust.cs: the one card in the game with
// `HasStarCostX`. `WithHitCount(ResolveStarXValue())` at RANDOM opponents — 5/7 damage once
// per star SPENT.
//
// The stars are gone by the time its effect runs, because SpendResources takes them before
// OnPlay, so the emulator read a counter that was already zero and dealt nothing.
public class StardustTests
{
    private const int Stardust = 463;

    [Fact]
    public void ItHitsOncePerStarSpent()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 4;
        fight.State.Hand.Add(new CardInstance(Stardust, false));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 20, fight.Enemy0.Hp);
        Assert.Equal(0, fight.State.Stars);
    }

    [Fact]
    public void TheUpgradeHitsForSeven()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 3;
        fight.State.Hand.Add(new CardInstance(Stardust, true));

        fight.Play(0, target: 0);

        Assert.Equal(500 - 21, fight.Enemy0.Hp);
    }

    /// <summary>No stars is no damage, and the card is still playable.</summary>
    [Fact]
    public void AtZeroStarsItDealsNothing()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Stars = 0;
        fight.State.Hand.Add(new CardInstance(Stardust, false));

        fight.Play(0, target: 0);

        Assert.Equal(500, fight.Enemy0.Hp);
        Assert.Empty(fight.State.Hand);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Supermassive.cs: CalculationBase 5 plus ExtraDamage 3
// (upgrading by 1) for each card the player has GENERATED this combat —
// `CardGeneratedEntry.Creator == owner`, counted over the whole fight. It had been in a flat
// 25/35 body.
//
// Its live capture is parked: `debug_add_card` GENERATES the card it stages, so the game
// counted one generation no rebuilt fight can have. The staging tool is inside the thing
// this card measures.
public class SupermassiveTests
{
    private const int Supermassive = 481;
    private const int Dirge = 145; // makes one Soul per energy

    [Fact]
    public void WithNothingGeneratedItHitsForFive()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Supermassive, false));

        fight.Play(0, target: 0);

        Assert.Equal(495, fight.Enemy0.Hp);
    }

    [Fact]
    public void EachGeneratedCardAddsThree()
    {
        var fight = Fight.Hand().Energy(2).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Dirge, false));
        fight.Play(0);
        fight.State.Energy = 9;

        fight.State.Hand.Add(new CardInstance(Supermassive, false));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        // Two Souls generated, so 5 + 6.
        Assert.Equal(500 - 11, fight.Enemy0.Hp);
    }

    /// <summary>The count is per COMBAT, so it survives the turn.</summary>
    [Fact]
    public void TheCountSurvivesTheTurn()
    {
        var fight = Fight.Hand().Energy(2).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Dirge, false));
        fight.Play(0);
        fight.EndTurn();
        fight.State.Energy = 9;

        fight.State.Hand.Add(new CardInstance(Supermassive, false));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(500 - 11, fight.Enemy0.Hp);
    }

    [Fact]
    public void TheUpgradeAddsFourPerCard()
    {
        var fight = Fight.Hand().Energy(2).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Dirge, false));
        fight.Play(0);
        fight.State.Energy = 9;

        fight.State.Hand.Add(new CardInstance(Supermassive, true));
        fight.Play(fight.State.Hand.Count - 1, target: 0);

        Assert.Equal(500 - 13, fight.Enemy0.Hp);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/Terraforming.cs: `PowerVar<VigorPower>(6)` upgrading by 2
// — VIGOR, not Strength. Vigor is spent by the next attack; Strength is not, and the
// emulator granted 1/2 of it from a shared body.
public class TerraformingTests
{
    private const int Terraforming = 496;
    private const int StrikeRegent = 474;

    [Fact]
    public void ItGrantsSixVigorAndNoStrength()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Terraforming, false));

        fight.Play(0);

        Assert.Equal(6, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vigor));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TheUpgradeGrantsEight()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Terraforming, true));

        fight.Play(0);

        Assert.Equal(8, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Vigor));
    }

    /// <summary>Vigor is spent by the attack that uses it — which is the whole difference.</summary>
    [Fact]
    public void TheNextAttackSpendsIt()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(StrikeRegent, false));
        control.Play(0, target: 0);
        int plain = 500 - control.Enemy0.Hp;

        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(Terraforming, false));
        fight.Play(0);
        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        fight.Play(0, target: 0);
        int first = 500 - fight.Enemy0.Hp;

        fight.State.Hand.Add(new CardInstance(StrikeRegent, false));
        int before = fight.Enemy0.Hp;
        fight.Play(0, target: 0);

        Assert.Equal(plain + 6, first);
        Assert.Equal(plain, before - fight.Enemy0.Hp);
    }
}
