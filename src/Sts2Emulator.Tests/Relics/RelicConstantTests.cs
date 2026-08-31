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

    [Fact]
    public void EveryIdConstantMatchesTheExtractedTable()
    {
        var table = GeneratedData
            .Relics.All.ToArray()
            .ToDictionary(def => def.Name, def => def.Id);
        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                RepoRoot(),
                "src",
                "Sts2Emulator",
                "Core",
                "Effects",
                "RelicEffects.cs"
            )
        );

        var wrong = Declaration
            .Matches(source)
            .Where(m => table.ContainsKey(m.Groups[1].Value))
            .Where(m => table[m.Groups[1].Value] != int.Parse(m.Groups[2].Value))
            .Select(m =>
                $"{m.Groups[1].Value} = {m.Groups[2].Value}, table says {table[m.Groups[1].Value]}"
            )
            .ToList();

        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

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
