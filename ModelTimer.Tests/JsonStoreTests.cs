namespace ModelTimer.Tests;

public class JsonStoreTests : IDisposable
{
    private class Sample
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private readonly string _dir;

    public JsonStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ModelTimerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string DataPath => Path.Combine(_dir, "data.json");

    [Fact]
    public void SaveThenLoad_RoundTripsData()
    {
        var ok = JsonStore.Save(DataPath, new Sample { Name = "Luna", Count = 3 });
        Assert.True(ok);

        var loaded = JsonStore.Load<Sample>(DataPath);

        Assert.NotNull(loaded);
        Assert.Equal("Luna", loaded!.Name);
        Assert.Equal(3, loaded.Count);
    }

    [Fact]
    public void Load_OnMissingFile_ReturnsNullRatherThanThrowing()
    {
        Assert.Null(JsonStore.Load<Sample>(DataPath));
    }

    [Fact]
    public void Load_OnCorruptFile_ReturnsNullRatherThanThrowing()
    {
        File.WriteAllText(DataPath, "{ this is not valid json");

        Assert.Null(JsonStore.Load<Sample>(DataPath));
    }

    [Fact]
    public void Save_DoesNotLeaveATempFileBehind()
    {
        JsonStore.Save(DataPath, new Sample { Name = "A" });

        Assert.False(File.Exists(DataPath + ".tmp"));
    }

    [Fact]
    public void Save_OverExistingFile_CreatesABackup()
    {
        JsonStore.Save(DataPath, new Sample { Name = "First" });
        JsonStore.Save(DataPath, new Sample { Name = "Second" });

        var backupDir = Path.Combine(_dir, "backups");
        Assert.True(Directory.Exists(backupDir));
        Assert.Single(Directory.GetFiles(backupDir));
    }

    [Fact]
    public void Save_KeepsOnlyTheMostRecentFiveBackups()
    {
        for (int i = 0; i < 8; i++)
        {
            JsonStore.Save(DataPath, new Sample { Name = $"v{i}" });
            Thread.Sleep(5); // backups are timestamp-named at millisecond resolution
        }

        var backupDir = Path.Combine(_dir, "backups");
        Assert.Equal(5, Directory.GetFiles(backupDir).Length);
    }

    [Fact]
    public void RestoreLatestBackup_BringsBackThePreviousVersion()
    {
        JsonStore.Save(DataPath, new Sample { Name = "Good" });
        JsonStore.Save(DataPath, new Sample { Name = "Oops" });

        var restored = JsonStore.RestoreLatestBackup(DataPath);

        Assert.True(restored);
        var loaded = JsonStore.Load<Sample>(DataPath);
        Assert.Equal("Good", loaded!.Name);
    }

    [Fact]
    public void RestoreLatestBackup_WithNoBackupAvailable_ReturnsFalse()
    {
        JsonStore.Save(DataPath, new Sample { Name = "OnlyVersion" });

        Assert.False(JsonStore.RestoreLatestBackup(DataPath));
    }

    [Fact]
    public void GetLatestBackupTime_WithNoBackupYet_ReturnsNull()
    {
        Assert.Null(JsonStore.GetLatestBackupTime(DataPath));
    }
}
