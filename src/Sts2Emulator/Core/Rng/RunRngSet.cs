namespace Sts2Emulator.Core.Rng;

public sealed class RunRngSet
{
    public uint Seed { get; }

    public GameRng UpFront { get; }
    public GameRng Shuffle { get; }
    public GameRng UnknownMapPoint { get; }
    public GameRng CombatCardGeneration { get; }
    public GameRng CombatPotionGeneration { get; }
    public GameRng CombatCardSelection { get; }
    public GameRng CombatEnergyCosts { get; }
    public GameRng CombatTargets { get; }
    public GameRng MonsterAi { get; }
    public GameRng Niche { get; }
    public GameRng CombatOrbs { get; }
    public GameRng TreasureRoomRelics { get; }

    /// <summary>The seed as the game stores it: canonicalized, not as typed.</summary>
    public string StringSeed { get; }

    public RunRngSet(string stringSeed)
    {
        StringSeed = SeedHelper.Canonicalize(stringSeed);
        Seed = unchecked((uint)DeterministicHash.GetDeterministicHashCode(StringSeed));
        UpFront = new GameRng(Seed, "up_front");
        Shuffle = new GameRng(Seed, "shuffle");
        UnknownMapPoint = new GameRng(Seed, "unknown_map_point");
        CombatCardGeneration = new GameRng(Seed, "combat_card_generation");
        CombatPotionGeneration = new GameRng(Seed, "combat_potion_generation");
        CombatCardSelection = new GameRng(Seed, "combat_card_selection");
        CombatEnergyCosts = new GameRng(Seed, "combat_energy_costs");
        CombatTargets = new GameRng(Seed, "combat_targets");
        MonsterAi = new GameRng(Seed, "monster_ai");
        Niche = new GameRng(Seed, "niche");
        CombatOrbs = new GameRng(Seed, "combat_orbs");
        TreasureRoomRelics = new GameRng(Seed, "treasure_room_relics");
    }

    public GameRng ActMapRng(int actIndex = 0) => new(Seed, $"act_{actIndex + 1}_map");

    public GameRng NeowRng(int netId = 1)
    {
        uint neowHash = unchecked((uint)DeterministicHash.GetDeterministicHashCode("NEOW"));
        uint seed = unchecked(Seed + (uint)netId + neowHash);
        return new GameRng(seed);
    }
}
