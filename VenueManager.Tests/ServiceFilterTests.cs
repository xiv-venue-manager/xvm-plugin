using VenueManager;
using Xunit;

public class ServiceFilterTests
{
    private static Service Svc(string name, string? linked = null, string? category = null) =>
        new() { Id = name, Name = name, LinkedItemName = linked, Category = category };

    private static readonly List<Service> All = new()
    {
        Svc("Grape Juice Shot", linked: "Grape Juice", category: "Drinks"),
        Svc("Lap Dance", category: "Entertainment"),
        Svc("Mythril Ale", linked: "Mythril Ale", category: "Drinks"),
    };

    [Fact]
    public void An_empty_query_keeps_every_service_in_order()
    {
        Assert.Equal(All, ServiceFilter.Apply(All, ""));
        Assert.Equal(All, ServiceFilter.Apply(All, "   "));
    }

    [Fact]
    public void Matches_the_name_ignoring_case()
    {
        Assert.Equal(new[] { "Lap Dance" }, ServiceFilter.Apply(All, "lap").Select(s => s.Name));
    }

    [Fact]
    public void Matches_the_linked_item_name()
    {
        Assert.Equal(new[] { "Grape Juice Shot" }, ServiceFilter.Apply(All, "juice").Select(s => s.Name));
    }

    [Fact]
    public void Matches_the_category()
    {
        Assert.Equal(new[] { "Grape Juice Shot", "Mythril Ale" }, ServiceFilter.Apply(All, "drinks").Select(s => s.Name));
    }

    [Fact]
    public void A_service_with_no_linked_item_or_category_does_not_throw()
    {
        Assert.Empty(ServiceFilter.Apply(new List<Service> { Svc("Plain") }, "zzz"));
    }
}
