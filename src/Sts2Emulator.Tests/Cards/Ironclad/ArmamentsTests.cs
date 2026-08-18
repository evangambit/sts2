using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class ArmamentsTests
{
    [Fact]
    public void GainsBlockAndUpgradesFirstCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.DoesNotContain(
            state.Hand,
            card => card.DefId == IC.StrikeIronclad && !card.Upgraded
        );
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && !card.Upgraded);
    }

    [Fact]
    public void UpgradedUpgradesAllCardsInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(ST.Slimed, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == ST.Slimed && !card.Upgraded);
    }
}
