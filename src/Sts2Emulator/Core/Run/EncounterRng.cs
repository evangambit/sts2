using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

/// <summary>
/// The per-encounter RNG stream: <c>EncounterModel.Rng</c>.
/// </summary>
/// <remarks>
/// <para>
/// Encounter models roll their own composition from a stream that is neither the combat
/// RNG nor any named run stream. <c>EncounterModel</c> builds it as
/// <c>new Rng((uint)((int)runState.Rng.Seed + runState.TotalFloor +
/// StringHelper.GetDeterministicHashCode(Id.Entry)))</c> — the run seed, the number of
/// map points visited so far, and a hash of the encounter's own id.
/// </para>
/// <para>
/// Two things ride on it, and both were wrong before this existed: which slimes spawn in
/// a Slimes encounter (<c>SlimesWeak.GenerateMonsters</c>), and which move each Corpse
/// Slug opens on (<c>CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves</c>). Neither is
/// derivable from the combat RNG, so a combat env that does not know the floor cannot
/// reproduce them.
/// </para>
/// </remarks>
public static class EncounterRng
{
    /// <summary>
    /// The game's <c>Id.Entry</c> for encounters whose generation consumes their own Rng.
    /// Anything absent here does not roll its composition and needs no seed.
    /// </summary>
    public static string? EntryId(int encounterId, bool weakVariant) =>
        encounterId switch
        {
            RunConstants.SlimesWeakEncounterId => "SLIMES_WEAK",
            RunConstants.SlimesNormalEncounterId => "SLIMES_NORMAL",
            // Both slug variants share one emulator enum id but not one entry id.
            RunConstants.CorpseSlugsEncounterId => weakVariant
                ? "CORPSE_SLUGS_WEAK"
                : "CORPSE_SLUGS_NORMAL",
            _ => null,
        };

    /// <summary>Seed for an encounter's own stream, or null when it does not use one.</summary>
    public static int? SeedFor(int runSeed, int totalFloor, int encounterId, bool weakVariant)
    {
        string? entry = EntryId(encounterId, weakVariant);
        if (entry is null)
        {
            return null;
        }

        return unchecked(
            (int)(
                (uint)runSeed
                + (uint)totalFloor
                + (uint)DeterministicHash.GetDeterministicHashCode(entry)
            )
        );
    }

    /// <summary>The stream itself: the game's unnamed <c>new Rng(seed)</c>.</summary>
    public static GameRng Stream(int seed) => new GameRng(unchecked((uint)seed));
}
