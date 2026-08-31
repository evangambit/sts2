using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Sts2Emulator.Core.Effects;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Every <c>RelicEffects</c> id constant has to equal the id the extractor wrote for that
/// name. A constant off by a few is a relic that never fires and a relic that fires for
/// the wrong reason, and NOTHING else in the suite can see it: the arm is written, the
/// tests around it drive the constant rather than the relic, and `audit_relics.py` reports
/// the id as unmodelled while the name looks handled.
/// </summary>
/// <remarks>
/// Written because four of them were wrong at once -- Bing Bong, Lost Wisp, Wongo's
/// Customer Appreciation Badge and Wongo's Mystery Ticket, all transcribed from a stale
/// reading in one sitting. The audit caught them only because it matches on the id AND the
/// name, which is a coincidence of that tool's design rather than a guard.
/// </remarks>
public class RelicConstantTests
{
    private static readonly Regex Declaration = new(
        @"public const int (\w+) = (\d+);",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Both files that name relics by id. `RunConstants` matters as much as `RelicEffects`
    /// and was missed the first time: six of its ids were FABRICATED -- 1332, 1363, 1394,
    /// 1399, 1510, 1533, none of which is a relic at all -- so five relics had written
    /// implementations that nothing could ever reach.
    /// </summary>
    public static TheoryData<string> Files() =>
        new()
        {
            "src/Sts2Emulator/Core/Effects/RelicEffects.cs",
            "src/Sts2Emulator/Core/Run/RunConstants.cs",
        };

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryIdConstantMatchesTheExtractedTable(string relativePath)
    {
        var table = GeneratedData
            .Relics.All.ToArray()
            .ToDictionary(def => def.Name, def => def.Id);
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar))
        );

        var wrong = Declaration
            .Matches(source)
            // `RunConstants` prefixes them; `RelicEffects` does not.
            .Select(m => (Name: StripRelicPrefix(m.Groups[1].Value), Value: int.Parse(m.Groups[2].Value)))
            .Where(pair => table.ContainsKey(pair.Name))
            .Where(pair => table[pair.Name] != pair.Value)
            .Select(pair => $"{pair.Name} = {pair.Value}, table says {table[pair.Name]}")
            .ToList();

        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    /// <summary>
    /// A `Relic`-prefixed constant whose value is not a relic id at all. Every one of
    /// these was a relic somebody implemented and nothing could reach.
    /// </summary>
    [Fact]
    public void NoRelicConstantNamesAnIdThatDoesNotExist()
    {
        var ids = GeneratedData.Relics.All.ToArray().Select(def => def.Id).ToHashSet();
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                RepoRoot(),
                System.IO.Path.Combine("src", "Sts2Emulator", "Core", "Run", "RunConstants.cs")
            )
        );

        var phantom = Declaration
            .Matches(source)
            .Where(m => m.Groups[1].Value.StartsWith("Relic", System.StringComparison.Ordinal))
            .Where(m => m.Groups[1].Value != "RelicSlotSize")
            .Where(m => !ids.Contains(int.Parse(m.Groups[2].Value)))
            .Select(m => $"{m.Groups[1].Value} = {m.Groups[2].Value}")
            .ToList();

        Assert.True(phantom.Count == 0, string.Join("; ", phantom));
    }

    private static string StripRelicPrefix(string name) =>
        name.StartsWith("Relic", System.StringComparison.Ordinal) && name.Length > 5
            ? name["Relic".Length..]
            : name;

    /// <summary>
    /// And the reverse: two names must not share an id. A copy-pasted constant that kept
    /// its neighbour's value is the same defect written the other way round.
    /// </summary>
    [Fact]
    public void NoTwoRelicConstantsShareAnId()
    {
        var byId = typeof(RelicEffects)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .Select(f => (f.Name, Id: (int)f.GetRawConstantValue()!))
            // Only the ones that name a real relic; the vars beside them are amounts.
            .Where(pair => GeneratedData.Relics.FindId(pair.Name) == pair.Id)
            .GroupBy(pair => pair.Id)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join("/", group.Select(p => p.Name))}")
            .ToList();

        Assert.True(byId.Count == 0, string.Join("; ", byId));
    }

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "README.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
