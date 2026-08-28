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

    /// <summary>
    /// <c>CardSelectCmd.FromChooseABundleScreen</c>: Scroll Boxes' two bundles of three
    /// cards, one of which the player takes whole. It is answered in TWO actions the way
    /// the game's screen is — select a bundle, then confirm it.
    /// </summary>
    BundleSelect = 12,
}
