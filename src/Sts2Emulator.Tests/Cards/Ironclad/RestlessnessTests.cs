using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class RestlessnessTests
{
    [Fact]
    public void DrawsAndGainsEnergyWhenOnlyCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Restlessness, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 0;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
    }
}
