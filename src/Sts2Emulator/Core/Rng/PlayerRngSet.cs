namespace Sts2Emulator.Core.Rng;

public sealed class PlayerRngSet
{
    public uint Seed { get; }

    public GameRng Rewards { get; }
    public GameRng Shops { get; }
    public GameRng Transformations { get; }

    public PlayerRngSet(RunRngSet runRngSet, int netId = 1)
    {
        Seed = unchecked(runRngSet.Seed + (uint)netId);
        Rewards = new GameRng(Seed, "rewards");
        Shops = new GameRng(Seed, "shops");
        Transformations = new GameRng(Seed, "transformations");
    }
}
