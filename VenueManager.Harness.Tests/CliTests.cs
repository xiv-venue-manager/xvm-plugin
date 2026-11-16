using System.Net;
using System.Text;
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

    [Fact]
    public void Wait_zero_prints_ok()
    {
        Assert.Contains("ok", Run("wait 0\nquit\n"));
    }

    [Fact]
    public void Wait_is_clamped_to_zero_through_sixty()
    {
        Assert.Equal(60, Cli.ClampWait(9999));
        Assert.Equal(0, Cli.ClampWait(-5));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly bool succeed;
        public FakeHandler(bool succeed) => this.succeed = succeed;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var (status, json) = !succeed ? (HttpStatusCode.BadRequest, "{}")
                : path == "/pairing/exchange" ? (HttpStatusCode.Created, "{\"secret\":\"s3cret-value\"}")
                : path == "/me" ? (HttpStatusCode.OK, "{\"person\":{\"display_name\":\"Ehno\"}}")
                : (HttpStatusCode.NotFound, "{}");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static string TempStorePath() => Path.Combine(Path.GetTempPath(), "vm-cli-" + Guid.NewGuid().ToString("N"), "s.json");

    private static string RunWith(PluginHost host, SessionStore? store, bool succeed, string script)
    {
        var output = new StringWriter();
        new Cli(host, store, new FakeHandler(succeed)).Run(new StringReader(script), output);
        return output.ToString();
    }

    [Fact]
    public void Link_saves_the_session_and_never_prints_the_secret_or_code()
    {
        var path = TempStorePath();
        try
        {
            using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1"));
            var store = new SessionStore(path);

            var output = RunWith(host, store, true, "link ABCD-1234\nquit\n");

            Assert.Contains("linked as Ehno", output);
            Assert.DoesNotContain("s3cret-value", output);
            Assert.DoesNotContain("ABCD-1234", output);
            Assert.True(host.Linked);
            Assert.Equal("s3cret-value", store.LoadSecret());
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { } }
    }

    [Fact]
    public void Link_without_a_store_says_not_saved()
    {
        using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1"));

        var output = RunWith(host, null, true, "link ABCD-1234\nquit\n");

        Assert.Contains("linked as Ehno (not saved)", output);
        Assert.True(host.Linked);
    }

    [Fact]
    public void A_failed_link_prints_the_reason_and_saves_nothing()
    {
        var path = TempStorePath();
        using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1"));

        var output = RunWith(host, new SessionStore(path), false, "link ABCD-1234\nquit\n");

        Assert.Contains("link failed: ", output);
        Assert.DoesNotContain("ABCD-1234", output);
        Assert.False(File.Exists(path));
        Assert.False(host.Linked);
    }

    [Fact]
    public void Unlink_deletes_the_session_and_unlinks_the_host()
    {
        var path = TempStorePath();
        try
        {
            using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1"));
            var store = new SessionStore(path);

            var output = RunWith(host, store, true, "link ABCD-1234\nunlink\nquit\n");

            Assert.Contains("unlinked", output);
            Assert.False(File.Exists(path));
            Assert.False(host.Linked);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { } }
    }

    [Fact]
    public void State_shows_the_link_status()
    {
        var output = Run("state\nquit\n");

        Assert.Contains("linked: no", output);
        Assert.Contains("link rejected: no", output);
    }

    [Fact]
    public void Venues_on_a_fresh_host_says_none_loaded()
    {
        Assert.Contains("(none loaded yet)", Run("venues\nquit\n"));
    }

    [Fact]
    public void Venues_and_venue_print_and_select()
    {
        using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1", null));
        host.Plugin.xivAppVenues.Add(new VenueManager.XIVAppVenue { Id = "id-1", Slug = "alpha-one", Name = "Alpha" });
        var output = new StringWriter();

        new Cli(host).Run(new StringReader("venues\nvenue alpha-one\nvenue nope\nquit\n"), output);

        var text = output.ToString();
        Assert.Contains("alpha-one  Alpha", text);
        Assert.Contains("venue: Alpha (alpha-one)", text);
        Assert.Contains("no venue: nope", text);
        Assert.Equal("id-1", host.Configuration.selectedVenueId);
    }
}
