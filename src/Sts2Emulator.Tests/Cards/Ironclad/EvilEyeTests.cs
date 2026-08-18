using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

public class EvilEyeTests
{
    [Fact]
    public void GainsBlockOnceWithoutPriorExhaust()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.EvilEye, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(8, state.PlayerBlock);
        // EvilEye reads whether a card was exhausted this turn; it does not exhaust
        // itself, so playing it alone leaves the count at zero.
        Assert.Equal(0, state.CardsExhaustedThisTurn);
        Assert.DoesNotContain(state.ExhaustPile, card => card.DefId == IC.EvilEye);
    }

    [Fact]
    public void GainsBlockTwiceAfterCardExhaustedThisTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.EvilEye, true)];
        state.Energy = 1;
        CardEffects.ExhaustCard(state, new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(22, state.PlayerBlock);
        // Only the pre-exhausted Strike counts — EvilEye does not add itself.
        Assert.Equal(1, state.CardsExhaustedThisTurn);
    }
}
