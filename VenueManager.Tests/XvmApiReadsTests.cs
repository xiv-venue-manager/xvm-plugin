using System.Net;
using System.Text;
using VenueManager;
using Xunit;

public class XvmApiReadsTests
{
    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Json)> _routes = new();
        public List<(string PathAndQuery, string? Authorization)> Requests { get; } = new();

        public RoutedHandler On(string pathPrefix, string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _routes[pathPrefix] = (status, json);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            Requests.Add((pathAndQuery, request.Headers.Authorization?.ToString()));
            var route = _routes.Where(r => pathAndQuery.StartsWith(r.Key)).OrderByDescending(r => r.Key.Length).FirstOrDefault();
            var (status, json) = route.Key is null ? (HttpStatusCode.NotFound, "{}") : route.Value;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static XvmApiReads Reads(RoutedHandler handler) => new(new HttpClient(handler), "https://api.test/", "s3cret");

    [Fact]
    public async Task Venues_come_from_me_venues_with_the_bearer_secret()
    {
        var handler = new RoutedHandler().On("/me/venues", "[{\"venue\":{\"id\":\"v1\",\"name\":\"Lotus\",\"slug\":\"lotus\"},\"tier\":\"owner\",\"effective_tier\":\"owner\"}]");

        var venues = await Reads(handler).GetVenuesAsync();

        Assert.Single(venues);
        Assert.Equal("v1", venues[0].Id);
        Assert.Equal("OWNER", venues[0].Role);
        Assert.Equal("Bearer s3cret", handler.Requests[0].Authorization);
        Assert.Equal("/me/venues", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task Shifts_ask_for_mine_and_open_over_a_day_back_to_two_weeks_ahead()
    {
        var handler = new RoutedHandler()
            .On("/venues/v1/shifts?from=2026-10-09T12%3A00%3A00Z&to=2026-10-24T12%3A00%3A00Z&mine=true", "[{\"id\":7,\"position_id\":null,\"scheduled_start\":\"2026-10-10T19:00:00Z\",\"scheduled_end\":\"2026-10-10T23:00:00Z\",\"actual_start\":null,\"actual_end\":null,\"notes\":null,\"status\":\"scheduled\"}]")
            .On("/venues/v1/event-shifts?from=2026-10-09T12%3A00%3A00Z&to=2026-10-24T12%3A00%3A00Z&open_only=true", "[{\"id\":20,\"starts_at\":\"2026-10-11T19:00:00Z\",\"ends_at\":\"2026-10-11T23:00:00Z\",\"position_name\":\"Shout runner\",\"capacity\":2,\"taken\":0,\"pending\":0}]");

        var shifts = await Reads(handler).GetShiftsAsync("v1", Now);

        Assert.Equal("SCHEDULED", shifts.Shifts[0].Status);
        Assert.Equal("Shout runner", shifts.OpenShifts[0].RoleName);
    }

    [Fact]
    public async Task Roles_are_the_positions_listed_on_the_callers_me_venues_row()
    {
        var handler = new RoutedHandler()
            .On("/me/venues", "[{\"venue\":{\"id\":\"v1\",\"name\":\"Lotus\",\"slug\":\"lotus\",\"inventory_enabled\":false},\"tier\":\"staff\",\"effective_tier\":\"staff\",\"membership_id\":7,\"position_ids\":[4]},{\"venue\":{\"id\":\"v2\",\"name\":\"Moon\",\"slug\":\"moon\",\"inventory_enabled\":false},\"tier\":\"staff\",\"effective_tier\":\"staff\",\"membership_id\":8,\"position_ids\":[3]}]")
            .On("/venues/v1/positions", "[{\"id\":3,\"name\":\"Bartender\"},{\"id\":4,\"name\":\"Host\"}]");

        var roles = await Reads(handler).GetRolesAsync("v1");

        Assert.Equal(new[] { "Host" }, roles.Select(r => r.Name));
        Assert.DoesNotContain(handler.Requests, r => r.PathAndQuery.Contains("/memberships"));
    }

    [Fact]
    public async Task An_old_api_without_position_ids_gives_no_roles()
    {
        var handler = new RoutedHandler()
            .On("/me/venues", "[{\"venue\":{\"id\":\"v1\",\"name\":\"Lotus\",\"slug\":\"lotus\"},\"tier\":\"staff\",\"effective_tier\":\"staff\"}]")
            .On("/venues/v1/positions", "[{\"id\":3,\"name\":\"Bartender\"}]");

        Assert.Empty(await Reads(handler).GetRolesAsync("v1"));
    }

    [Fact]
    public async Task Venue_addresses_come_from_each_venues_detail()
    {
        var handler = new RoutedHandler()
            .On("/venues/v1", "{\"id\":\"v1\",\"world\":\"Raiden\",\"district\":\"Lavender Beds\",\"ward\":6,\"plot\":6,\"room\":null,\"subdivision\":null}")
            .On("/venues/v2", "{\"id\":\"v2\",\"world\":\"Raiden\",\"district\":\"Mist\",\"ward\":12,\"plot\":null,\"room\":45,\"subdivision\":true}");

        var addresses = await Reads(handler).GetVenueAddressesAsync(new[] { "v1", "v2" });

        Assert.Equal(2, addresses.Count);
        Assert.Equal(6, addresses.Single(a => a.VenueId == "v1").Plot);
        Assert.Equal(true, addresses.Single(a => a.VenueId == "v2").Subdivision);
    }

    [Fact]
    public async Task Services_join_their_category_names()
    {
        var handler = new RoutedHandler()
            .On("/venues/v1/services/categories", "[{\"id\":2,\"name\":\"Drinks\",\"sort_order\":0}]")
            .On("/venues/v1/services", "[{\"id\":11,\"name\":\"House Cocktail\",\"description\":null,\"price_minor\":1500,\"category_id\":2,\"inventory\":null}]");

        var result = await Reads(handler).GetServicesAsync("v1");

        Assert.Equal("Drinks", result.Services[0].Category);
        Assert.Equal("1500", result.Services[0].Price);
    }

    [Fact]
    public async Task Inventory_follows_the_venues_inventory_enabled_flag()
    {
        var handler = new RoutedHandler()
            .On("/me/venues", "[{\"venue\":{\"id\":\"v1\",\"name\":\"Lotus\",\"slug\":\"lotus\",\"inventory_enabled\":true},\"tier\":\"staff\",\"effective_tier\":\"staff\",\"membership_id\":7,\"position_ids\":[]},{\"venue\":{\"id\":\"v2\",\"name\":\"Moon\",\"slug\":\"moon\"},\"tier\":\"owner\",\"effective_tier\":\"owner\",\"membership_id\":8,\"position_ids\":[]}]");
        var reads = Reads(handler);

        Assert.True(await reads.GetInventoryEnabledAsync("v1"));
        Assert.False(await reads.GetInventoryEnabledAsync("v2"));
        Assert.False(await reads.GetInventoryEnabledAsync("unknown"));
    }

    [Fact]
    public async Task Bans_active_event_and_rooms_map_through()
    {
        var handler = new RoutedHandler()
            .On("/venues/v1/patrons/banned", "[{\"character_name\":\"Some Person\",\"world\":\"Cactuar\",\"ban_reason\":null}]")
            .On("/venues/v1/events/active", "{\"id\":42,\"title\":\"Friday Night\"}")
            .On("/venues/v2/events/active", "null")
            .On("/venues/v1/rooms", "[{\"id\":5,\"name\":\"Suite\",\"notes\":null,\"room_number\":1,\"locked\":false,\"disabled\":false,\"status\":\"reserved\"}]");
        var reads = Reads(handler);

        Assert.Equal("Some Person", (await reads.GetBannedPatronsAsync("v1"))[0].CharacterName);
        Assert.True((await reads.GetActiveEventAsync("v1")).Active);
        Assert.False((await reads.GetActiveEventAsync("v2")).Active);
        Assert.True((await reads.GetRoomsAsync("v1"))[0].IsOccupied);
    }

    [Fact]
    public async Task A_failed_call_throws_with_the_path_and_status()
    {
        var handler = new RoutedHandler().On("/me/venues", "{}", HttpStatusCode.Unauthorized);
        var ex = await Assert.ThrowsAsync<XIVAppApiException>(() => Reads(handler).GetVenuesAsync());
        Assert.Contains("/me/venues", ex.Message);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Venue_ids_are_escaped_in_paths()
    {
        var handler = new RoutedHandler().On("/venues/a%20b/rooms", "[]");
        await Reads(handler).GetRoomsAsync("a b");
        Assert.Equal("/venues/a%20b/rooms", handler.Requests[0].PathAndQuery);
    }
}
