namespace Sts2Emulator.Core;

public enum PotionRarity
{
    None,
    Common,
    Uncommon,
    Rare,
    Event,
    Token,
}

public readonly record struct PotionDef(
    int Id,
    string Name,
    PotionRarity Rarity = PotionRarity.None
);
