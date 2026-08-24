using System.Reflection;
using Sts2Emulator.Core;
using Sts2Emulator.Core.Run;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// The action mask and the step have to agree about every event option, in every run
/// state -- an option the mask offers must be takeable, and an option it withholds must be
/// refused.
///
/// This exists because the disagreement is a real recurring defect rather than a
/// hypothetical one, and it is invisible to everything else. The live fixtures each pin
/// ONE run state, so an option whose gate reads a rolled price agrees with the mask at the
/// captured gold and diverges everywhere else. Two have been found by hand:
///
/// <list type="bullet">
/// <item>The Tea Master's mask read BoneTeaCost (50) while the step charged the Ember
/// price (150), so a run holding 50 gold was offered Bone Tea and then refused.</item>
/// <item>Luminous Choir's mask read the ROLLED tribute (149 minus NextInt(0, 50)) while
/// the step charged a flat 149, so a run holding 120 gold was offered the tribute and
/// then refused.</item>
/// </list>
///
/// Both are the same shape and both cost an agent a legal move it was told it had. Rather
/// than wait for the third, this sweeps every modelled event across a spread of gold, hp,
/// potion and deck states and asserts the two sides agree.
/// </summary>
public class EventMaskAgreesWithStepTests
{
    /// <summary>
    /// Options the mask offers that the step refuses on purpose, because the option needs
    /// a mechanic the emulator does not have. Each is a real gap: an agent is told it may
    /// take the option and then cannot. Masking them out would be the other honest answer
    /// -- but it would also hide the gap, so they are listed here instead.
    /// </summary>
    private static readonly HashSet<(string Event, int Option)> Unmodelled = [];

    /// <summary>
    /// Run states worth sweeping: the gates read gold, hp, max hp, the belt and the deck,
    /// and the interesting values are the ones either side of a threshold.
    ///
    /// An EMPTY deck is deliberately not among them. No run reaches one -- Ascender's Bane
    /// is not removable and every event that removes cards is gated on having some -- so
    /// sweeping it only produces disagreements about a state the game cannot be in, and
    /// resolving those would mean encoding an answer nothing can check.
    /// </summary>
    private static readonly (string Name, Action<RunState> Apply)[] Situations =
    [
        ("a fresh run", _ => { }),
        ("broke", state => state.Gold = 0),
        ("55 gold", state => state.Gold = 55),
        ("120 gold", state => state.Gold = 120),
        ("rich", state => state.Gold = 999),
        ("nearly dead", state => state.PlayerHp = 1),
        (
            "at full health",
            state =>
            {
                state.Gold = 999;
                state.PlayerHp = state.PlayerMaxHp;
            }
        ),
        (
            "holding a potion",
            state =>
            {
                state.Gold = 999;
                state.PotionSlots[0] = GeneratedData.Potions.FindId("FoulPotion") ?? 0;
            }
        ),
        (
            "with no attacks",
            state =>
            {
                state.Gold = 999;
                state.Deck.RemoveAll(card =>
                    GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
                );
            }
        ),
        (
            "with a fully upgraded deck",
            state =>
            {
                state.Gold = 999;
                for (int i = 0; i < state.Deck.Count; i++)
                {
                    state.Deck[i] = state.Deck[i] with { Upgraded = true };
                }
            }
        ),
    ];

    private static readonly Dictionary<int, string> Names = BuildNames();

    private static Dictionary<int, string> BuildNames() =>
        typeof(RunConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("Event") && field.FieldType == typeof(int))
            .Where(field => (int)field.GetValue(null)! > 0)
            .GroupBy(field => (int)field.GetValue(null)!)
            .ToDictionary(group => group.Key, group => group.First().Name["Event".Length..]);

    public static TheoryData<string> Events()
    {
        var data = new TheoryData<string>();
        foreach (string name in ImplementedEvents.Names)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    /// One generated run, cloned per probe rather than generated per probe.
    /// </summary>
    /// <remarks>
    /// This sweep asks for a fresh run several thousand times -- every event, in every
    /// situation, for every option, three times over -- and <c>Reset</c> is not cheap:
    /// it generates the map, the encounter sequences and the relic grab bags' 230 shuffle
    /// draws, at ~149ms a time. That was 304 seconds of a 420-second suite sample, about
    /// three quarters of the whole run, to build the same run over and over and then
    /// overwrite two fields of it.
    ///
    /// <para>
    /// <c>RunEngine.Clone</c> is the same state for ~0.07ms, three orders of magnitude
    /// cheaper, and it is the fork the tree search already relies on. Cloning a pristine
    /// run is exactly as isolated as resetting one: every probe still gets its own state
    /// and nothing it does can reach another.
    /// </para>
    /// </remarks>
    private static readonly RunEngine Pristine = GeneratePristineRun();

    private static RunEngine GeneratePristineRun()
    {
        var engine = new RunEngine();
        engine.Reset("ABCDEF");
        return engine;
    }

    private static RunEngine At(int eventId, Action<RunState> situation)
    {
        var engine = Pristine.Clone();
        situation(engine.State);
        engine.State.EventId = eventId;
        engine.State.Phase = RunPhase.Event;
        return engine;
    }

    private static int[] Offered(RunEngine engine)
    {
        var mask = new int[RunConstants.MaxActions];
        engine.WriteActionMask(mask);
        return Enumerable
            .Range(0, RunConstants.EventSkipAction)
            .Where(index => mask[index] != 0)
            .ToArray();
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void EveryOfferedOptionIsTakeable(string eventName)
    {
        int eventId = Names.First(pair => pair.Value == eventName).Key;
        var refused = new List<string>();

        foreach (var (label, apply) in Situations)
        {
            var probe = At(eventId, apply);
            foreach (int option in Offered(probe))
            {
                if (Unmodelled.Contains((eventName, option)))
                {
                    continue;
                }

                var engine = At(eventId, apply);
                if (engine.Step(option, -1, out _, out _, out _) == -1)
                {
                    refused.Add($"option {option} {label}");
                }
            }
        }

        Assert.True(
            refused.Count == 0,
            $"{eventName} offers options its own step refuses: "
                + string.Join("; ", refused.Distinct())
        );
    }

    /// <summary>
    /// The other direction. An option the mask withholds must be refused, or the mask is
    /// hiding a move the agent is entitled to -- which costs it the option silently
    /// instead of loudly.
    /// </summary>
    [Theory]
    [MemberData(nameof(Events))]
    public void EveryWithheldOptionIsRefused(string eventName)
    {
        int eventId = Names.First(pair => pair.Value == eventName).Key;
        var accepted = new List<string>();

        foreach (var (label, apply) in Situations)
        {
            var probe = At(eventId, apply);
            var offered = Offered(probe).ToHashSet();
            for (int option = 0; option < RunConstants.EventSkipAction; option++)
            {
                if (offered.Contains(option))
                {
                    continue;
                }

                var engine = At(eventId, apply);
                if (engine.Step(option, -1, out _, out _, out _) != -1)
                {
                    accepted.Add($"option {option} {label}");
                }
            }
        }

        Assert.True(
            accepted.Count == 0,
            $"{eventName} withholds options its own step accepts: "
                + string.Join("; ", accepted.Distinct())
        );
    }

    /// <summary>
    /// Leaving is always available, whatever the run holds: an event the player cannot
    /// afford any option of still has to be escapable.
    /// </summary>
    [Theory]
    [MemberData(nameof(Events))]
    public void LeavingIsAlwaysOffered(string eventName)
    {
        int eventId = Names.First(pair => pair.Value == eventName).Key;

        foreach (var (label, apply) in Situations)
        {
            var engine = At(eventId, apply);
            var mask = new int[RunConstants.MaxActions];
            engine.WriteActionMask(mask);

            Assert.True(
                mask[RunConstants.EventSkipAction] != 0,
                $"{eventName} offers no way out {label}"
            );
        }
    }
}
