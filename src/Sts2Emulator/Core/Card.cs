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
    bool InnateWhenUpgraded = false
);

public readonly record struct CardInstance(
    int DefId,
    bool Upgraded,
    bool FreeThisTurn = false,
    bool Retain = false,
    int Sharp = 0,
    int Nimble = 0,
    int Swift = 0,
    int CostForCombat = int.MinValue
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
}
