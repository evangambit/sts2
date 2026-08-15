namespace Sts2Emulator.Core.Rng;

public static class DeterministicHash
{
    public static int GetDeterministicHashCode(string value)
    {
        unchecked
        {
            int hash1 = 352654597;
            int hash2 = 352654597;

            for (int i = 0; i < value.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ value[i];
                if (i == value.Length - 1)
                {
                    break;
                }

                hash2 = ((hash2 << 5) + hash2) ^ value[i + 1];
            }

            return hash1 + hash2 * 1566083941;
        }
    }
}
