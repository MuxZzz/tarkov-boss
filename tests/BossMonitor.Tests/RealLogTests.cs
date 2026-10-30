using System.Text.Json.Nodes;
using BossMonitor.Core;
using Xunit;

namespace BossMonitor.Tests;

public class RealLogTests
{
    private static string Expected => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "real-session", "expected.json"));

    [Fact]
    public void AnonymizedSessionRetainsReviewedRaidState()
    {
        using var fixture = new LogFixture("real-session");
        fixture.Poll();
        var raids = fixture.Reader.Raids;
        Assert.Equal(7, raids.Count);
        Assert.Equal(27, raids.Sum(r => r.Pmcs.Values.Sum(p => p.Count)));
        Assert.Equal(7, raids.Sum(r => r.SpecialAis.Values.Sum(s => s.Count)));
        Assert.Equal(14, raids.Sum(r => r.Scavs.Count));
        Assert.Equal(1, raids.Sum(r => r.Bosses.Values.Sum(b => b.Count)));
        Assert.Equal(JsonNode.Parse(Expected)!.ToJsonString(), JsonNode.Parse(LogFixture.Snapshot(raids))!.ToJsonString());
    }

    [Fact]
    public void RepeatedPollAndReevaluateDoNotChangeRecordedResults()
    {
        using var fixture = new LogFixture("real-session");
        fixture.Poll();
        string before = LogFixture.Snapshot(fixture.Reader.Raids);
        fixture.Poll();
        Assert.Equal(before, LogFixture.Snapshot(fixture.Reader.Raids));
        fixture.Reader.Reevaluate(fixture.Settings);
        Assert.Equal(before, LogFixture.Snapshot(fixture.Reader.Raids));
    }

    [ArchivedLogsFact]
    public void PrivateArchiveMatchesAnonymizedExpectedState()
    {
        string root = Environment.GetEnvironmentVariable("TARKOV_BOSS_TEST_LOG_ROOT")!;
        var reader = new LogReader();
        LogFixture.Replay(reader, root);
        Assert.Equal(JsonNode.Parse(Expected)!.ToJsonString(), JsonNode.Parse(LogFixture.Snapshot(reader.Raids))!.ToJsonString());
    }
}

public sealed class ArchivedLogsFactAttribute : FactAttribute
{
    public ArchivedLogsFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TARKOV_BOSS_TEST_LOG_ROOT")))
            Skip = "Optional private archive: set TARKOV_BOSS_TEST_LOG_ROOT to work/test-logs.";
    }
}
