namespace Sts2Emulator.Core.Run;

public enum RunPhase
{
    Combat = 0,
    CardReward = 1,
    Map = 2,
    Rest = 3,
    Shop = 4,
    RelicReward = 5,
    Complete = 6,
    Event = 7,
    Ancient = 8,
    TransformSelect = 9,
    Treasure = 10,

    /// <summary>The Crystal Sphere's grid of fog, which is a screen of its own.</summary>
    CrystalSphere = 11,
}
