using VenueManager.Harness;
using Xunit;

public class PluginHostTests
{
    private static readonly string[] TabNames =
        { "Patrons", "Sales", "History", "My Shift", "Rooms", "Bans", "Inventory", "Venues", "Settings" };

    private static HarnessOptions Linked => new("http://127.0.0.1:1", "test-secret");

    [Fact]
    public void Draws_the_main_window_with_the_setup_text()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        host.Open("Main");
        host.Step(4);

        var tree = host.Tree();
        Assert.Contains(tree, line => line.Contains("XIV Venue Manager"));
        Assert.Contains(tree, line => line.Contains("Welcome to XIV Venue Manager Sync!"));
    }

    [Fact]
    public void Draws_real_icons_not_question_marks()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        host.Open("Main");
        host.Step(4);

        var tree = host.Tree();
        Assert.Contains(tree, line => line.Contains("[Cog]"));
        Assert.DoesNotContain(tree, line => line.EndsWith(") ?"));
    }

    [Fact]
    public void Lists_the_plugin_tabs()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.Equal(TabNames, host.Tabs);
    }

    [Fact]
    public void Selecting_a_tab_sticks_once_an_account_is_linked()
    {
        using var host = PluginHost.Start(Linked);
        host.Open("Main");
        host.Step(3);

        var selected = host.SelectTab("My Shift");
        host.Step(3);

        Assert.True(selected);
        Assert.Equal("My Shift", host.CurrentTab);
    }

    [Fact]
    public void Selecting_an_unknown_tab_returns_false()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.False(host.SelectTab("Nope"));
    }

    [Fact]
    public void A_registered_chat_command_runs_and_an_unknown_one_does_not()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.True(host.Command("/xvm"));
        Assert.False(host.Command("/definitely-not-a-command"));
    }

    [Fact]
    public void A_secret_without_a_url_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PluginHost.Start(new HarnessOptions(null, "test-secret")));
    }

    [Fact]
    public void Sidebar_icons_draw_without_hanging()
    {
        using var host = PluginHost.Start(Linked);

        host.Open("Main");
        host.Step(4);

        Assert.True(host.Tree().Count > 5);
    }

    [Fact]
    public void Draws_symbols_not_question_marks()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        host.Open("Main");
        host.Step(4);

        var texts = host.Tree().Select(line => line[(line.IndexOf(") ") + 2)..]).ToList();
        Assert.DoesNotContain(texts, t => t == "?" || t.StartsWith("? ") || t.Contains(" ? "));
    }

    [Fact]
    public void A_production_url_in_the_options_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PluginHost.Start(new HarnessOptions("https://api.xivvenuemanager.com", "x")));
    }

    [Fact]
    public void ApplySession_links_the_plugin_and_ClearSession_unlinks_it()
    {
        using var host = PluginHost.Start(new HarnessOptions("http://127.0.0.1:1"));
        Assert.False(host.Linked);

        host.ApplySession("test-secret");
        Assert.True(host.Linked);
        Assert.Equal("test-secret", host.Configuration.xvmApiSecret);

        host.ClearSession();
        Assert.False(host.Linked);
        Assert.True(string.IsNullOrEmpty(host.Configuration.xvmApiSecret));
    }

    [Fact]
    public void ApiUrl_defaults_to_the_dev_api()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.Equal(VenueManager.XvmApiPairing.DevUrl, host.ApiUrl);
    }

    [Theory]
    [InlineData("https://dev-api.xivvenuemanager.com")]
    [InlineData("http://127.0.0.1:1")]
    [InlineData("http://localhost:8000")]
    public void CheckApiUrl_accepts_dev_and_loopback(string url) => PluginHost.CheckApiUrl(url);

    [Theory]
    [InlineData("https://api.xivvenuemanager.com")]
    [InlineData("https://example.com")]
    [InlineData("https://dev-api.xivvenuemanager.com.evil.com")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void CheckApiUrl_refuses_everything_else(string? url) =>
        Assert.Throws<ArgumentException>(() => PluginHost.CheckApiUrl(url));
}
