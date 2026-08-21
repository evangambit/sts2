namespace Sts2Emulator.Core.Rng;

public sealed class PlayerRngSet
{
    public uint Seed { get; }

    public GameRng Rewards { get; }
    public GameRng Shops { get; }
    public GameRng Transformations { get; }

    /// <param name="netId">
    /// The owner's player slot. Player.cs seeds this set with
    /// <c>hash(seed) + RunState.GetPlayerSlotIndex(this)</c>, and a solo run's only
    /// player is slot 0 — the same off-by-one that had Neow offering the wrong relics.
    /// Every reward, shop and transformation in the run comes off these streams.
    /// </param>
    public PlayerRngSet(RunRngSet runRngSet, int netId = 0)
    {
        Seed = unchecked(runRngSet.Seed + (uint)netId);
        Rewards = new GameRng(Seed, "rewards");
        Shops = new GameRng(Seed, "shops");
        Transformations = new GameRng(Seed, "transformations");
    }
}
