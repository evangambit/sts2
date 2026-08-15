namespace Sts2Emulator.Core.Effects;

using Run;

public static class RelicEffects
{
    public const int Anchor = 4;
    public const int BagOfMarbles = 9;
    public const int BagOfPreparation = 10;
    public const int BloodVial = 23;
    public const int BoomingConch = 29;
    public const int BronzeScales = 35;
    public const int CaptainsWheel = 41;
    public const int HappyFlower = 110;
    public const int HornCleat = 114;
    public const int Lantern = 128;
    public const int OddlySmoothStone = 169;
    public const int Orichalcum = 172;
    public const int RedSkull = 215;
    public const int StoneCracker = 249;
    public const int VenerableTeaSetActive = 100282;
    public const int Vajra = 279;

    public static void ApplyBeforeOpeningHand(CombatState state, Random rng)
    {
        if (!HasRelic(state, StoneCracker))
        {
            return;
        }

        var upgradableIndices = state
            .DrawPile.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .ToList();

        for (int i = 0; i < 2 && upgradableIndices.Count > 0; i++)
        {
            int selected = rng.Next(upgradableIndices.Count);
            int drawPileIndex = upgradableIndices[selected];
            state.DrawPile[drawPileIndex] = state.DrawPile[drawPileIndex] with { Upgraded = true };
            upgradableIndices.RemoveAt(selected);
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
            CardEffects.GainBlock(state, 10);
        }

        if (HasRelic(state, Vajra))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);
        }

        if (HasRelic(state, OddlySmoothStone))
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);
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

    public static void ApplyStartOfPlayerTurn(CombatState state)
    {
        int turnNumber = state.Turn + 1;

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

            if (HasRelic(state, BagOfMarbles))
            {
                foreach (var enemy in state.Enemies.Where(enemy => enemy.Hp > 0))
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Vulnerable, 1);
                }
            }
        }

        if (turnNumber == 2 && HasRelic(state, HornCleat))
        {
            CardEffects.GainBlock(state, 14);
        }

        if (turnNumber == 3 && HasRelic(state, CaptainsWheel))
        {
            CardEffects.GainBlock(state, 18);
        }

        int index = state.Relics.FindIndex(relic => relic.DefId == HappyFlower);
        if (index < 0)
        {
            return;
        }

        int turnsSeen = (state.Relics[index].Counter + 1) % 3;
        state.Relics[index] = state.Relics[index] with { Counter = turnsSeen };
        if (turnsSeen == 0)
        {
            state.Energy += 1;
        }
    }

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

    public static void ApplyEndOfPlayerTurn(CombatState state)
    {
        if (HasRelic(state, Orichalcum) && state.PlayerBlock == 0)
        {
            CardEffects.GainBlock(state, 6);
        }
    }

    private static bool HasRelic(CombatState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);
}
