// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.
using Sts2Emulator.Core;
namespace Sts2Emulator.GeneratedData;

internal static class Potions
{
    private static readonly PotionDef[] _all =
    [
        new PotionDef(Id: 1, Name: "Ashwater"),
        new PotionDef(Id: 2, Name: "AttackPotion"),
        new PotionDef(Id: 3, Name: "BeetleJuice"),
        new PotionDef(Id: 4, Name: "BlessingOfTheForge"),
        new PotionDef(Id: 5, Name: "BlockPotion"),
        new PotionDef(Id: 6, Name: "BloodPotion"),
        new PotionDef(Id: 7, Name: "BoneBrew"),
        new PotionDef(Id: 8, Name: "BottledPotential"),
        new PotionDef(Id: 9, Name: "Clarity"),
        new PotionDef(Id: 10, Name: "ColorlessPotion"),
        new PotionDef(Id: 11, Name: "CosmicConcoction"),
        new PotionDef(Id: 12, Name: "CunningPotion"),
        new PotionDef(Id: 13, Name: "CureAll"),
        new PotionDef(Id: 14, Name: "DexterityPotion"),
        new PotionDef(Id: 15, Name: "DistilledChaos"),
        new PotionDef(Id: 16, Name: "DropletOfPrecognition"),
        new PotionDef(Id: 17, Name: "Duplicator"),
        new PotionDef(Id: 18, Name: "EnergyPotion"),
        new PotionDef(Id: 19, Name: "EntropicBrew"),
        new PotionDef(Id: 20, Name: "EssenceOfDarkness"),
        new PotionDef(Id: 21, Name: "ExplosiveAmpoule"),
        new PotionDef(Id: 22, Name: "FairyInABottle"),
        new PotionDef(Id: 23, Name: "FirePotion"),
        new PotionDef(Id: 24, Name: "FlexPotion"),
        new PotionDef(Id: 25, Name: "FocusPotion"),
        new PotionDef(Id: 26, Name: "Fortifier"),
        new PotionDef(Id: 27, Name: "FoulPotion"),
        new PotionDef(Id: 28, Name: "FruitJuice"),
        new PotionDef(Id: 29, Name: "FyshOil"),
        new PotionDef(Id: 30, Name: "GamblersBrew"),
        new PotionDef(Id: 31, Name: "GhostInAJar"),
        new PotionDef(Id: 32, Name: "GigantificationPotion"),
        new PotionDef(Id: 33, Name: "GlowwaterPotion"),
        new PotionDef(Id: 34, Name: "HeartOfIron"),
        new PotionDef(Id: 35, Name: "KingsCourage"),
        new PotionDef(Id: 36, Name: "LiquidBronze"),
        new PotionDef(Id: 37, Name: "LiquidMemories"),
        new PotionDef(Id: 38, Name: "LuckyTonic"),
        new PotionDef(Id: 39, Name: "MazalethsGift"),
        new PotionDef(Id: 40, Name: "OrobicAcid"),
        new PotionDef(Id: 41, Name: "PoisonPotion"),
        new PotionDef(Id: 42, Name: "PotionOfBinding"),
        new PotionDef(Id: 43, Name: "PotionOfCapacity"),
        new PotionDef(Id: 44, Name: "PotionOfDoom"),
        new PotionDef(Id: 45, Name: "PotionShapedRock"),
        new PotionDef(Id: 46, Name: "PotOfGhouls"),
        new PotionDef(Id: 47, Name: "PowderedDemise"),
        new PotionDef(Id: 48, Name: "PowerPotion"),
        new PotionDef(Id: 49, Name: "RadiantTincture"),
        new PotionDef(Id: 50, Name: "RegenPotion"),
        new PotionDef(Id: 51, Name: "ShacklingPotion"),
        new PotionDef(Id: 52, Name: "ShipInABottle"),
        new PotionDef(Id: 53, Name: "SkillPotion"),
        new PotionDef(Id: 54, Name: "SneckoOil"),
        new PotionDef(Id: 55, Name: "SoldiersStew"),
        new PotionDef(Id: 56, Name: "SpeedPotion"),
        new PotionDef(Id: 57, Name: "StableSerum"),
        new PotionDef(Id: 58, Name: "StarPotion"),
        new PotionDef(Id: 59, Name: "StrengthPotion"),
        new PotionDef(Id: 60, Name: "SwiftPotion"),
        new PotionDef(Id: 61, Name: "TouchOfInsanity"),
        new PotionDef(Id: 62, Name: "VulnerablePotion"),
        new PotionDef(Id: 63, Name: "WeakPotion"),
    ];

    public static PotionDef Get(int id) =>
        Array.Find(_all, p => p.Id == id) is { Id: > 0 } def
            ? def
            : throw new ArgumentException($"Unknown potion id {id}");

    public static int? FindId(string name) =>
        Array.Find(_all, p => p.Name == name) is { Id: > 0 } def
            ? def.Id
            : null;
}
