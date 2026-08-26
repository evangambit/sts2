using System.Linq;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Core.Run;
using Xunit;
using static Sts2Emulator.Tests.TestDeck;

namespace Sts2Emulator.Tests;

/// <summary>
/// Keywords an UPGRADE changes, and the one keyword the table had no field for at all.
/// </summary>
/// <remarks>
/// All four came out of one sweep, prompted by how `Retain` and `Sly` were lost:
/// `extract_data.py` gathers canonical keywords from a tuple, and `CardDef` had fields
/// that were not in it. A field-by-field sweep of all five generated tables found no other
/// never-emitted field — but counting each keyword against the decompiled source found
/// three more gaps of the same family, which is what `scripts/audit_card_keywords.py` now
/// checks on every run.
///
/// The direction is not interchangeable and that is the whole finding: twelve cards GAIN
/// Retain when upgraded, the way fifteen gain Innate, while nineteen LOSE Exhaust and
/// three lose Ethereal. For most of the latter, losing it is the entire benefit of the
/// upgrade.
/// </remarks>
public class UpgradeKeywordTests
{
    private const int CalculatedGamble = 75;
    private const int Discovery = 146;
    private const int Hologram = 252;
    private const int Apparition = 17;
    private const int EchoForm = 159;
    private const int VoidForm = 534;
    private const int AscendersBane = 10001;

    // ── Retain, gained on upgrade ────────────────────────────────────────────

    /// <summary>
    /// Twelve cards add Retain in `OnUpgrade`. `CardDef` had a `Retain` field and no
    /// `RetainWhenUpgraded`, so an upgraded Calculated Gamble was discarded with the rest
    /// of the hand.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AnUpgradeThatGrantsRetainKeepsTheCard(bool upgraded, bool survives)
    {
        var fight = Fight.Hand(Card(CalculatedGamble, upgraded)).Energy(3);
        fight.State.PlayerHp = 999;

        fight.EndTurn();

        Assert.Equal(survives, fight.State.Hand.Any(c => c.DefId == CalculatedGamble));
    }

    // ── Exhaust, lost on upgrade ─────────────────────────────────────────────

    /// <summary>
    /// Nineteen cards remove Exhaust in `OnUpgrade`, and `ShouldExhaustAfterPlay` read the
    /// printed keyword and nothing else — so every one of them exhausted anyway, which for
    /// most of them is the upgrade's whole point.
    /// </summary>
    [Theory]
    [InlineData(Discovery, false, true)]
    [InlineData(Discovery, true, false)]
    [InlineData(Hologram, false, true)]
    [InlineData(Hologram, true, false)]
    public void AnUpgradeThatRemovesExhaustStopsItExhausting(
        int defId,
        bool upgraded,
        bool exhausts
    )
    {
        var fight = Fight.Hand(Card(defId, upgraded)).Energy(3).Enemy(hp: 60);

        fight.Play();
        // Discovery raises its own choose-a-card screen; answer it so the play finishes.
        while (fight.Pending is not null)
        {
            fight.Choose(0);
        }

        Assert.Equal(exhausts, fight.State.ExhaustPile.Any(c => c.DefId == defId));
        Assert.Equal(!exhausts, fight.State.DiscardPile.Any(c => c.DefId == defId));
    }

    // ── Ethereal, lost on upgrade ────────────────────────────────────────────

    /// <summary>
    /// Three cards remove Ethereal on upgrade, and the emulator knew about exactly one of
    /// them — by its raw id. `if (def.Ethereal &amp;&amp; !(def.Id == 159 &amp;&amp; card.Upgraded))`
    /// is Echo Form, and said nothing about Apparition or Void Form.
    /// </summary>
    [Theory]
    [InlineData(Apparition)]
    [InlineData(EchoForm)]
    [InlineData(VoidForm)]
    public void AnUpgradeThatRemovesEtherealStopsItVanishing(int defId)
    {
        var plain = Fight.Hand(Card(defId)).Energy(0);
        plain.State.PlayerHp = 999;
        plain.EndTurn();
        Assert.Contains(plain.State.ExhaustPile, c => c.DefId == defId);

        var upgraded = Fight.Hand(Card(defId, upgraded: true)).Energy(0);
        upgraded.State.PlayerHp = 999;
        upgraded.EndTurn();
        Assert.DoesNotContain(upgraded.State.ExhaustPile, c => c.DefId == defId);
    }

    // ── Eternal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// `CardModel.IsRemovable` is `!Keywords.Contains(CardKeyword.Eternal)`, and
    /// `CardSelectCmd.FromDeckForRemoval` filters on it — so the game will not so much as
    /// OFFER an Eternal card for removal. Seven curses carry it, Ascender's Bane among
    /// them, and `CardDef` had no field for the keyword at all.
    /// </summary>
    [Fact]
    public void AnEternalCardIsNotOfferedForRemoval()
    {
        var state = new RunState
        {
            Deck =
            [
                new CardInstance(AscendersBane, false),
                new CardInstance(IC.StrikeIronclad, false),
            ],
        };

        Assert.True(RunNonCombatEffects.BeginDeckSelection(state, DeckSelection.Remove, 0));
        Assert.False(RunNonCombatEffects.CanSelectCard(state, 0));
        Assert.True(RunNonCombatEffects.CanSelectCard(state, 1));
    }

    /// <summary>
    /// And a deck of nothing BUT Eternal cards has nothing to offer, so the selection does
    /// not open — which is what stops a removal being silently spent on nobody.
    /// </summary>
    [Fact]
    public void ADeckOfEternalCardsOffersNoRemovalAtAll()
    {
        var state = new RunState { Deck = [new CardInstance(AscendersBane, false)] };

        Assert.False(RunNonCombatEffects.BeginDeckSelection(state, DeckSelection.Remove, 0));
    }
}
