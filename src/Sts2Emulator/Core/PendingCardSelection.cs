namespace Sts2Emulator.Core;

/// <summary>What the player is being asked to choose a card for.</summary>
public enum CardSelectionKind
{
    None = 0,

    /// <summary>Headbutt: a card from the discard pile goes on top of the draw pile.</summary>
    DiscardToDrawPileTop = 1,

    /// <summary>True Grit (upgraded): a card in hand is exhausted.</summary>
    ExhaustFromHand = 2,
}

/// <summary>
/// A choice the game would raise a card-selection screen for, paused mid-play until the
/// caller answers it.
///
/// The alternative — picking on the player's behalf — is what the emulator used to do,
/// and it silently made two cards weaker than they are: Headbutt always took the most
/// recently discarded card rather than the best one, and upgraded True Grit exhausted at
/// random rather than the card you would actually pick.
///
/// While this is open, <see cref="CombatEngine.ValidActions" /> offers the candidate
/// indices and nothing else, so an agent answers it as an ordinary step.
/// </summary>
public sealed class PendingCardSelection
{
    public required CardSelectionKind Kind { get; init; }

    /// <summary>Indices into the pile the choice is made from, in pile order.</summary>
    public required List<int> Candidates { get; init; }

    /// <summary>The card that asked, so an observation can say what the choice is for.</summary>
    public required int SourceCardDefId { get; init; }
}
