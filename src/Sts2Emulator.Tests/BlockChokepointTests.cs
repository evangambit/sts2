using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Sts2Emulator.Tests;

/// <summary>
/// Gold and stars each turned out to be gained by a bare <c>+=</c> at a dozen places while
/// the game gains them at one, and each time that hid a hook (E216, E220). Block was the
/// third resource checked. It already HAD its chokepoint — <c>CardEffects.GainBlock</c>,
/// which is where Dexterity, Frail, Shadowmeld, Unmovable, Vambrace and Juggernaut all
/// live — with exactly one bypass, Feel No Pain's.
///
/// This is the guard rather than the fix: the fix is a one-line call, and nothing about it
/// stops the next `PlayerBlock +=` being written.
/// </summary>
public class BlockChokepointTests
{
    [Fact]
    public void NothingAddsToPlayerBlockOutsideGainBlock()
    {
        var offenders = SourceFiles()
            .SelectMany(path =>
                Regex
                    .Matches(File.ReadAllText(path), @"PlayerBlock\s*\+=")
                    .Select(m => $"{Path.GetFileName(path)}: {m.Value}")
            )
            .ToList();

        // The one legitimate site is the chokepoint's own write.
        Assert.Single(offenders);
        Assert.StartsWith("CardEffects.cs", offenders[0]);
    }

    /// <summary>
    /// The same question for the two resources that DID need one built, so a new gain site
    /// written the old way fails here rather than silently skipping a hook.
    /// </summary>
    [Theory]
    [InlineData(@"\.Stars\s*\+=", 1)]
    [InlineData(@"PlayerGold\s*\+=", 1)]
    [InlineData(@"State\.Gold\s*\+=|state\.Gold\s*\+=", 1)]
    // Energy has TWO: the chokepoint's own write, and turn one's reset catching up with a
    // +1-energy relic's new maximum, which is the reset path and not a gain.
    [InlineData(@"\.Energy\s*\+=|\.Energy\+\+", 2)]
    public void TheOtherChokepointsHaveExactlyOneWriter(string pattern, int expected)
    {
        var offenders = SourceFiles()
            .SelectMany(path =>
                Regex
                    .Matches(File.ReadAllText(path), pattern)
                    .Select(m => $"{Path.GetFileName(path)}: {m.Value}")
            )
            .ToList();

        Assert.Equal(expected, offenders.Count);
    }

    private static string[] SourceFiles()
    {
        string core = Path.Combine(RepoRoot(), "src", "Sts2Emulator", "Core");
        var files = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        // StateCloning copies every field by name; it is not a gain.
        return files.Where(f => Path.GetFileName(f) != "StateCloning.cs").ToArray();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HANDOFF.md")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
