namespace Sts2Emulator.Core;

public static class CombatObservation
{
    public const int MaxHand = 10;
    public const int MaxEnemies = 6;
    public const int MaxPlayerBuffs = 10;
    public const int MaxEnemyBuffs = 5;

    /// <summary>Candidates an open card selection can expose; a hand or pile is capped anyway.</summary>
    public const int MaxSelectionCandidates = 10;

    /// <summary>Hp, max hp, block, energy, max energy, and the three pile sizes.</summary>
    public const int ScalarCount = 8;

    /// <summary>
    /// Per card: def id, upgraded, enchantment, and the amount it was applied at. The last
    /// two used to be missing, so a Sharp Strike and a plain one were the same two numbers
    /// -- and the enchantment is worth 2 damage on every attack, which is the difference
    /// between a card being worth playing and not.
    /// </summary>
    public const int CardSlotSize = 4;

    public const int HandOffset = ScalarCount;
    public const int PotionSlotSize = 2;
    public const int PotionOffset = HandOffset + MaxHand * CardSlotSize;
    public const int BuffSlotSize = 2;
    public const int PlayerBuffOffset = PotionOffset + 3 * PotionSlotSize;

    /// <summary>Hp, max hp, block, intent type, announced magnitude, then the buffs.</summary>
    public const int EnemySlotSize = 5 + MaxEnemyBuffs * BuffSlotSize;

    /// <summary>Where an enemy slot carries what it means to do this turn.</summary>
    public const int EnemyIntentField = 3;

    public const int EnemyOffset = PlayerBuffOffset + MaxPlayerBuffs * BuffSlotSize;
    public const int SecondaryIntentSlotSize = 2;
    public const int SecondaryIntentOffset = EnemyOffset + MaxEnemies * EnemySlotSize;
    public const int GoldOffset = SecondaryIntentOffset + MaxEnemies * SecondaryIntentSlotSize;
    public const int SelectionKindOffset = GoldOffset + 1;
    public const int SelectionCountOffset = SelectionKindOffset + 1;
    public const int SelectionOffset = SelectionCountOffset + 1;
    public const int ObsSize = SelectionOffset + MaxSelectionCandidates * CardSlotSize;

    /// <summary>Writes one card into a slot: what it is, and what has been done to it.</summary>
    private static void WriteCard(Span<int> obs, int at, CardInstance card)
    {
        obs[at] = card.DefId;
        obs[at + 1] = card.Upgraded ? 1 : 0;
        obs[at + 2] = (int)card.Enchantment;
        obs[at + 3] = card.EnchantAmount;
    }

    /// <summary>
    /// What the game shows on an attack intent. AttackIntent.GetSingleDamage runs the
    /// move's damage through Hook.ModifyDamage before displaying it, so the number the
    /// player reads already includes the attacker's Strength and its own Weak — reporting
    /// the raw move damage would tell a policy a Ritual-stacking cultist still hits for
    /// nine on the turn it hits for fifteen. Non-attack intents carry a count, not damage.
    /// </summary>
    private static int AnnouncedMagnitude(CombatState s, EnemyState enemy) =>
        enemy.CurrentIntent.AnnouncedDamage(enemy.Buffs, s.PlayerBuffs);

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

        for (int i = 0; i < MaxHand && i < s.Hand.Count; i++)
        {
            WriteCard(obs, HandOffset + i * CardSlotSize, s.Hand[i]);
        }

        int offset = PotionOffset;
        for (int i = 0; i < 3; i++)
        {
            obs[offset + i * PotionSlotSize] = s.PotionSlots[i];
            obs[offset + i * PotionSlotSize + 1] = s.PotionSlots[i] != 0 ? 1 : 0;
        }

        offset = PlayerBuffOffset;
        for (int i = 0; i < MaxPlayerBuffs; i++)
        {
            if (i < s.PlayerBuffs.Count)
            {
                obs[offset + i * BuffSlotSize] = (int)s.PlayerBuffs[i].Id;
                obs[offset + i * BuffSlotSize + 1] = s.PlayerBuffs[i].Magnitude;
            }
        }

        offset = EnemyOffset;
        int enemySlotSize = EnemySlotSize;
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
            obs[baseIndex + 4] = AnnouncedMagnitude(s, enemy);
            for (int buffIndex = 0; buffIndex < MaxEnemyBuffs; buffIndex++)
            {
                if (buffIndex < enemy.Buffs.Count)
                {
                    obs[baseIndex + 5 + buffIndex * 2] = (int)enemy.Buffs[buffIndex].Id;
                    obs[baseIndex + 5 + buffIndex * 2 + 1] = enemy.Buffs[buffIndex].Magnitude;
                }
            }
        }

        offset = SecondaryIntentOffset;
        for (int enemyIndex = 0; enemyIndex < MaxEnemies; enemyIndex++)
        {
            if (
                enemyIndex < s.Enemies.Count
                && s.Enemies[enemyIndex].SecondaryIntent is { } secondary
            )
            {
                obs[offset + enemyIndex * SecondaryIntentSlotSize] = (int)secondary.Type + 1;
                obs[offset + enemyIndex * SecondaryIntentSlotSize + 1] = secondary.Magnitude;
            }
        }

        obs[GoldOffset] = s.PlayerGold;

        // An open card selection replaces the action space, so a policy is blind without
        // knowing both that one is open and what it is choosing between.
        if (s.PendingSelection is { } selection)
        {
            obs[SelectionKindOffset] = (int)selection.Kind;
            obs[SelectionCountOffset] = selection.Candidates.Count;

            // A generated choice has no pile behind it; its options are on the selection.
            if (selection.Kind == CardSelectionKind.GeneratedCardToHand)
            {
                for (
                    int i = 0;
                    i < MaxSelectionCandidates && i < selection.GeneratedCandidates.Count;
                    i++
                )
                {
                    obs[SelectionOffset + i * CardSlotSize] =
                        selection.GeneratedCandidates[i];
                }
            }
            else
            {
                var pile = selection.Kind switch
                {
                    CardSelectionKind.DiscardToDrawPileTop => s.DiscardPile,
                    CardSelectionKind.DrawPileToHand => s.DrawPile,
                    _ => s.Hand,
                };
                for (int i = 0; i < MaxSelectionCandidates && i < selection.Candidates.Count; i++)
                {
                    int index = selection.Candidates[i];
                    if (index < pile.Count)
                    {
                        WriteCard(obs, SelectionOffset + i * CardSlotSize, pile[index]);
                    }
                }
            }
        }
    }
}
