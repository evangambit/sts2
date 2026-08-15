namespace Sts2Emulator.Core;

public enum OrbType
{
    Lightning,
    Frost,
    Dark,
    Plasma,
    Glass,
}

public readonly record struct OrbState(OrbType Type, int EvokeValue = 0);
