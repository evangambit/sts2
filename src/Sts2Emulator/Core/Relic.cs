namespace Sts2Emulator.Core;

public enum RelicRarity
{
    None,
    Starter,
    Common,
    Uncommon,
    Rare,
    Shop,
    Event,
    Ancient,
}

public readonly record struct RelicDef(
    int Id,
    string Name,
    // The game's ModelId.Entry -- the slugified class name. Anything that StableShuffles
    // relics sorts by ModelId first, and ModelId orders by Category then Entry as ordinal
    // strings, so the pre-shuffle order has to come from this rather than from our own
    // numeric ids. The relic trader's stock is the shuffle that needs it.
    string Entry = "",
    RelicRarity Rarity = RelicRarity.None,
    // RelicModel.HasUponPickupEffect: the relic did something on pickup that handing it
    // over could not undo.
    bool HasUponPickupEffect = false,
    bool SpawnsPets = false
)
{
    /// <summary>
    /// The game's <c>RelicModel.IsTradable</c>: what Ranwid the Elder will take and the
    /// relic trader will deal in. Starter, Event and Ancient relics are never tradable,
    /// nor is one whose pickup already paid out, nor one with a pet attached.
    /// </summary>
    /// <remarks>
    /// The game also excludes used-up and melted relics; those are run state rather than
    /// definition, so callers holding a <c>RelicInstance</c> check them separately.
    /// </remarks>
    public bool IsTradable =>
        !HasUponPickupEffect
        && !SpawnsPets
        && Rarity is not (RelicRarity.Starter or RelicRarity.Event or RelicRarity.Ancient);
}

public readonly record struct RelicInstance(int DefId, int Counter = 0);
