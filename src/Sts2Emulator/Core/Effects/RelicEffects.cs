namespace Sts2Emulator.Core.Effects;

using Run;

public static class RelicEffects
{
    public const int Akabeko = 1;
    public const int Anchor = 4;
    public const int BagOfMarbles = 9;
    public const int BagOfPreparation = 10;
    public const int BloodVial = 23;
    public const int BoomingConch = 29;
    public const int BronzeScales = 35;
    public const int CaptainsWheel = 41;
    public const int CentennialPuzzle = 43;
    public const int DataDisk = 56;
    public const int FestivePopper = 87;
    public const int Gorget = 107;
    public const int HappyFlower = 110;
    public const int HornCleat = 114;
    public const int Kunai = 126;
    public const int Lantern = 128;
    public const int LetterOpener = 136;
    public const int MummifiedHand = 158;
    public const int Nunchaku = 166;
    public const int OddlySmoothStone = 169;
    public const int Orichalcum = 172;
    public const int OrnamentalFan = 173;
    public const int Pendulum = 191;
    public const int Permafrost = 193;
    public const int RedMask = 214;
    public const int RedSkull = 215;
    public const int SelfFormingClay = 234;
    public const int Shuriken = 237;
    public const int StoneCracker = 249;
    public const int VenerableTeaSetActive = 100282;
    public const int Vajra = 279;

    public static void ApplyBeforeOpeningHand(CombatState state, Random rng)
    {
        if (!HasRelic(state, StoneCracker))
        {
            return;
        }

        // The relic takes Cards.Where(IsUpgradable).Take(2) off the draw pile — the first
        // two in pile order, not two at random, which is what this used to roll for.
        var upgradableIndices = state
            .DrawPile.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .Take(2)
            .ToList();

        foreach (int drawPileIndex in upgradableIndices)
        {
            state.DrawPile[drawPileIndex] = state.DrawPile[drawPileIndex] with { Upgraded = true };
        }
    }

    public static void ApplyCombatStart(CombatState state, Random rng)
    {
        if (HasRelic(state, BloodVial))
        {
            state.PlayerHp = Math.Min(state.PlayerHp + 2, state.PlayerMaxHp);
        }

        if (HasRelic(state, Anchor))
        {
            // BlockVar(10m, ValueProp.Unpowered) — Dexterity does not raise it.
            CardEffects.GainUnpoweredBlock(state, 10, rng);
        }

        if (HasRelic(state, Vajra))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
        }

        if (HasRelic(state, OddlySmoothStone))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
        }

        if (HasRelic(state, DataDisk))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Focus, 1);
        }

        if (HasRelic(state, Gorget))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, 4);
        }

        if (HasRelic(state, BronzeScales))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 3);
        }

        if (HasRelic(state, BagOfPreparation))
        {
            CardEffects.DrawCards(state, 2, rng);
        }

        if (HasRelic(state, BoomingConch) && state.IsEliteCombat)
        {
            CardEffects.DrawCards(state, 2, rng);
            state.Energy += 1;
        }
    }

    public static void ApplyStartOfPlayerTurn(CombatState state, Random? rng = null)
    {
        int turnNumber = state.Turn + 1;

        // Every "N cards played this turn" relic counts from zero again; the game resets
        // them in BeforeSideTurnStart, which is this same boundary.
        foreach (int relicId in PerTurnCounters)
        {
            SetCounter(state, relicId, 0);
        }

        if (turnNumber == 1)
        {
            if (HasRelic(state, Lantern))
            {
                state.Energy += 1;
            }

            if (HasRelic(state, VenerableTeaSetActive))
            {
                state.Energy += 2;
            }

            if (HasRelic(state, Akabeko))
            {
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vigor, 8);
            }

            if (HasRelic(state, BagOfMarbles))
            {
                foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Vulnerable, 1);
                }
            }

            if (HasRelic(state, RedMask))
            {
                foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Weak, 1);
                }
            }

            if (HasRelic(state, FestivePopper))
            {
                // DamageVar(9m, ValueProp.Unpowered) — Strength does not raise it.
                CardEffects.DealUnpoweredDamageToAll(state, 9);
            }
        }

        // Both are BlockVar(..., ValueProp.Unpowered): Dexterity does not raise them.
        if (turnNumber == 2 && HasRelic(state, HornCleat))
        {
            CardEffects.GainUnpoweredBlock(state, 14, rng);
        }

        if (turnNumber == 3 && HasRelic(state, CaptainsWheel))
        {
            CardEffects.GainUnpoweredBlock(state, 18, rng);
        }

        if (CountTowards(state, HappyFlower, period: 3))
        {
            state.Energy += 1;
        }

        if (CountTowards(state, Pendulum, period: 3))
        {
            CardEffects.DrawCards(state, 1, DrawRng(state, rng));
        }
    }

    /// <summary>
    /// The relics that pay out every Nth card of a type played in one turn. The game holds
    /// this on the relic (Kunai's AttacksPlayedThisTurn and friends), so the counter lives
    /// on the relic instance here too rather than on a shared per-turn tally.
    /// </summary>
    private static readonly int[] PerTurnCounters = [Kunai, Shuriken, OrnamentalFan, LetterOpener];

    public static void ApplyAfterCardPlayed(CombatState state, CardDef def, Random? rng = null)
    {
        switch (def.Type)
        {
            case CardType.Attack:
                if (CountTowards(state, Shuriken, period: 3))
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
                }

                if (CountTowards(state, Kunai, period: 3))
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
                }

                if (CountTowards(state, OrnamentalFan, period: 3))
                {
                    // BlockVar(4m, ValueProp.Unpowered).
                    CardEffects.GainUnpoweredBlock(state, 4, rng);
                }

                // Nunchaku counts attacks for the whole combat, not the turn, so its
                // counter is deliberately absent from PerTurnCounters.
                if (CountTowards(state, Nunchaku, period: 10))
                {
                    state.Energy += 1;
                }

                break;

            case CardType.Skill:
                if (CountTowards(state, LetterOpener, period: 3))
                {
                    // DamageVar(5m, ValueProp.Unpowered), to every hittable enemy.
                    CardEffects.DealUnpoweredDamageToAll(state, 5);
                }

                break;

            case CardType.Power:
                if (FiresOncePerCombat(state, Permafrost))
                {
                    // BlockVar(7m, ValueProp.Unpowered), on the first Power only.
                    CardEffects.GainUnpoweredBlock(state, 7, rng);
                }

                MakeRandomHandCardFree(state, rng);
                break;
        }
    }

    /// <summary>
    /// Fires when the player takes unblocked damage. The game hangs both of these off
    /// Hook.AfterDamageReceived, which runs per damage instance; here the caller is the
    /// enemy attack path, so self-damage from a card does not trigger them.
    /// </summary>
    public static void ApplyAfterUnblockedDamageReceived(CombatState state, Random? rng = null)
    {
        if (HasRelic(state, SelfFormingClay))
        {
            // SelfFormingClayPower: 3 unpowered block after the next block clear.
            BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, 3);
        }

        if (FiresOncePerCombat(state, CentennialPuzzle))
        {
            CardEffects.DrawCards(state, 3, DrawRng(state, rng));
        }
    }

    /// <summary>
    /// Mummified Hand: after a Power, a random card in hand costs nothing this turn. The
    /// game's preference order is cards with a printed cost that still cost something,
    /// then anything that still costs something, then anything printed-costed, then
    /// anything at all — so a hand of free cards still picks a card.
    /// </summary>
    private static void MakeRandomHandCardFree(CombatState state, Random? rng)
    {
        if (!HasRelic(state, MummifiedHand) || state.Hand.Count == 0)
        {
            return;
        }

        var all = Enumerable.Range(0, state.Hand.Count).ToList();
        var printedCosted = all.Where(i => PrintedCost(state.Hand[i]) > 0).ToList();
        var stillCosts = all.Where(i => CombatEngine.EffectiveCost(state.Hand[i], state) > 0)
            .ToList();

        var candidates = printedCosted.Intersect(stillCosts).ToList();
        if (candidates.Count == 0)
        {
            candidates = stillCosts;
        }

        if (candidates.Count == 0)
        {
            candidates = printedCosted;
        }

        if (candidates.Count == 0)
        {
            candidates = all;
        }

        var selectionRng = state.CardSelectionRng ?? rng;
        int handIndex = candidates[selectionRng?.Next(candidates.Count) ?? 0];
        state.Hand[handIndex] = state.Hand[handIndex] with { FreeThisTurn = true };
    }

    private static int PrintedCost(CardInstance card)
    {
        var def = GeneratedData.Cards.Get(card.DefId);
        int cost = card.CostForCombat == int.MinValue ? def.Cost : card.CostForCombat;
        return card.Upgraded ? cost + def.UpgradeCost : cost;
    }

    /// <summary>
    /// Advances a relic's counter and reports whether this was the Nth tick. Absent relic
    /// counts as "no" without touching the list.
    /// </summary>
    private static bool CountTowards(CombatState state, int relicId, int period)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index < 0)
        {
            return false;
        }

        int seen = (state.Relics[index].Counter + 1) % period;
        state.Relics[index] = state.Relics[index] with { Counter = seen };
        return seen == 0;
    }

    /// <summary>
    /// A once-per-combat relic, held as a counter because that is the only per-relic state
    /// there is. Combat setup builds fresh RelicInstances, so the flag clears itself.
    /// </summary>
    private static bool FiresOncePerCombat(CombatState state, int relicId)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index < 0 || state.Relics[index].Counter != 0)
        {
            return false;
        }

        state.Relics[index] = state.Relics[index] with { Counter = 1 };
        return true;
    }

    private static void SetCounter(CombatState state, int relicId, int counter)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == relicId);
        if (index >= 0)
        {
            state.Relics[index] = state.Relics[index] with { Counter = counter };
        }
    }

    /// <summary>
    /// The only randomness a relic-driven draw can need is a reshuffle, and that always
    /// reads the run's shuffle stream. The last fallback is only reachable on a state built
    /// without one.
    /// </summary>
    private static Random DrawRng(CombatState state, Random? rng) =>
        state.ShuffleRng ?? rng ?? new Random(0);

    public static void ApplyAfterPlayerHpChanged(CombatState state)
    {
        int index = state.Relics.FindIndex(relic => relic.DefId == RedSkull);
        if (index < 0)
        {
            return;
        }

        bool shouldBeActive = state.PlayerHp <= state.PlayerMaxHp / 2;
        bool isActive = state.Relics[index].Counter > 0;

        if (shouldBeActive == isActive)
        {
            return;
        }

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, shouldBeActive ? 3 : -3);
        state.Relics[index] = state.Relics[index] with { Counter = shouldBeActive ? 1 : 0 };
    }

    public static void ApplyEndOfPlayerTurn(CombatState state, Random? rng = null)
    {
        if (HasRelic(state, Orichalcum) && state.PlayerBlock == 0)
        {
            // BlockVar(6m, ValueProp.Unpowered) — flat, whatever the player's Dexterity.
            CardEffects.GainUnpoweredBlock(state, 6, rng);
        }
    }

    private static bool HasRelic(CombatState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);
}
