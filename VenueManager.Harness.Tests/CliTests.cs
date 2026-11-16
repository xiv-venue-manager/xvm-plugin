using VenueManager.Harness;
using Xunit;

public class CliTests
{
    private static string Run(string script, HarnessOptions? options = null)
    {
        using var host = PluginHost.Start(options ?? new HarnessOptions());
        var output = new StringWriter();
        new Cli(host).Run(new StringReader(script), output);
        return output.ToString();
    }

    [Fact]
    public void Open_then_tree_prints_the_screen()
    {
        var output = Run("open Main\ntree\nquit\n");

        Assert.Contains("Welcome to XIV Venue Manager Sync!", output);
    }

    [Fact]
    public void Tabs_lists_every_tab_one_per_line()
    {
        var output = Run("tabs\nquit\n");

        Assert.Contains("My Shift", output);
        Assert.Contains("Settings", output);
    }

    [Fact]
    public void Tab_reports_the_selection_and_unknown_tabs()
    {
        var output = Run("open Main\ntab My Shift\ntab Nope\nquit\n", new HarnessOptions("http://127.0.0.1:1", "test-secret"));

        Assert.Contains("tab: My Shift", output);
        Assert.Contains("no tab: Nope", output);
    }

    [Fact]
    public void Tap_reports_a_missing_label()
    {
        var output = Run("open Main\ntap Nothing Here\nquit\n");

        Assert.Contains("not found: Nothing Here", output);
    }

    [Fact]
    public void Command_reports_whether_a_handler_ran()
    {
        var output = Run("command /definitely-not-a-command\nquit\n");

        Assert.Contains("no handler", output);
    }

    [Fact]
    public void An_unknown_verb_is_reported()
    {
        var output = Run("frobnicate\nquit\n");

        Assert.Contains("unknown command: frobnicate", output);
    }

    [Fact]
    public void Quit_stops_reading()
    {
        var output = Run("quit\ntabs\n");

        Assert.DoesNotContain("Settings", output);
    }

    [Fact]
    public void Shot_writes_a_png_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "vm-shot-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var output = Run($"open Main\nshot {path}\nquit\n");

            Assert.Contains($"wrote {path}", output);
            Assert.Equal(0x89, File.ReadAllBytes(path)[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
