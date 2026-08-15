namespace Sts2Emulator.Core;

public static class CombatObservation
{
    public const int ObsSize = 164;
    public const int MaxHand = 10;
    public const int MaxEnemies = 6;
    public const int MaxPlayerBuffs = 10;
    public const int MaxEnemyBuffs = 5;

    public static void Write(CombatState s, Span<int> obs)
    {
        if (obs.Length < ObsSize)
        {
            throw new ArgumentException("Combat observation buffer is too small.", nameof(obs));
        }

        obs[..ObsSize].Clear();
        obs[0] = s.PlayerHp;
        obs[1] = s.PlayerMaxHp;
        obs[2] = s.PlayerBlock;
        obs[3] = s.Energy;
        obs[4] = s.MaxEnergy;
        obs[5] = s.DrawPile.Count;
        obs[6] = s.DiscardPile.Count;
        obs[7] = s.ExhaustPile.Count;

        int offset = 8;
        for (int i = 0; i < MaxHand; i++)
        {
            if (i < s.Hand.Count)
            {
                obs[offset + i * 2] = s.Hand[i].DefId;
                obs[offset + i * 2 + 1] = s.Hand[i].Upgraded ? 1 : 0;
            }
        }

        offset = 8 + MaxHand * 2;
        for (int i = 0; i < 3; i++)
        {
            obs[offset + i * 2] = s.PotionSlots[i];
            obs[offset + i * 2 + 1] = s.PotionSlots[i] != 0 ? 1 : 0;
        }

        offset = 8 + MaxHand * 2 + 6;
        for (int i = 0; i < MaxPlayerBuffs; i++)
        {
            if (i < s.PlayerBuffs.Count)
            {
                obs[offset + i * 2] = (int)s.PlayerBuffs[i].Id;
                obs[offset + i * 2 + 1] = s.PlayerBuffs[i].Magnitude;
            }
        }

        offset = 8 + MaxHand * 2 + 6 + MaxPlayerBuffs * 2;
        int enemySlotSize = 5 + MaxEnemyBuffs * 2;
        for (int enemyIndex = 0; enemyIndex < MaxEnemies; enemyIndex++)
        {
            int baseIndex = offset + enemyIndex * enemySlotSize;
            if (enemyIndex >= s.Enemies.Count)
            {
                continue;
            }

            var enemy = s.Enemies[enemyIndex];
            obs[baseIndex] = enemy.Hp;
            obs[baseIndex + 1] = enemy.MaxHp;
            obs[baseIndex + 2] = enemy.Block;
            obs[baseIndex + 3] = (int)enemy.CurrentIntent.Type;
            obs[baseIndex + 4] = enemy.CurrentIntent.Magnitude;
            for (int buffIndex = 0; buffIndex < MaxEnemyBuffs; buffIndex++)
            {
                if (buffIndex < enemy.Buffs.Count)
                {
                    obs[baseIndex + 5 + buffIndex * 2] = (int)enemy.Buffs[buffIndex].Id;
                    obs[baseIndex + 5 + buffIndex * 2 + 1] = enemy.Buffs[buffIndex].Magnitude;
                }
            }
        }

        offset = 8 + MaxHand * 2 + 6 + MaxPlayerBuffs * 2 + MaxEnemies * enemySlotSize;
        for (int enemyIndex = 0; enemyIndex < MaxEnemies; enemyIndex++)
        {
            if (
                enemyIndex < s.Enemies.Count
                && s.Enemies[enemyIndex].SecondaryIntent is { } secondary
            )
            {
                obs[offset + enemyIndex * 2] = (int)secondary.Type + 1;
                obs[offset + enemyIndex * 2 + 1] = secondary.Magnitude;
            }
        }

        obs[156] = s.PlayerGold;
    }
}
