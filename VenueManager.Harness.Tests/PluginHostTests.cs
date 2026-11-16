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
}
