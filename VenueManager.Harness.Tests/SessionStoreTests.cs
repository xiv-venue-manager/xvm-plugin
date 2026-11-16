using VenueManager.Harness;
using Xunit;

public class SessionStoreTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "vm-session-" + Guid.NewGuid().ToString("N"));

    private SessionStore Store => new(Path.Combine(dir, "nested", "dev-session.json"));

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public void Save_then_load_round_trips_and_creates_the_directory()
    {
        Store.Save("abc", "Ehno");

        Assert.Equal("abc", Store.LoadSecret());
    }

    [Fact]
    public void A_missing_file_loads_null() => Assert.Null(Store.LoadSecret());

    [Fact]
    public void Corrupt_json_loads_null()
    {
        var store = Store;
        Directory.CreateDirectory(Path.GetDirectoryName(store.Path)!);
        File.WriteAllText(store.Path, "{not json");

        Assert.Null(store.LoadSecret());
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = Store;
        store.Save("abc", null);

        store.Delete();

        Assert.False(File.Exists(store.Path));
        Assert.Null(store.LoadSecret());
    }

    [Fact]
    public void The_file_is_owner_read_write_only_on_unix()
    {
        if (OperatingSystem.IsWindows()) return;
        var store = Store;
        store.Save("abc", null);
        store.Save("def", null);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.Path));
        Assert.Equal("def", store.LoadSecret());
    }
}
