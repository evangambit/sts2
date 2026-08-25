namespace Sts2Emulator.Core;

public static class BuffSystem
{
    public static void Apply(List<BuffState> buffs, BuffId id, int magnitude)
    {
        if (magnitude == 0)
        {
            return;
        }

        if (magnitude > 0 && IsDebuff(id))
        {
            if (TryConsumeArtifact(buffs))
            {
                return;
            }
        }

        int idx = buffs.FindIndex(b => b.Id == id);
        if (idx >= 0)
        {
            int newVal = buffs[idx].Magnitude + magnitude;
            if (newVal == 0)
            {
                buffs.RemoveAt(idx);
            }
            else
            {
                buffs[idx] = buffs[idx] with { Magnitude = newVal };
            }
        }
        else
        {
            buffs.Add(new BuffState(id, magnitude));
        }
    }

    public static int Get(List<BuffState> buffs, BuffId id)
    {
        int idx = buffs.FindIndex(b => b.Id == id);
        return idx >= 0 ? buffs[idx].Magnitude : 0;
    }

    public static bool Has(List<BuffState> buffs, BuffId id) => Get(buffs, id) > 0;

    public static void Remove(List<BuffState> buffs, BuffId id) => buffs.RemoveAll(b => b.Id == id);

    public static bool TryConsumeArtifact(List<BuffState> buffs)
    {
        int artifact = Get(buffs, BuffId.Artifact);
        if (artifact <= 0)
        {
            return false;
        }

        int artifactIdx = buffs.FindIndex(b => b.Id == BuffId.Artifact);
        if (artifact == 1)
        {
            buffs.RemoveAt(artifactIdx);
        }
        else
        {
            buffs[artifactIdx] = buffs[artifactIdx] with { Magnitude = artifact - 1 };
        }

        return true;
    }

    // Called at end of turn for the owning side (tick debuffs down by 1).
    /// <summary>The duration debuffs, as a snapshot to compare a later stack against.</summary>
    public static List<BuffState> DurationDebuffSnapshot(List<BuffState> buffs) =>
        [.. buffs.Where(buff => IsDurationDebuff(buff.Id))];

    private static bool IsDurationDebuff(BuffId id) =>
        id is BuffId.Vulnerable or BuffId.Weak or BuffId.Frail or BuffId.Shrink;

    /// <param name="atRoundStart">
    /// What the owner held when the round began, for the player's one-tick grace: a stack
    /// bigger now than it was then was applied during this round, and the game's
    /// SkipNextDurationTick means this tick passes it over. Omit for enemies, which get
    /// no grace.
    /// </param>
    public static void TickEndOfTurn(List<BuffState> buffs, List<BuffState>? atRoundStart = null)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var b = buffs[i];
            switch (b.Id)
            {
                // SlumberPower.AfterSideTurnEnd, which ticks for its OWNER's side only --
                // the beetle is an enemy, and this runs for the enemies. Its other
                // decrement, on unblocked damage, is in DealDamageToEnemy.
                case BuffId.Slumber:
                case BuffId.Vulnerable:
                case BuffId.Weak:
                case BuffId.Frail:
                case BuffId.Shrink:
                    if (b.Magnitude < 0)
                    {
                        break; // negative = permanent (e.g. ShrinkerBeetle Shrink)
                    }

                    if (atRoundStart != null && WasAppliedThisRound(b, atRoundStart))
                    {
                        break;
                    }

                    buffs[i] = b with { Magnitude = b.Magnitude - 1 };
                    if (buffs[i].Magnitude <= 0)
                    {
                        buffs.RemoveAt(i);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Whether this debuff is new to the player this round, and so owed the one-tick
    /// grace. It is the POWER that carries `SkipNextDurationTick`, not the application:
    /// `PowerCmd.Apply` sets the flag on the model it creates, so a debuff that lands on
    /// a stack the player already had leaves the existing power's flag alone and that
    /// power ticks as usual.
    ///
    /// Live, at A8, the Two-Tailed Rats screech nearly every turn and the player's Frail
    /// reads 1, 1, 0, 1 — each new point cancelled by the same round's tick. Treating any
    /// increase as a skip made it climb instead, which is a point of block a turn.
    /// </summary>
    private static bool WasAppliedThisRound(BuffState buff, List<BuffState> atRoundStart)
    {
        return !atRoundStart.Any(other => other.Id == buff.Id);
    }

    public static int IncomingDamage(
        int baseDamage,
        List<BuffState> attackerBuffs,
        List<BuffState> defenderBuffs
    )
    {
        float dmg = baseDamage;
        dmg += Get(attackerBuffs, BuffId.Strength);
        dmg += Get(attackerBuffs, BuffId.Vigor);
        if (Get(attackerBuffs, BuffId.Weak) > 0)
        {
            dmg *= 0.75f;
        }

        if (Get(attackerBuffs, BuffId.Shrink) != 0)
        {
            dmg *= 0.70f; // negative = permanent
        }

        if (Get(defenderBuffs, BuffId.Vulnerable) > 0)
        {
            float mult = 1.5f + Get(attackerBuffs, BuffId.CrueltyPower) / 100f;
            dmg *= mult;
        }

        // SurroundedPower.ModifyDamageMultiplicative, the Kaiser Crab's: an attack from
        // the half at the player's BACK lands at 1.5x. It belongs here rather than at the
        // point of damage because the game's readout shows the modified number --
        // AttackIntent.GetSingleDamage runs the move through Hook.ModifyDamage first --
        // and a live capture confirms it: the Crusher opens announcing 18 for a base 12.
        int facing = Get(defenderBuffs, BuffId.Surrounded);
        bool fromBehind =
            facing == Run.RunConstants.FacingRight
                ? Get(attackerBuffs, BuffId.BackAttackLeft) > 0
                : facing == Run.RunConstants.FacingLeft
                    && Get(attackerBuffs, BuffId.BackAttackRight) > 0;
        if (fromBehind)
        {
            dmg *= 1.5f;
        }

        return Math.Max(0, (int)dmg);
    }

    public static int IncomingBlock(int baseBlock, List<BuffState> buffs, bool isDefend = false)
    {
        float blk = baseBlock;
        if (Get(buffs, BuffId.NoBlock) > 0)
        {
            return 0;
        }

        blk += Get(buffs, BuffId.Dexterity);
        if (Get(buffs, BuffId.Frail) > 0)
        {
            blk *= 0.75f;
        }

        if (isDefend)
        {
            blk += Get(buffs, BuffId.FastenPower);
        }

        return Math.Max(0, (int)blk);
    }

    private static bool IsDebuff(BuffId id) =>
        id
            is BuffId.Vulnerable
                or BuffId.Weak
                or BuffId.Frail
                or BuffId.Poison
                or BuffId.Burn
                or BuffId.Shrink
                or BuffId.Tangled
                or BuffId.Constrict
                or BuffId.Smoggy
                or BuffId.NoBlock
                or BuffId.Doom;
}
