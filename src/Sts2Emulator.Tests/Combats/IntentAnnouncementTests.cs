using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// What a monster's intent SAYS, as opposed to what it does. The game builds the label in
/// AttackIntent.GetTotalDamage, from the per-hit damage after Hook.ModifyDamage — so
/// Strength lands on each hit before the multiply, and the two orders differ for anything
/// that swings more than once.
///
/// Getting this wrong is invisible in a fight where nothing buffs itself, which is why it
/// stood for so long: the emulator announced base damage, and HANDOFF carried it as a
/// known-open gap with this exact fix prescribed.
/// </summary>
public class IntentAnnouncementTests
{
    private static List<BuffState> Strength(int amount) => [new BuffState(BuffId.Strength, amount)];

    [Fact]
    public void ASingleHitAnnouncesItsDamagePlusStrength()
    {
        var intent = new Intent(IntentType.Attack, 10);

        Assert.Equal(13, intent.AnnouncedDamage(Strength(3), []));
    }

    /// <summary>
    /// Three hits of 3 with 2 Strength is 3x(3+2) = 15, not (3x3)+2 = 11. Pre-multiplying
    /// into a total cannot express the difference, which is the whole reason Intent carries
    /// a hit count.
    /// </summary>
    [Fact]
    public void StrengthLandsOnEveryHitOfAMultiHit()
    {
        var intent = new Intent(IntentType.Attack, 3, Hits: 3);

        Assert.Equal(15, intent.AnnouncedDamage(Strength(2), []));
    }

    [Fact]
    public void AnUnbuffedMultiHitAnnouncesItsPlainTotal()
    {
        var intent = new Intent(IntentType.Attack, 4, Hits: 3);

        Assert.Equal(12, intent.AnnouncedDamage([], []));
    }

    /// <summary>A non-attack's magnitude is a count — Dazed added, block gained — not damage.</summary>
    [Fact]
    public void NonAttackIntentsAreLeftAlone()
    {
        var intent = new Intent(IntentType.Debuff, 5);

        Assert.Equal(5, intent.AnnouncedDamage(Strength(3), []));
    }

    /// <summary>
    /// The player's own debuffs count too: the game runs the same ModifyDamage chain, so a
    /// Weak attacker announces the reduced number rather than its printed one.
    /// </summary>
    [Fact]
    public void AWeakAttackerAnnouncesLess()
    {
        var intent = new Intent(IntentType.Attack, 10);
        List<BuffState> weak = [new BuffState(BuffId.Weak, 1)];

        Assert.Equal(7, intent.AnnouncedDamage(weak, []));
    }
}
