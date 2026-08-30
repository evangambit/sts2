using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// The orb ring in the observation (O26).
/// </summary>
/// <remarks>
/// Defect's whole character is the ring and none of it was in the observation: not the
/// orbs, not their order, not Dark's banked total or Glass's remaining charge. An agent
/// cannot learn when to evoke something it cannot see, so every one of Defect's 88 cards
/// was being made correct for a policy that could not tell a full ring from an empty one.
///
/// The block is APPENDED after the selection candidates. Every offset above it is a
/// committed layout that fixtures are written against, and inserting a field would
/// renumber all of them — the same discipline `BuffId` and `Enchantment` carry.
/// </remarks>
public class OrbObservationTests
{
    private static int[] Observe(CombatState state)
    {
        var obs = new int[CombatObservation.ObsSize];
        CombatObservation.Write(state, obs);
        return obs;
    }

    /// <summary>The ring sits at the end, so nothing that was already there moved.</summary>
    [Fact]
    public void TheOrbBlockIsAppendedAfterEverythingElse()
    {
        Assert.Equal(
            CombatObservation.SelectionOffset
                + CombatObservation.MaxSelectionCandidates * CombatObservation.CardSlotSize,
            CombatObservation.OrbCapacityOffset
        );
        Assert.Equal(CombatObservation.OrbCapacityOffset + 1, CombatObservation.OrbOffset);
        Assert.Equal(
            CombatObservation.OrbOffset
                + CombatObservation.MaxOrbs * CombatObservation.OrbSlotSize,
            CombatObservation.ObsSize
        );
    }

    [Fact]
    public void CapacityIsReported()
    {
        var fight = DefectFight.Hand().Energy(3);

        Assert.Equal(3, Observe(fight.State)[CombatObservation.OrbCapacityOffset]);

        fight.State.OrbCapacity = 6;
        Assert.Equal(6, Observe(fight.State)[CombatObservation.OrbCapacityOffset]);
    }

    /// <summary>
    /// Type is written as `type + 1` so an empty slot reads 0 — Lightning is type 0, and a
    /// raw value could not be told from a slot nobody filled.
    /// </summary>
    [Fact]
    public void AnEmptySlotIsZeroAndALightningOrbIsNot()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 200);
        var empty = Observe(fight.State);
        Assert.Equal(0, empty[CombatObservation.OrbOffset]);

        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        var obs = Observe(fight.State);
        Assert.Equal((int)OrbType.Lightning + 1, obs[CombatObservation.OrbOffset]);
        // And the slot after it is still empty.
        Assert.Equal(0, obs[CombatObservation.OrbOffset + CombatObservation.OrbSlotSize]);
    }

    /// <summary>The ring is written in order, which is what makes "the next orb" legible.</summary>
    [Fact]
    public void TheRingIsWrittenInOrder()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);
        CardEffects.ChannelOrb(fight.State, OrbType.Plasma);

        var obs = Observe(fight.State);

        Assert.Equal(
            [(int)OrbType.Frost + 1, (int)OrbType.Dark + 1, (int)OrbType.Plasma + 1],
            Enumerable
                .Range(0, 3)
                .Select(i => obs[CombatObservation.OrbOffset + i * CombatObservation.OrbSlotSize])
        );
    }

    /// <summary>
    /// The two values are what the orb is WORTH — Focus already applied, as the intent
    /// field carries announced damage rather than the move's raw number.
    /// </summary>
    [Fact]
    public void TheValuesCarryFocus()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Lightning);

        var plain = Observe(fight.State);
        Assert.Equal(3, plain[CombatObservation.OrbOffset + 1]);
        Assert.Equal(8, plain[CombatObservation.OrbOffset + 2]);

        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 2);

        var focused = Observe(fight.State);
        Assert.Equal(5, focused[CombatObservation.OrbOffset + 1]);
        Assert.Equal(10, focused[CombatObservation.OrbOffset + 2]);
    }

    /// <summary>
    /// Plasma is the orb Focus does not touch, and the observation has to say so — a
    /// policy that inferred "Focus raises orb values" from the other four would be wrong
    /// about this one at both ends.
    /// </summary>
    [Fact]
    public void PlasmaIsUnmovedByFocus()
    {
        var fight = DefectFight.Hand().Energy(3);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Focus, 5);
        CardEffects.ChannelOrb(fight.State, OrbType.Plasma);

        var obs = Observe(fight.State);

        Assert.Equal(1, obs[CombatObservation.OrbOffset + 1]);
        Assert.Equal(2, obs[CombatObservation.OrbOffset + 2]);
    }

    /// <summary>
    /// Dark's banked total is the reason the block carries values at all: it appears
    /// nowhere else, and it is the whole question of whether to hold the orb or spend it.
    /// </summary>
    [Fact]
    public void DarksBankedTotalIsVisibleAndGrows()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 200);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Dark);
        Assert.Equal(6, Observe(fight.State)[CombatObservation.OrbOffset + 2]);

        fight.EndTurn();

        Assert.Equal(12, Observe(fight.State)[CombatObservation.OrbOffset + 2]);
    }

    /// <summary>And Glass's remaining charge, which runs the other way.</summary>
    [Fact]
    public void GlassRemainingChargeIsVisibleAndDecays()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 400);
        fight.State.PlayerHp = 999;
        CardEffects.ChannelOrb(fight.State, OrbType.Glass);

        var fresh = Observe(fight.State);
        Assert.Equal(4, fresh[CombatObservation.OrbOffset + 1]);
        Assert.Equal(8, fresh[CombatObservation.OrbOffset + 2]);

        fight.EndTurn();

        var worn = Observe(fight.State);
        Assert.Equal(3, worn[CombatObservation.OrbOffset + 1]);
        Assert.Equal(6, worn[CombatObservation.OrbOffset + 2]);
    }

    /// <summary>An evoked orb leaves its slot, so the ring shortens as it is spent.</summary>
    [Fact]
    public void EvokingClearsTheSlot()
    {
        var fight = DefectFight.Hand().Energy(3).Enemy(hp: 200);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);
        CardEffects.ChannelOrb(fight.State, OrbType.Frost);

        CardEffects.EvokeNextOrb(fight.State, new Random(0));

        var obs = Observe(fight.State);
        Assert.Equal((int)OrbType.Frost + 1, obs[CombatObservation.OrbOffset]);
        Assert.Equal(0, obs[CombatObservation.OrbOffset + CombatObservation.OrbSlotSize]);
    }
}
