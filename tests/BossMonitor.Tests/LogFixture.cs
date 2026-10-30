using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BossMonitor.Core;

namespace BossMonitor.Tests;

internal sealed class LogFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "BossMonitor.Tests", Guid.NewGuid().ToString("N"));
    public string Session { get; }
    public LogReader Reader { get; } = new();
    public AppSettings Settings { get; } = new();
    public string Ai => Path.Combine(Session, "fixture aiData_000.log");
    public string Application => Path.Combine(Session, "fixture application_000.log");
    public string Output => Path.Combine(Session, "fixture output_000.log");

    public LogFixture(string? fixture = null)
    {
        Directory.CreateDirectory(Root);
        if (fixture != null)
        {
            string source = Path.Combine(AppContext.BaseDirectory, "fixtures", fixture);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(Root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }
        Session = Directory.Exists(Path.Combine(Root, "Logs"))
            ? Directory.GetDirectories(Path.Combine(Root, "Logs")).Single()
            : Path.Combine(Root, "Logs", "log_2000.01.01_9-00-00_fixture");
        Directory.CreateDirectory(Session);
    }

    public static string Line(string time, string body) => $"2000-01-01 {time}|fixture|Trace|{body}\n";
    public void Append(string time, string body) => File.AppendAllText(Ai, Line(time, "aiData|" + body), new UTF8Encoding(false));
    public void Start(string map = "Lighthouse") => File.WriteAllText(Application,
        Line("09:00:00.000", "application|MatchingCompleted:0") +
        Line("09:00:00.001", $"application|RaidId:fixtureRaid, Count:0, Locations:{map} -> "));
    public void Poll() => Replay(Reader, Root, Settings);

    public static void Replay(LogReader reader, string root, AppSettings? settings = null)
    {
        string session = Directory.GetDirectories(Path.Combine(root, "Logs")).OrderByDescending(LogReader.SessionTime).First();
        long bytes = Directory.EnumerateFiles(session, "*.log").Sum(f => new FileInfo(f).Length);
        // Poll's public contract limits ingestion to 8 MiB per call. Do not rely on
        // localized diagnostic text or a single call for larger archived sessions.
        long polls = bytes / (8 * 1024 * 1024) + 2;
        for (long i = 0; i < polls; i++) reader.Poll(root, settings ?? new AppSettings());
    }

    public static string Snapshot(IReadOnlyList<RaidState> raids)
    {
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(raids))!;
        StripIdentity(node);
        return node.ToJsonString(new() { WriteIndented = true });
    }

    private static void StripIdentity(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            // Compare all observable business state while excluding pseudonymized
            // identifiers and the absolute session path embedded in the raid key.
            obj.Remove("Key"); obj.Remove("RaidId"); obj.Remove("Profile");
            foreach (var child in obj) if (child.Value != null) StripIdentity(child.Value);
        }
        else if (node is JsonArray array)
            foreach (var child in array) if (child != null) StripIdentity(child);
    }

    public void Dispose()
    {
        // Root is created exclusively by this test, never a supplied log directory.
        Directory.Delete(Root, recursive: true);
    }
}
