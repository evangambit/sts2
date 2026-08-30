using Sts2Emulator.Core;

namespace Sts2Emulator.Tests;

/// <summary>
/// A <see cref="Fight" /> on the DEFECT's board: three orb slots of their own.
/// </summary>
/// <remarks>
/// `BaseOrbSlotCount` is a CharacterModel property and only <c>Defect.cs</c> overrides it,
/// to 3 — everyone else has none, and `OrbCmd.Channel` gives a slotless character ONE slot
/// the first time they channel. So an Ironclad handed Glacier channels two Frost into a
/// single slot and evokes the first; a Defect keeps both.
///
/// The emulator used to default every combat to three slots, which quietly made every
/// character a Defect. These tests are Defect card tests, so they say so here rather than
/// each restating the number.
/// </remarks>
internal static class DefectFight
{
    internal static Fight Hand(params CardInstance[] hand) => Board(Fight.Hand(hand));

    internal static Fight WithRelics(params int[] relicIds) => Board(Fight.WithRelics(relicIds));

    internal static Fight Encounter(
        CombatFactory.ActOneEncounter encounter,
        int ascension = Ascension.DefaultLevel,
        int seed = 0,
        params int[] relicIds
    ) => Board(Fight.Encounter(encounter, ascension, seed, relicIds));

    internal static Fight Encounter(int encounterId, params int[] relicIds) =>
        Board(Fight.Encounter(encounterId, relicIds));

    private static Fight Board(Fight fight)
    {
        fight.State.BaseOrbSlots = 3;
        fight.State.OrbCapacity = 3;
        return fight;
    }
}
