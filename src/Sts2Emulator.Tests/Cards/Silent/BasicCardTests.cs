using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// The last four Silent cards: the three a run starts with, and Suppress.
//
// The basics are the most-played cards in the game and the least interesting, which is
// exactly why they went unpinned the longest. Their numbers were right. The Defend was
// not: `FastenPower` reads the card's `CardTag.Defend` and the emulator carried the tag at
// the call site rather than on the card, so the two Defends with their own `case` arm did
// not have it.

public class StrikeSilentTests
{
    // DamageVar(6m) +3. Tagged CardTag.Strike, which is what Perfected Strike and its kin
    // count -- the tag is not extracted yet, so nothing reads it here.
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public void Hits(bool upgraded, int damage)
    {
        var fight = Fight.Hand(Card(SI.StrikeSilent, upgraded)).Energy(1).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
    }

    /// <summary>
    /// Strength is added per hit before block, as any powered attack is — worth pinning on
    /// the card an agent plays more than any other.
    /// </summary>
    [Fact]
    public void StrengthAndBlockApplyAsUsual()
    {
        var fight = Fight.Hand(Card(SI.StrikeSilent)).Energy(1).Enemy(hp: 60, block: 4);
        BuffSystem.Apply(fight.State.PlayerBuffs, BuffId.Strength, 3);

        fight.Play();

        Assert.Equal(60 - (6 + 3 - 4), fight.Enemy0.Hp);
    }
}

/// <summary>
/// Defend Silent: five block, and it carries <c>CardTag.Defend</c>.
/// </summary>
/// <remarks>
/// The tag is not decoration. `FastenPower.ModifyBlockAdditive` returns its amount only
/// when the block's `cardSource` is tagged Defend, and the emulator passed that as a flag
/// at ONE call site — the generated-card approximation — so `DefendSilent` and
/// `UltimateDefend`, the two Defends with their own `case` arm, quietly did not have it.
/// The tag is a fact about the card, so it now rides on the state for the duration of the
/// card rather than being remembered per call: the same fix as the discard chokepoint and
/// the temporary-strength helper, and the sixth time this shape has come up.
/// </remarks>
public class DefendSilentTests
{
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public void Blocks(bool upgraded, int block)
    {
        var fight = Fight.Hand(Card(SI.DefendSilent, upgraded)).Energy(1);

        fight.Play();

        Assert.Equal(block, fight.State.PlayerBlock);
    }

    [Fact]
    public void FastenPaysOutOnIt()
    {
        var fight = Fight.Hand(Card(CL.Fasten), Card(SI.DefendSilent)).Energy(3);

        fight.Play();
        Assert.Equal(4, fight.PlayerBuffAmount(BuffId.FastenPower));

        fight.Play();

        Assert.Equal(5 + 4, fight.State.PlayerBlock);
    }

    /// <summary>And not on a Skill that merely blocks — Fasten reads the tag, not the effect.</summary>
    [Fact]
    public void FastenDoesNotPayOutOnAnUntaggedBlockCard()
    {
        var fight = Fight.Hand(Card(CL.Fasten), Card(SI.Deflect)).Energy(3);
        fight.Play();

        fight.Play();

        Assert.Equal(4, fight.State.PlayerBlock);
    }

    /// <summary>
    /// Ultimate Defend is the other card that had its own arm and so lost the tag with it.
    /// It is Ironclad's, and it is here because the fix is one chokepoint rather than two
    /// call sites — pinning only the card that prompted the fix would leave the other free
    /// to regress.
    /// </summary>
    [Fact]
    public void FastenPaysOutOnUltimateDefendToo()
    {
        var fight = Fight.Hand(Card(CL.Fasten), Card(IC.UltimateDefend)).Energy(3);
        fight.Play();

        fight.Play();

        Assert.Equal(11 + 4, fight.State.PlayerBlock);
    }
}

public class NeutralizeTests
{
    // DamageVar(3m) +1 and PowerVar<WeakPower>(1m) +1 -- both vars upgrade, and it is free.
    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(true, 4, 2)]
    public void HitsAndWeakens(bool upgraded, int damage, int weak)
    {
        var fight = Fight.Hand(Card(SI.Neutralize, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
    }
}

public class SuppressTests
{
    // DamageVar(11m) +6 and PowerVar<WeakPower>(3m) +2, 0-cost and Innate. Rarity Ancient
    // rather than Rare -- it is not in the ordinary reward pool.
    [Theory]
    [InlineData(false, 11, 3)]
    [InlineData(true, 17, 5)]
    public void HitsHardAndWeakens(bool upgraded, int damage, int weak)
    {
        var fight = Fight.Hand(Card(SI.Suppress, upgraded)).Energy(0).Enemy(hp: 60);

        fight.Play();

        Assert.Equal(60 - damage, fight.Enemy0.Hp);
        Assert.Equal(weak, fight.EnemyBuffAmount(BuffId.Weak));
    }

    [Fact]
    public void ItIsInnateAndAncient()
    {
        var def = GeneratedData.Cards.Get(SI.Suppress);

        Assert.True(def.Innate);
        Assert.Equal(CardRarity.Ancient, def.Rarity);
    }
}
