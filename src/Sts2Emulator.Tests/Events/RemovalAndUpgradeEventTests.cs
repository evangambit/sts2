using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Two events that were choosing for the player, and paying the wrong price for it.
/// </summary>
/// <remarks>
/// Neither is covered by a capture — both were read straight out of the decompiled
/// event models after `RemoveLowestPriorityCard` was found still standing at four call
/// sites (catalogue E44's closing note). What the reading turned up was worse than the
/// removal: on both events, BOTH options were somebody else's.
/// </remarks>
[CoversEvent("FieldOfManSizedHoles")]
[CoversEvent("SpiritGrafter")]
public class RemovalAndUpgradeEventTests
{
    private static RunEngine At(int eventId)
    {
        var engine = new RunEngine();
        engine.Reset("NXV45HW43K");
        engine.State.Phase = RunPhase.Event;
        engine.State.EventId = eventId;
        return engine;
    }

    private static int Choose(RunEngine engine, int option) =>
        engine.Step(option, -1, out _, out _, out _);

    /// <summary>
    /// FieldOfManSizedHoles.Resist: FromDeckForRemoval at CardsVar(2), then
    /// AddCursesToDeck(Normality). Two cards the player picks, and a curse for them.
    /// </summary>
    [Fact]
    public void FieldOfManSizedHolesResistRemovesTwoChosenCardsAndAddsNormality()
    {
        var engine = At(RunConstants.EventFieldOfManSizedHoles);
        int before = engine.State.Deck.Count;

        Choose(engine, 0);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        // Nothing has gone yet — the screen is open, and the curse comes after it.
        Assert.Equal(before, engine.State.Deck.Count);

        Choose(engine, 0);
        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        Choose(engine, 0);

        // Two out, one curse in.
        Assert.Equal(before - 1, engine.State.Deck.Count);
        Assert.Contains(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.NamedCard("Normality")
        );
    }

    /// <summary>
    /// SpiritGrafter.LetItIn: HealVar(25) and a Metamorphosis into the deck. It used to
    /// remove a card and grant 3 max HP, which is neither half of it.
    /// </summary>
    [Fact]
    public void SpiritGrafterLetItInHealsAndAddsMetamorphosis()
    {
        var engine = At(RunConstants.EventSpiritGrafter);
        engine.State.PlayerHp = 30;
        int maxHp = engine.State.PlayerMaxHp;
        int before = engine.State.Deck.Count;

        Choose(engine, 0);

        Assert.Equal(System.Math.Min(maxHp, 55), engine.State.PlayerHp);
        Assert.Equal(maxHp, engine.State.PlayerMaxHp);
        Assert.Equal(before + 1, engine.State.Deck.Count);
        Assert.Contains(
            engine.State.Deck,
            card => card.DefId == RunNonCombatEffects.NamedCard("Metamorphosis")
        );
    }

    /// <summary>
    /// SpiritGrafter.Rejection: FromDeckForUpgrade, and the HpLossVar(10) lands AFTER the
    /// card is chosen — the same ordering Precarious Shears got wrong (E44).
    /// </summary>
    [Fact]
    public void SpiritGrafterRejectionUpgradesAChosenCardThenCharacterTakesTen()
    {
        var engine = At(RunConstants.EventSpiritGrafter);
        engine.State.PlayerHp = 60;

        Choose(engine, 1);

        Assert.Equal(RunPhase.TransformSelect, engine.State.Phase);
        // The damage has not landed yet: a capture taken at the selector shows it unpaid.
        Assert.Equal(60, engine.State.PlayerHp);

        Choose(engine, 0);

        Assert.Equal(50, engine.State.PlayerHp);
        Assert.Contains(engine.State.Deck, card => card.Upgraded);
    }
}
