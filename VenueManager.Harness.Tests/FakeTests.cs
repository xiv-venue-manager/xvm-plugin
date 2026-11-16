using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VenueManager.Harness;
using Xunit;

public class FakeTests
{
    public FakeTests() => Fake.Reset();

    [Fact]
    public void An_override_answers_a_property()
    {
        Fake.Overrides["IClientState.get_TerritoryType"] = _ => 7u;

        var state = Fake.Make<IClientState>();

        Assert.Equal(7u, state.TerritoryType);
    }

    [Fact]
    public void A_property_that_returns_an_interface_gets_a_nested_fake()
    {
        var plugin = Fake.Make<IDalamudPluginInterface>();

        Assert.NotNull(plugin.UiBuilder);
    }

    [Fact]
    public void An_event_handler_is_stored_and_can_be_raised()
    {
        var framework = Fake.Make<IFramework>();
        var runs = 0;
        framework.Update += _ => runs++;

        ((Fake)(object)framework).Raise("Update", framework);

        Assert.Equal(1, runs);
    }

    [Fact]
    public void A_removed_event_handler_is_not_raised()
    {
        var framework = Fake.Make<IFramework>();
        var runs = 0;
        IFramework.OnUpdateDelegate handler = _ => runs++;
        framework.Update += handler;
        framework.Update -= handler;

        ((Fake)(object)framework).Raise("Update", framework);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void Every_call_is_recorded()
    {
        var state = Fake.Make<IClientState>();

        _ = state.TerritoryType;
        _ = state.TerritoryType;

        Assert.Equal(2, Fake.Calls["IClientState.get_TerritoryType"]);
    }
}
