using VenueManager;
using Xunit;

public class VenueAddressMatcherTests
{
    private static XvmVenueAddress PlotVenue(string id, string world = "Raiden", string? district = "Lavender Beds", int ward = 6, int plot = 6) =>
        new(id, world, district, ward, plot, null, null);

    private static XvmVenueAddress ApartmentVenue(string id, int room, bool subdivision, int ward = 12) =>
        new(id, "Raiden", "Mist", ward, null, room, subdivision);

    private static readonly HouseAddress FcHouse = new("Raiden", "Lavender Beds", 6, HouseKind.Plot, 6, 0);

    [Fact]
    public void A_plot_matches_on_world_district_ward_and_plot()
    {
        Assert.Equal("v1", VenueAddressMatcher.FindUnique(FcHouse, new[] { PlotVenue("v1"), PlotVenue("v2", plot: 7) }));
    }

    [Fact]
    public void Standing_in_a_chamber_still_matches_the_plot_venue()
    {
        var inChamber = FcHouse with { Room = 3 };
        Assert.Equal("v1", VenueAddressMatcher.FindUnique(inChamber, new[] { PlotVenue("v1") }));
    }

    [Theory]
    [InlineData("Twintania", "Lavender Beds", 6, 6)]
    [InlineData("Raiden", "Mist", 6, 6)]
    [InlineData("Raiden", "Lavender Beds", 7, 6)]
    [InlineData("Raiden", "Lavender Beds", 6, 7)]
    public void Any_difference_in_the_address_is_no_match(string world, string district, int ward, int plot)
    {
        var venue = PlotVenue("v1", world, district, ward, plot);
        Assert.Null(VenueAddressMatcher.FindUnique(FcHouse, new[] { venue }));
    }

    [Fact]
    public void World_and_district_ignore_case_and_spaces()
    {
        var venue = PlotVenue("v1", " raiden ", "lavender beds");
        Assert.Equal("v1", VenueAddressMatcher.FindUnique(FcHouse, new[] { venue }));
    }

    [Fact]
    public void A_legacy_venue_with_no_district_never_matches()
    {
        Assert.Null(VenueAddressMatcher.FindUnique(FcHouse, new[] { PlotVenue("v1", district: null) }));
    }

    [Fact]
    public void An_apartment_matches_on_room_and_the_main_or_subdivision_building()
    {
        var main = new HouseAddress("Raiden", "Mist", 12, HouseKind.Apartment, -127, 45);
        var sub = main with { Kind = HouseKind.ApartmentSubdivision, Plot = -126 };
        var venues = new[] { ApartmentVenue("main", 45, false), ApartmentVenue("sub", 45, true) };

        Assert.Equal("main", VenueAddressMatcher.FindUnique(main, venues));
        Assert.Equal("sub", VenueAddressMatcher.FindUnique(sub, venues));
    }

    [Fact]
    public void An_apartment_never_matches_a_plot_venue_and_the_other_way_round()
    {
        var apartment = new HouseAddress("Raiden", "Lavender Beds", 6, HouseKind.Apartment, -127, 6);
        Assert.Null(VenueAddressMatcher.FindUnique(apartment, new[] { PlotVenue("v1") }));
        Assert.Null(VenueAddressMatcher.FindUnique(FcHouse, new[] { new XvmVenueAddress("v2", "Raiden", "Lavender Beds", 6, null, 6, false) }));
    }

    [Fact]
    public void Two_venues_at_one_address_are_ambiguous_so_nothing_is_chosen()
    {
        Assert.Null(VenueAddressMatcher.FindUnique(FcHouse, new[] { PlotVenue("v1"), PlotVenue("v2") }));
    }

    [Theory]
    [InlineData(6, HouseKind.Plot)]
    [InlineData(0, HouseKind.Apartment)]
    [InlineData(-127, HouseKind.Apartment)]
    [InlineData(-126, HouseKind.ApartmentSubdivision)]
    public void The_kind_follows_the_plot_value_the_plugin_stores(int plot, HouseKind expected)
    {
        Assert.Equal(expected, VenueAddressMatcher.KindOf(plot));
    }
}
