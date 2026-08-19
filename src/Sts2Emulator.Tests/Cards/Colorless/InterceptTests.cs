using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

// 1-cost Skill, CardMultiplayerConstraint.MultiplayerOnly, TargetType.AnyAlly.
// MegaCrit.Sts2.Core.Models.Cards/Intercept.cs: BlockVar(9m) to yourself, then
// CoveredPower(1) on the targeted ally; OnUpgrade raises the block by 4.
//
// CoveredPower guards an ally and means nothing alone, so the emulator stands Intangible
// in for it. That is a generous stand-in — Intangible caps all incoming damage at 1 — and
// these tests pin it so the substitution is visible rather than assumed harmless.
public class InterceptTests
{
    [Fact]
    public void GainsNineBlock()
    {
        var fight = Fight.Hand(Card(CL.Intercept)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(9, fight.State.PlayerBlock);
    }

    [Fact]
    public void UpgradedGainsThirteen()
    {
        var fight = Fight.Hand(Card(CL.Intercept, upgraded: true)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(13, fight.State.PlayerBlock);
    }

    [Fact]
    public void StandsIntangibleInForCoveredPower()
    {
        var fight = Fight.Hand(Card(CL.Intercept)).Energy(1).Enemy(hp: 40);

        fight.Play();

        Assert.Equal(1, fight.PlayerBuffAmount(BuffId.Intangible));
    }
}
