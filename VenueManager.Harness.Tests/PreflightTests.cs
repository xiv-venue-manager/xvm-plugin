using VenueManager.Harness;
using Xunit;

public class PreflightTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "preflight-" + Guid.NewGuid().ToString("N"));

    private string Installed => Path.Combine(root, "installed");
    private string Cache => Path.Combine(root, "cache");

    private void Lay(string installedBytes, string cachedBytes, bool native = true)
    {
        Directory.CreateDirectory(Installed);
        Directory.CreateDirectory(Path.Combine(Cache, "dalamud"));
        Directory.CreateDirectory(Path.Combine(Cache, "native"));
        File.WriteAllText(Path.Combine(Installed, "Dalamud.Bindings.ImGui.dll"), installedBytes);
        File.WriteAllText(Path.Combine(Cache, "dalamud", "Dalamud.Bindings.ImGui.dll"), cachedBytes);
        if (native) File.WriteAllText(Path.Combine(Cache, "native", "libcimgui.so"), "native");
    }

    [Fact]
    public void Passes_when_the_bindings_are_identical()
    {
        Lay("same", "same");

        Assert.Null(Preflight.Check(Installed, Cache));
    }

    [Fact]
    public void Reports_a_mismatch_when_the_bindings_differ()
    {
        Lay("one", "two");

        Assert.Contains("differ", Preflight.Check(Installed, Cache));
    }

    [Fact]
    public void Reports_the_missing_file_by_path()
    {
        Lay("same", "same", native: false);

        Assert.Contains("libcimgui.so", Preflight.Check(Installed, Cache));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
