namespace Sts2Emulator.Core;

public enum CardType
{
    Attack,
    Skill,
    Power,
    Status,
    Curse,

    /// <summary>
    /// Quest cards (e.g. SpoilsMap). Appended rather than slotted into the game's
    /// own ordering: these ordinals are referenced across the engine and the
    /// observation encoding, so inserting would silently reinterpret existing data.
    /// </summary>
    Quest,
}

public enum CardRarity
{
    Basic,
    Common,
    Uncommon,
    Rare,
    Status,
    Curse,
    Special,
    Ancient,
    Event,
    Token,

    /// <summary>Quest rarity — appended; see CardType.Quest.</summary>
    Quest,
}

public readonly record struct CardDef(
    int Id,
    string Name,
    int Cost,
    int BaseDamage,
    int BaseBlock,
    int UpgradeDamage,
    int UpgradeBlock,
    int UpgradeCost,
    CardType Type,
    CardRarity Rarity,
    bool Ethereal = false,
    bool Exhaust = false,
    bool Unplayable = false,
    bool Retain = false,
    // Innate is declared two different ways in the game: in the card's
    // CanonicalKeywords (always innate), or added by OnUpgrade (innate only once
    // upgraded).  Keep them separate — see CardInstanceExtensions.IsInnate.
    bool Innate = false,
    bool InnateWhenUpgraded = false,
    // The game's CardModel.HasEnergyCostX. An X card is printed at cost 0 and spends
    // whatever is left on the bar, so the printed cost says nothing about what a play
    // actually cost — which is what CardPlay.Resources.EnergyValue reports to relics.
    bool HasEnergyCostX = false,
    // The game's CardModel.MultiplayerConstraint. CardFactory.FilterForPlayerCount drops
    // these from every pool in a solo run, so a single-player agent must never be offered
    // one — 21 cards, and the emulator's pools were built without the filter.
    bool MultiplayerOnly = false,
    // The game's ModelId.Entry — the slugified class name. The mid-combat reshuffle
    // sorts the pile by ModelId before Fisher-Yates (ListExtensions.StableShuffle), and
    // ModelId orders by Category then Entry as ordinal strings. Our own numeric ids sort
    // differently, and a different pre-shuffle order is a different shuffle from the
    // same stream — so the pile order has to come from this, not from Id.
    string Entry = ""
);

/// <summary>
/// The game's enchantments, as <c>EnchantmentModel</c> subclasses. A card carries at most
/// ONE -- <c>CardModel.Enchantment</c> is a single reference, and the base
/// <c>CanEnchant</c> refuses any card that already has one unless the enchantment is
/// stackable with itself -- so this is a slot, not a set of flags.
/// </summary>
public enum Enchantment
{
    None = 0,

    /// <summary>Attacks only; adds its amount to the card's damage.</summary>
    Sharp,

    /// <summary>Skills only; adds its amount to the card's block.</summary>
    Nimble,

    /// <summary>Powers only.</summary>
    Swift,

    /// <summary>Grants the Retain keyword.</summary>
    Steady,

    /// <summary>Basic Strikes and Defends only; the card plays one extra time.</summary>
    Spiral,

    /// <summary>Once per combat, playing the card also gains its amount of energy.</summary>
    Sown,

    /// <summary>Attacks only; 1.5x powered damage, and playing it costs 2 HP.</summary>
    Corrupted,

    /// <summary>Costs a fresh random amount (0..3) every time it is drawn to hand.</summary>
    Slither,
}

public readonly record struct CardInstance(
    int DefId,
    bool Upgraded,
    bool FreeThisTurn = false,
    bool Retain = false,
    // At most one enchantment, with the amount it was applied at. Self-Help Book grants
    // Sharp/Nimble/Swift at 2; the event enchantments are all applied at 1.
    Enchantment Enchantment = Enchantment.None,
    int EnchantAmount = 0,
    int CostForCombat = int.MinValue,
    // Damage this copy has permanently gained during the combat. Rampage raises its own
    // damage every time it is played, and the growth rides on the card rather than on the
    // player, so two Rampages in a deck grow independently.
    int BonusDamage = 0
);

public static class CardInstanceExtensions
{
    /// <summary>
    /// Whether this card counts as Innate right now, mirroring the game's
    /// <c>CardModel.Keywords.Contains(CardKeyword.Innate)</c> for the sources the
    /// emulator models (canonical keywords + the upgrade-granted keyword).
    /// Enchantment- and power-granted keywords are not modelled yet.
    /// </summary>
    public static bool IsInnate(this CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Innate || (card.Upgraded && def.InnateWhenUpgraded);
    }

    /// <summary>
    /// Whether this card should be sorted to the bottom of the draw pile at the
    /// start of combat (the game's <c>EnchantmentModel.ShouldStartAtBottomOfDrawPile</c>,
    /// true only for the <c>Imbued</c> enchantment).  Enchantments are not modelled
    /// yet, so this is always false — it exists so the turn-1 reorder is written
    /// against the real rule rather than silently omitting half of it.
    /// </summary>
    public static bool StartsAtBottomOfDrawPile(this CardInstance card) => false;

    /// <summary>The amount this card's enchantment was applied at, if it is that one.</summary>
    public static int EnchantedWith(this CardInstance card, Enchantment enchantment) =>
        card.Enchantment == enchantment ? card.EnchantAmount : 0;

    /// <summary>
    /// Whether this card is Retained, from any source the emulator models: the printed
    /// keyword, or the Steady enchantment, which adds it (<c>Steady.OnEnchant</c>).
    /// </summary>
    public static bool IsRetained(this CardInstance card) =>
        GeneratedData.Cards.Get(card.DefId).Retain || card.Enchantment == Enchantment.Steady;
}

public static class Enchantments
{
    /// <summary>
    /// Enchantments whose combat behaviour is NOT modelled yet. Applying one is recorded
    /// on the card and changes nothing when the card is played, so a run that takes the
    /// event gets the wrong combat.
    ///
    /// Kept as a list rather than a comment so a test can name the gap and so it shrinks
    /// visibly. Each needs a piece of plumbing that does not exist:
    /// <list type="bullet">
    /// <item>Swift -- what it does was never transcribed.</item>
    /// <item>Sown -- gains energy once per combat, which needs per-copy spent state.</item>
    /// <item>Corrupted -- 1.5x powered damage plus 2 HP on play, which needs a damage
    /// multiplier hook.</item>
    /// <item>Slither -- re-rolls its cost on every draw off the run's
    /// <c>combat_energy_costs</c> stream, which combat does not carry.</item>
    /// </list>
    /// </summary>
    public static readonly Enchantment[] InertInCombat =
    [
        Enchantment.Swift,
        Enchantment.Sown,
        Enchantment.Corrupted,
        Enchantment.Slither,
    ];

    /// <summary>
    /// The game's <c>EnchantmentModel.CanEnchant</c>: Status, Curse and Quest cards are
    /// never enchantable, an Unplayable card in the deck is not either, a card that
    /// already carries an enchantment is refused (none of the modelled ones stack with
    /// themselves), and each enchantment may narrow it further.
    /// </summary>
    public static bool CanEnchant(CardInstance card, Enchantment enchantment)
    {
        if (enchantment == Enchantment.None || card.Enchantment != Enchantment.None)
        {
            return false;
        }

        var def = GeneratedData.Cards.Get(card.DefId);
        if (def.Type is CardType.Status or CardType.Curse or CardType.Quest)
        {
            return false;
        }

        if (def.Unplayable)
        {
            return false;
        }

        return enchantment switch
        {
            // CanEnchantCardType overrides.
            Enchantment.Sharp or Enchantment.Corrupted => def.Type == CardType.Attack,
            Enchantment.Nimble => def.Type == CardType.Skill,
            Enchantment.Swift => def.Type == CardType.Power,

            // Spiral.CanEnchant: Basic rarity, and tagged Strike or Defend.
            Enchantment.Spiral => def.Rarity == CardRarity.Basic && IsStrikeOrDefend(def),

            // Slither.CanEnchant also refuses an X-cost card.
            Enchantment.Slither => !def.HasEnergyCostX,

            _ => true,
        };
    }

    /// <summary>
    /// The game's <c>CardTag.Strike</c> / <c>CardTag.Defend</c>. Tags are not extracted
    /// yet, and among Basic cards the tag and the name agree for every character, so the
    /// entry slug stands in. Extending this past Basic rarity would need the real tags --
    /// Perfected Strike and its kin are tagged Strike and are not Basic.
    /// </summary>
    private static bool IsStrikeOrDefend(CardDef def) =>
        def.Entry.StartsWith("STRIKE_", StringComparison.Ordinal)
        || def.Entry.StartsWith("DEFEND_", StringComparison.Ordinal);
}
