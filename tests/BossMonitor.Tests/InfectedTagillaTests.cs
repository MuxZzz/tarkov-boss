using BossMonitor.Core;
using Xunit;

namespace BossMonitor.Tests;

public class InfectedTagillaTests
{
    [Fact]
    public void RealPatch130DecisionShowsSixPercentWithoutConfirmingSpawn()
    {
        using var f = new LogFixture("infected-tagilla-plan");
        f.Poll();
        Assert.Equal("factory4_day", f.Reader.Current.Map);
        Assert.Equal(6, f.Reader.Current.InfectedTagilla.SpawnChance);
        Assert.Equal("NotPlanned", f.Reader.Current.InfectedTagilla.Status);
        Assert.Empty(f.Reader.Current.InfectedTagilla.LifeInstances);
        Assert.Empty(f.Reader.Current.Bosses);
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 6)]
    [InlineData(true, 100)]
    public void PlanIsSeparateFromOrdinaryTagillaAndActualActivation(bool planned, int chance)
    {
        using var f = new LogFixture();
        f.Start("factory4_day");
        f.Append("09:00:01.000", "Init Boss wave:bossTagilla ShallSpawn:False BossChance:0");
        f.Append("09:00:01.001", $"Init Boss wave:infectedTagilla ShallSpawn:{planned} BossChance:{chance}");
        f.Poll();
        Assert.Equal(planned ? "Planned" : "NotPlanned", f.Reader.Current.InfectedTagilla.Status);
        Assert.Equal(chance, f.Reader.Current.InfectedTagilla.SpawnChance);
        Assert.Empty(f.Reader.Current.InfectedTagilla.LifeInstances);
        Assert.Equal("NotPlanned", f.Reader.Current.Bosses["bossTagilla"].Status);

        f.Append("09:00:02.000", "Bot activate id:20 role:infectedTagilla profileId:eventBoss pos:(1, 2, 3)");
        f.Append("09:00:02.001", "Bot activate id:20 role:infectedTagilla profileId:eventBoss pos:(1, 2, 3)");
        f.Poll();
        Assert.Equal("Confirmed", f.Reader.Current.InfectedTagilla.Status);
        Assert.Single(f.Reader.Current.InfectedTagilla.LifeInstances);
        Assert.Equal(chance, f.Reader.Current.InfectedTagilla.SpawnChance);
        Assert.Equal(new DateTime(2000, 1, 1, 9, 0, 2), f.Reader.Current.InfectedTagilla.At);
    }

    [Fact]
    public void EventBossDeathIsNotAssignedToOrdinaryTagillaOrScreamers()
    {
        using var f = new LogFixture();
        f.Start("factory4_day");
        f.Append("09:00:01.000", "Bot activate id:20 role:infectedTagilla profileId:eventBoss pos:(1, 2, 3)");
        f.Append("09:00:01.001", "Bot activate id:21 role:bossTagilla profileId:normalBoss pos:(4, 5, 6)");
        f.Append("09:00:01.002", "Bot activate id:22 role:infectedScreamer profileId:infectedA pos:(7, 8, 9)");
        f.Append("09:00:02.000", "bot dispose:20");
        f.Append("09:00:02.001", "BotDied pos:(10, 11, 12)");
        f.Poll();
        var boss = Assert.Single(f.Reader.Current.InfectedTagilla.LifeInstances);
        Assert.Equal("DeadConfirmed", boss.LifeStatus);
        Assert.Equal("10, 11, 12", boss.DeathPosition);
        Assert.Null(Assert.Single(f.Reader.Current.Bosses["bossTagilla"].LifeInstances).DiedAt);
        Assert.Null(Assert.Single(f.Reader.Current.InfectedScreamer.LifeInstances).DiedAt);
    }

    [Fact]
    public void LatePlanDoesNotDowngradeActivationAndRaidEndResetsEventBoss()
    {
        using var f = new LogFixture();
        f.Start("factory4_day");
        f.Append("09:00:01.000", "Bot activate id:20 role:infectedTagilla profileId:eventBoss pos:(1, 2, 3)");
        f.Append("09:00:02.000", "Init Boss wave:infectedTagilla ShallSpawn:False BossChance:6");
        f.Poll();
        Assert.Equal("Confirmed", f.Reader.Current.InfectedTagilla.Status);
        Assert.Equal(new DateTime(2000, 1, 1, 9, 0, 1), f.Reader.Current.InfectedTagilla.At);
        File.AppendAllText(f.Application, LogFixture.Line("09:00:03.000", "application|MatchingCompleted:0") + LogFixture.Line("09:00:03.001", "application|RaidId:nextRaid, Count:0, Locations:Woods ->"));
        f.Poll();
        Assert.Equal("DeathUnconfirmedAtEnd", Assert.Single(f.Reader.Raids[0].InfectedTagilla.LifeInstances).LifeStatus);
        Assert.Equal("Unknown", f.Reader.Current.InfectedTagilla.Status);
        Assert.Null(f.Reader.Current.InfectedTagilla.SpawnChance);
        Assert.Empty(f.Reader.Current.InfectedTagilla.LifeInstances);
    }
}
