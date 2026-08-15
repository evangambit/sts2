namespace Sts2Emulator.Core;

public readonly record struct RelicDef(int Id, string Name);

public readonly record struct RelicInstance(int DefId, int Counter = 0);
