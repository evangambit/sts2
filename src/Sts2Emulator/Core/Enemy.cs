namespace Sts2Emulator.Core;

public enum IntentType
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Unknown,
}

public readonly record struct EnemyDef(
    int Id,
    string Name,
    int MinHp,
    int MaxHp,
    int[] Moves // flat [damage, repeats, damage, repeats, ...] pairs
);

public readonly record struct Intent(IntentType Type, int Magnitude);

public sealed class EnemyState
{
    public int DefId;
    public int Hp;
    public int MaxHp;
    public int Block;
    public Intent CurrentIntent;
    public Intent? SecondaryIntent;
    public List<BuffState> Buffs = [];
    public int MoveIndex;
    public int LastMove = -1; // ID of the last move chosen (to avoid repetition)
    public int StolenGold;
    public int HeistGold;
}
