namespace Sts2Emulator.Core;

/// <summary>What the player is being asked to choose a card for.</summary>
public enum CardSelectionKind
{
    None = 0,

    /// <summary>Headbutt: a card from the discard pile goes on top of the draw pile.</summary>
    DiscardToDrawPileTop = 1,

    /// <summary>True Grit (upgraded): a card in hand is exhausted.</summary>
    ExhaustFromHand = 2,

    /// <summary>Burning Pact: a card in hand is exhausted, and then cards are drawn.</summary>
    ExhaustFromHandThenDraw = 3,

    /// <summary>
    /// Secret Technique, Secret Weapon, Seeker Strike: a card from the draw pile goes to
    /// hand. The candidates are the draw-pile indices the card is allowed to offer, so a
    /// type filter lives in the candidate list rather than in the resolution.
    /// </summary>
    DrawPileToHand = 4,

    /// <summary>Thinking Ahead: a card in hand goes back on top of the draw pile.</summary>
    HandToDrawPileTop = 5,

    /// <summary>
    /// Purity: a card in hand is exhausted, and the screen reopens until
    /// <see cref="PendingCardSelection.Amount" /> picks are spent or the hand runs out.
    /// </summary>
    ExhaustFromHandRepeated = 6,

    /// <summary>
    /// Discovery: one of several freshly generated cards joins the hand, free for the
    /// turn. The candidates are not in any pile, so they are carried on the selection
    /// itself — see <see cref="PendingCardSelection.GeneratedCandidates" />.
    /// </summary>
    GeneratedCardToHand = 7,

    /// <summary>
    /// The Knowledge Demon's CURSE_OF_KNOWLEDGE: two curses offered, and the one chosen
    /// applies its power. The pair changes on each of its three casts, and Disintegration
    /// — always the first option — escalates 6, 7, 8.
    /// </summary>
    /// <remarks>
    /// The candidates are card models the game generates for the screen; nothing is added
    /// to a pile, so <see cref="PendingCardSelection.GeneratedCandidates" /> carries them
    /// exactly as Discovery's do. The emulator used to apply a flat Disintegration 6 with
    /// no choice at all — the same picking-for-the-player this type exists to stop.
    /// </remarks>
    CurseOfKnowledge = 8,

    /// <summary>
    /// Survivor, Acrobatics, Dagger Throw, Prepared and Hidden Daggers: a card in hand is
    /// DISCARDED, and the screen reopens until <see cref="PendingCardSelection.Amount" />
    /// picks are spent or the hand runs out — the same shape as Purity's exhaust.
    /// </summary>
    /// <remarks>
    /// All five used to discard the FIRST card in hand. That is not a small
    /// simplification: choosing what to throw away is the whole point of Survivor, and an
    /// agent told it discards the leftmost card learns a rule the game does not have.
    /// </remarks>
    DiscardFromHandRepeated = 9,

    /// <summary>
    /// Hand Trick: a Skill in hand that is not already Sly is made Sly for this turn, so
    /// discarding it later plays it. The filter lives in the candidate list, as Secret
    /// Weapon's does.
    /// </summary>
    MarkHandCardSly = 10,

    /// <summary>
    /// Nightmare: a card in hand is chosen, LEFT WHERE IT IS, and three copies of it join
    /// the hand at the start of the next turn.
    /// </summary>
    /// <remarks>
    /// The only selection so far that does not move the card it picks. Nightmare reads the
    /// choice into <c>NightmarePower</c> and the power does the work a turn later, so the
    /// pick is a question about the future rather than an edit to a pile.
    /// </remarks>
    QueueHandCardCopies = 11,

    /// <summary>
    /// Well-Laid Plans: cards in hand are chosen to survive the end-of-turn flush, up to
    /// <see cref="PendingCardSelection.Amount" /> of them, and the screen reopens until
    /// the picks are spent or nothing is left to offer.
    /// </summary>
    /// <remarks>
    /// The first selection raised OUTSIDE a card play — `WellLaidPlansPower` asks in
    /// `BeforeFlushLate`, every turn, for as long as the power stands. It is also the
    /// first that may be DECLINED: `CardSelectorPrefs(prompt, 0, Amount)` has a minimum of
    /// zero, so keeping nothing is a legal answer and the action space has to offer it.
    /// </remarks>
    RetainForNextTurn = 12,
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

    /// <summary>
    /// What the resolution does afterwards, for a choice that is not the last thing the
    /// card does — Burning Pact draws this many cards once the exhaust is chosen, and the
    /// draw has to happen after it so the newly drawn cards are not candidates.
    /// </summary>
    public int Amount { get; init; }

    /// <summary>
    /// Card def ids the choice is over, for a selection whose options do not exist in a
    /// pile yet. <see cref="Candidates" /> indexes into this rather than into the hand or
    /// the draw pile.
    /// </summary>
    public List<int> GeneratedCandidates { get; init; } = [];

    /// <summary>
    /// Cards that join the hand once the LAST pick is made — Hidden Daggers' Shivs, which
    /// the game creates after the discard and so must not be discard candidates
    /// themselves.
    /// </summary>
    public List<CardInstance> AfterSelectionToHand { get; init; } = [];

    /// <summary>
    /// Whether declining is a legal answer, for a screen whose `CardSelectorPrefs` has a
    /// minimum of zero. The skip is offered as the action one past the last candidate,
    /// which is free: while a selection is open only candidate indices are valid, so
    /// nothing else claims that index.
    /// </summary>
    public bool Skippable { get; init; }
}
