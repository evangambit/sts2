using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class DrumOfBattleTests
{
    [Fact]
    public void DrawsAndGainsEnergyWhenExhausted()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.DrumOfBattle, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        // Playing it only draws — DrumOfBattle declares no Exhaust keyword, and its
        // OnPlay just draws. The energy comes from AfterCardExhausted, which fires
        // when the card itself is exhausted by something else.
        Assert.Equal(0, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.DrumOfBattle);
        Assert.Contains(state.DiscardPile, card => card.DefId == IC.DrumOfBattle);

        CardEffects.ExhaustCard(state, new CardInstance(IC.DrumOfBattle, false));

        Assert.Equal(2, state.Energy);
    }

    [Fact]
    public void GainsEnergyWhenExhaustedByAnotherCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.TrueGrit, false),
            new CardInstance(IC.DrumOfBattle, true),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(3, state.Energy);
        Assert.Equal(7, state.PlayerBlock);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DrumOfBattle);
        Assert.DoesNotContain(state.Hand, card => card.DefId == IC.DrumOfBattle);
    }
}
