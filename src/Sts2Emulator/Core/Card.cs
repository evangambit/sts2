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

    /// <summary>
    /// The game's <c>CardType.None</c>, appended for the same reason Quest was: these
    /// ordinals are referenced across the engine and the observation encoding, so
    /// inserting would silently reinterpret existing data. Only a card that has not been
    /// through Tinker Time uses it, so nothing reads it as a real type.
    /// </summary>
    None,
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

/// <summary>
/// The game's <c>TargetType</c>: whether playing this card picks a creature.
/// </summary>
/// <remarks>
/// Only <see cref="AnyEnemy"/> performs target selection, which is what decides whether
/// a play carries a creature <c>Target</c> — and so whether the Kaiser Crab's
/// <c>SurroundedPower</c> turns the player toward it. An AllEnemies attack does not.
/// </remarks>
public enum CardTarget
{
    None,
    Self,
    AnyEnemy,
    AllEnemies,
    RandomEnemy,
    AnyPlayer,
    AnyAlly,
    AllAllies,
    TargetedNoCreature,
    Osty,
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
    CardTarget Target = CardTarget.Self,
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
    string Entry = "",
    // CardModel.CanBeGeneratedByModifiers: eight curses refuse to be handed out by
    // anything that rolls one, so a curse roll has to filter on it.
    bool CanBeGeneratedByModifiers = true,
    // `CardModel.CanBeGeneratedInCombat`. Fourteen cards refuse to be rolled by an
    // in-combat generator -- Feed and The Hunt among them -- and `FilterForCombat` drops
    // them alongside the Basic, Ancient and Event rarities.
    bool CanBeGeneratedInCombat = true,
    // `CardTag.OstyAttack`. Thirteen Necrobinder cards carry it, and Squeeze's damage
    // COUNTS them -- so it is a number the emulator has to be able to derive rather than
    // a label. Generated for the reason every other hand-kept list in here was deleted.
    bool OstyAttack = false,
    // CardModel.IsUpgradable is CurrentUpgradeLevel < MaxUpgradeLevel, and 38 cards
    // override MaxUpgradeLevel to zero -- every curse and status. This is that override,
    // read from the source rather than restated: the hand-kept list of ids it replaces
    // had 14 of the 38, and the dozen curses missing from it were silently eligible for
    // every upgrade in the game.
    bool Upgradable = true,
    // CardKeyword.Sly. A Sly card DISCARDED by an effect is auto-played --
    // `CardCmd.DiscardAndDraw` gathers the sly ones as it moves them and plays each
    // afterwards. The end-of-turn hand discard does not route through that command and so
    // does not trigger it.
    bool Sly = false,
    // Keywords an UPGRADE changes, which the flags above cannot express on their own. The
    // direction differs by keyword and is not interchangeable: twelve cards GAIN Retain
    // when upgraded, the way fifteen gain Innate, while nineteen LOSE Exhaust and three
    // lose Ethereal -- and for most of those losing it is the whole benefit of the
    // upgrade. See CardInstanceExtensions.IsRetained / IsExhaust / IsEthereal.
    bool RetainWhenUpgraded = false,
    bool ExhaustRemovedWhenUpgraded = false,
    bool EtherealRemovedWhenUpgraded = false,
    // CardKeyword.Eternal, which is `CardModel.IsRemovable => !Keywords.Contains(Eternal)`.
    // Seven curses carry it and `CardSelectCmd.FromDeckForRemoval` filters on it, so the
    // game will not so much as OFFER one for removal.
    bool Eternal = false
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

    /// <summary>Attacks only; adds its amount to the FIRST powered attack, then stops.</summary>
    Vigorous,

    // The four the act-2 ancients hand out. Appended, never inserted: the value is what
    // the observation carries.

    /// <summary>Skills only; starts at the bottom of the draw pile and auto-plays on turn 1.</summary>
    Imbued,

    /// <summary>Declares nothing of its own — an empty EnchantmentModel carrying an amount.</summary>
    Clone,

    /// <summary>Defend-tagged cards only; gains Exhaust.</summary>
    Goopy,

    /// <summary>Costs nothing, gains Eternal, and adds its amount to a powered attack.</summary>
    TezcatarasEmber,

    /// <summary>
    /// Blade of Ink's rider on the Shivs it makes: +1 damage on a powered attack, and Weak
    /// 1 on what the card hit. Both numbers are the enchantment's OWN vars — a
    /// <c>DamageVar(1)</c> and a <c>PowerVar&lt;WeakPower&gt;(1)</c> — rather than the
    /// amount it was applied at, which is why it declares <c>ShowAmount =&gt; false</c>.
    /// </summary>
    Inky,

    /// <summary>
    /// Kifuda's: block equal to its amount whenever the card is played, any card type.
    /// `RecalculateValues` sets a `BlockVar` from the amount, so the block IS the amount.
    /// </summary>
    Adroit,

    /// <summary>
    /// Punch Dagger's, Attacks only: every play adds its amount to a running bonus that
    /// the card then carries for the rest of the run.
    /// </summary>
    /// <remarks>
    /// The accumulation happens in `OnPlay` and the damage is read in
    /// `EnchantDamageAdditive`, which runs FIRST — so the play that adds the amount does
    /// not benefit from it. A freshly enchanted card hits for its printed damage the first
    /// time and grows from the second.
    /// </remarks>
    Momentum,

    /// <summary>
    /// Royal Stamp's, Attacks and Skills only: `OnEnchant` adds the Innate AND Retain
    /// keywords to the card, permanently. It has no play-time behaviour of its own.
    /// </summary>
    RoyallyApproved,
}

/// <summary>
/// Mad Science's rider, chosen on Tinker Time's third page.
/// </summary>
/// <remarks>
/// Declared in the game on <c>TinkerTime</c> itself. Three riders per card type, and the
/// event offers two of the three: Attack gets Sapping/Violence/Choking, Skill gets
/// Energized/Wisdom/Chaos, Power gets Expertise/Curious/Improvement. The ordering is the
/// enum's own, which is what the event's shuffle permutes.
/// </remarks>
public enum TinkerRider
{
    None = 0,
    Sapping,
    Violence,
    Choking,
    Energized,
    Wisdom,
    Chaos,
    Expertise,
    Curious,
    Improvement,
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
    // `EnergyCost.AddThisCombat(n)` on THIS card's model, which is per-card and not
    // player-wide: a Frantic Escape that has been played costs more, and its siblings do
    // not. Tracked here so the bump travels with the copy through the piles.
    int CostBump = 0,
    // Whether a once-per-combat enchantment on this copy has already fired. Sown, Swift
    // and Vigorous each set EnchantmentStatus.Disabled after they go off.
    //
    // Modelled as once per COMBAT because combat builds its own CardInstance list from the
    // run deck, so the flag never travels home. Nothing in the decompiled source resets
    // EnchantmentStatus, which would make it once per RUN instead -- but nothing was found
    // that copies the deck into the draw pile either, and the two readings only differ from
    // the second combat onwards. A capture of a Sown card played in two fights settles it.
    // Extra times this copy plays, from Hidden Gem's Replay. Per-copy, like BonusDamage:
    // the gem picks one card out of the draw pile and only that copy replays.
    int ReplayCount = 0,
    bool EnchantSpent = false,
    int CostForCombat = int.MinValue,
    // Damage this copy has permanently gained during the combat. Rampage raises its own
    // damage every time it is played, and the growth rides on the card rather than on the
    // player, so two Rampages in a deck grow independently.
    int BonusDamage = 0,
    // Mad Science is built at Tinker Time and nowhere else: its type and its rider are
    // chosen by the player and then SAVED ON THE CARD ([SavedProperty] on both), which
    // makes them part of the instance rather than of the definition. Default to None for
    // every other card.
    CardType TinkerType = CardType.None,
    TinkerRider TinkerRider = TinkerRider.None,
    // `CardModel.HasSingleTurnSly`, which Hand Trick sets on a chosen Skill. Sly is
    // otherwise a keyword on the DEFINITION, so this is the per-copy half of the same
    // question -- see CardInstanceExtensions.IsSlyThisTurn.
    bool SlyThisTurn = false,
    // `CardCmd.ApplyKeyword(card, CardKeyword.Sly)`, which Master Planner does to every
    // Skill its owner plays. Unlike Hand Trick's grant this one is permanent for the
    // combat, so it rides on the copy through the piles rather than expiring with the turn.
    bool SlyForCombat = false,
    // `CardModel.GiveSingleTurnRetain()`, which Well-Laid Plans' power hands to the cards
    // its owner picks just before the hand is flushed. Single-turn like Hand Trick's Sly:
    // it survives one flush and is cleared as the card lands in the next hand.
    bool RetainThisTurn = false,
    // `EnergyCost.SetUntilPlayed(0)`, which Rocket Punch does to ITSELF when its owner
    // generates a Status card. Unlike FreeThisTurn this survives the turn boundary and is
    // spent by the play rather than by the clock.
    bool FreeUntilPlayed = false,
    // Block this copy has permanently gained during the combat. Genetic Algorithm raises
    // its own block every time it is played, and the growth rides on the CARD -- the same
    // shape as Rampage's BonusDamage, and for the same reason: two copies grow apart.
    int BonusBlock = 0
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
        return def.Innate
            || (card.Upgraded && def.InnateWhenUpgraded)
            || card.Enchantment == Enchantment.RoyallyApproved;
    }

    /// <summary>
    /// Whether this card should be sorted to the bottom of the draw pile at the start of
    /// combat — the game's <c>EnchantmentModel.ShouldStartAtBottomOfDrawPile</c>, true
    /// only for <c>Imbued</c>.
    /// </summary>
    /// <remarks>
    /// This was a stub returning false, written against the real rule so the turn-1
    /// reorder would not silently omit half of it, and waiting for an enchantment that
    /// did not exist. Electric Shrymp brought Imbued with it.
    /// </remarks>
    public static bool StartsAtBottomOfDrawPile(this CardInstance card) =>
        card.Enchantment == Enchantment.Imbued;

    /// <summary>The amount this card's enchantment was applied at, if it is that one.</summary>
    public static int EnchantedWith(this CardInstance card, Enchantment enchantment) =>
        card.Enchantment == enchantment ? card.EnchantAmount : 0;

    /// <summary>
    /// Whether this card is Retained, from any source the emulator models: the printed
    /// keyword, or the Steady enchantment, which adds it (<c>Steady.OnEnchant</c>).
    /// </summary>
    public static bool IsRetained(this CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Retain
            || (card.Upgraded && def.RetainWhenUpgraded)
            || card.Enchantment == Enchantment.Steady
            // `RoyallyApproved.OnEnchant` adds Innate AND Retain -- both keywords, one
            // enchantment, which is why it reads in two places rather than one.
            || card.Enchantment == Enchantment.RoyallyApproved
            || card.RetainThisTurn;
    }

    /// <summary>
    /// Whether this copy exhausts when played. Nineteen cards DROP Exhaust when upgraded,
    /// and for most of them that is the whole benefit of the upgrade — an upgraded
    /// Discovery, Hologram or Secret Technique comes back.
    /// </summary>
    public static bool IsExhaust(this CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Exhaust && !(card.Upgraded && def.ExhaustRemovedWhenUpgraded);
    }

    /// <summary>
    /// Whether this copy is Ethereal. Apparition, Echo Form and Void Form all drop it when
    /// upgraded; the emulator knew about Echo Form only, by its raw id.
    /// </summary>
    public static bool IsEthereal(this CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Ethereal && !(card.Upgraded && def.EtherealRemovedWhenUpgraded);
    }

    /// <summary>
    /// `CardModel.IsSlyThisTurn`: the Sly keyword on the definition, or a single-turn
    /// grant from Hand Trick. A Sly card DISCARDED by an effect is played instead of
    /// joining the pile.
    /// </summary>
    public static bool IsSlyThisTurn(this CardInstance card) =>
        GeneratedData.Cards.Get(card.DefId).Sly || card.SlyForCombat || card.SlyThisTurn;
}

public static class Enchantments
{
    /// <summary>
    /// Enchantments whose combat behaviour is NOT modelled. Empty now: applying one used
    /// to be recorded on the card and change nothing when it was played, so a run that
    /// took Sapphire Seed, Wood Carvings or Self-Help Book got the wrong combat.
    ///
    /// Kept as a list rather than deleted so the next unmodelled one has an obvious place
    /// to be declared, and so the test that pins the gap keeps working.
    /// </summary>
    public static readonly Enchantment[] InertInCombat = [];

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
            Enchantment.Sharp or Enchantment.Corrupted or Enchantment.Vigorous => def.Type
                == CardType.Attack,
            Enchantment.Nimble => def.Type == CardType.Skill,
            Enchantment.Swift => def.Type == CardType.Power,

            // Spiral.CanEnchant: Basic rarity, and tagged Strike or Defend.
            Enchantment.Spiral => def.Rarity == CardRarity.Basic && IsStrikeOrDefend(def),

            // Slither.CanEnchant also refuses an X-cost card.
            Enchantment.Slither => !def.HasEnergyCostX,

            // Imbued.CanEnchantCardType: skills.
            Enchantment.Imbued => def.Type == CardType.Skill,

            // Momentum.CanEnchantCardType: attacks.
            Enchantment.Momentum => def.Type == CardType.Attack,

            // RoyallyApproved.CanEnchantCardType is `(uint)(cardType - 1) <= 1u`, which is
            // the GAME's Attack and Skill -- its CardType starts at 1 where this one starts
            // at 0, so the ordinals do not transfer and the names have to.
            Enchantment.RoyallyApproved => def.Type is CardType.Attack or CardType.Skill,

            // Goopy.CanEnchant: tagged Defend. The same stand-in as Spiral's -- among
            // Basic cards the tag and the name agree -- so this is right for the Defends
            // a run starts with and blind to any Defend-tagged card that is not Basic.
            Enchantment.Goopy => IsStrikeOrDefend(def)
                && def.Entry.StartsWith("DEFEND_", StringComparison.Ordinal),

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
