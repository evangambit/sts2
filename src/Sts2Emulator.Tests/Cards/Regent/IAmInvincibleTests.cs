using Sts2Emulator.Core;
using Xunit;

namespace Sts2Emulator.Tests;

// MegaCrit.Sts2.Core.Models.Cards/IAmInvincible.cs: 10 block on play, and nothing else
// then. Its `AfterAutoPostPlayPhaseEntered` auto-plays the card ITSELF when it is sitting
// on TOP of the draw pile as the play phase ends —
// `AutoPlayFromDrawPile(..., 1, Top, forceExhaust: false)`.
//
// The emulator auto-played the first ATTACK in the draw pile the moment this was played,
// which is neither the card, the trigger, nor the time.
public class IAmInvincibleTests
{
    private const int IAmInvincible = 257;
    private const int StrikeRegent = 474;
    private const int DefendRegent = 133;

    [Fact]
    public void PlayingItJustBlocks()
    {
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(new CardInstance(StrikeRegent, false), new CardInstance(DefendRegent, false))
            .Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(IAmInvincible, false));

        fight.Play(0);

        Assert.Equal(10, fight.State.PlayerBlock);
        // The draw pile is untouched: no attack was auto-played out of it.
        Assert.Equal(2, fight.State.DrawPile.Count);
        Assert.Equal(500, fight.Enemy0.Hp);
    }

    /// <summary>On top of the draw pile at end of turn, it plays itself.</summary>
    [Fact]
    public void OnTopOfTheDrawPileItPlaysItself()
    {
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(new CardInstance(IAmInvincible, false), new CardInstance(StrikeRegent, false))
            .Enemy(hp: 500);

        fight.EndTurn();

        // It blocked before the enemy's turn, so some of that block was spent rather than
        // being visible now -- what is certain is that it left the draw pile.
        Assert.DoesNotContain(fight.State.DrawPile, c => c.DefId == IAmInvincible);
    }

    /// <summary>Anywhere but the top, it sits there.</summary>
    [Fact]
    public void UnderAnotherCardItDoesNothing()
    {
        var fight = Fight
            .Hand()
            .Energy(9)
            .Draw(new CardInstance(StrikeRegent, false), new CardInstance(IAmInvincible, false))
            .Enemy(hp: 500);

        fight.EndTurn();

        // The turn's draw took the Strike, and the card under it was never on top while the
        // play phase was ending.
        Assert.Equal(0, fight.State.PlayerBlock);
    }
}

// MegaCrit.Sts2.Core.Models.Cards/HammerTime.cs: one stack, and the upgrade is a discount.
// `HammerTimePower.AfterForge` forges the same amount for every OTHER player, so in a solo
// run it does nothing whatever. It is `MultiplayerOnly`, and tracked only because the game
// reports the power and a capture compares the whole set — the Legion of Bone lesson
// (E264) in its other form: unobtainable solo, and here genuinely inert as well.
public class HammerTimeTests
{
    private const int HammerTime = 233;
    private const int RefineBlade = 389; // Forge 9

    private static Fight Played()
    {
        var fight = Fight.Hand().Energy(9).Enemy(hp: 500);
        fight.State.Hand.Add(new CardInstance(HammerTime, false));
        fight.Play(0);
        return fight;
    }

    [Fact]
    public void ItAppliesItsPowerAndNoStrength()
    {
        var fight = Played();

        Assert.Equal(1, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.HammerTime));
        Assert.Equal(0, BuffSystem.Get(fight.State.PlayerBuffs, BuffId.Strength));
    }

    /// <summary>A forge with it up is the same forge: there is nobody else to forge for.</summary>
    [Fact]
    public void ItChangesNothingAboutAForge()
    {
        var control = Fight.Hand().Energy(9).Enemy(hp: 500);
        control.State.Hand.Add(new CardInstance(RefineBlade, false));
        control.Play(0);
        int plain = control.ForgedDamage();

        var fight = Played();
        fight.State.Hand.Add(new CardInstance(RefineBlade, false));
        fight.Play(fight.State.Hand.Count - 1);

        Assert.Equal(plain, fight.ForgedDamage());
    }
}
