namespace Sts2Emulator.Core.Effects;

public static class PotionEffects
{
    public static void Apply(int potionId, CombatState state, Random? rng = null)
    {
        // Populated incrementally as potions are reverse-engineered from sts2.dll.
        switch (potionId)
        {
            case 5: // Block Potion: gain 12 unpowered Block.
                CardEffects.GainBlock(state, 12, rng);
                break;
            case 17: // Duplicator: this turn, play the next card an extra time.
                BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, 1);
                break;
            case 51: // Shackling Potion: all enemies lose 7 Strength this turn.
                foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
                {
                    if (BuffSystem.TryConsumeArtifact(enemy.Buffs))
                    {
                        continue;
                    }

                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, -7);
                    BuffSystem.Apply(enemy.Buffs, BuffId.TemporaryStrength, 7);
                }
                break;
            default:
                break;
        }
    }
}
