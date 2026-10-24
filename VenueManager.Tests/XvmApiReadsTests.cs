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
    public async Task My_shifts_come_from_one_call_across_venues()
    {
        var handler = new RoutedHandler()
            .On("/me/shifts", "[{\"id\":1,\"venue_id\":\"v1\",\"status\":\"scheduled\",\"scheduled_start\":\"2026-10-10T19:00:00Z\",\"scheduled_end\":\"2026-10-10T22:00:00Z\",\"actual_start\":null,\"actual_end\":null,\"conflicts\":false},{\"id\":2,\"venue_id\":\"v2\",\"status\":\"active\",\"scheduled_start\":\"2026-10-10T16:00:00Z\",\"scheduled_end\":\"2026-10-10T19:00:00Z\",\"actual_start\":\"2026-10-10T16:05:00Z\",\"actual_end\":null,\"conflicts\":false}]");

        var shifts = await Reads(handler).GetMyShiftsAsync(new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new[] { "v1", "v2" }, shifts.Select(s => s.VenueId));
        Assert.Equal(new DateTime(2026, 10, 10, 16, 5, 0, DateTimeKind.Utc), shifts[1].ActualStart);
        Assert.Contains("from=2026-10-09T20%3A00%3A00Z", handler.Requests[0].PathAndQuery);
        Assert.Contains("to=2026-10-11T20%3A00%3A00Z", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task A_401_reports_the_link_rejected_and_a_later_success_clears_it()
    {
        var states = new List<bool>();
        var rejected = new RoutedHandler().On("/me/venues", "{}", HttpStatusCode.Unauthorized);
        await Assert.ThrowsAsync<XIVAppApiException>(() => new XvmApiReads(new HttpClient(rejected), "https://api.test/", "s3cret", states.Add).GetVenuesAsync());

        var ok = new RoutedHandler().On("/me/venues", "[]");
        await new XvmApiReads(new HttpClient(ok), "https://api.test/", "s3cret", states.Add).GetVenuesAsync();

        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task A_server_error_leaves_the_link_state_alone()
    {
        var states = new List<bool>();
        var broken = new RoutedHandler().On("/me/venues", "{}", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<XIVAppApiException>(() => new XvmApiReads(new HttpClient(broken), "https://api.test/", "s3cret", states.Add).GetVenuesAsync());

        Assert.Empty(states);
    }

    [Fact]
    public async Task My_characters_come_from_me_characters_primary_first()
    {
        var handler = new RoutedHandler().On("/me/characters", "[{\"id\":3,\"character_name\":\"Ehno Cure\",\"world\":\"Raiden\",\"is_primary\":true},{\"id\":4,\"character_name\":\"Alt Name\",\"world\":\"Twintania\",\"is_primary\":false}]");

        var characters = await Reads(handler).GetMyCharactersAsync();

        Assert.Equal(new[] { "Ehno Cure", "Alt Name" }, characters.Select(c => c.CharacterName));
        Assert.True(characters[0].IsPrimary);
        Assert.Equal("/me/characters", handler.Requests[0].PathAndQuery);
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
    public async Task Inventory_follows_the_venues_inventory_module()
    {
        var handler = new RoutedHandler()
            .On("/venues/v1/modules", "[{\"module\":\"rooms\",\"enabled\":true},{\"module\":\"inventory\",\"enabled\":true}]")
            .On("/venues/v2/modules", "[{\"module\":\"rooms\",\"enabled\":true},{\"module\":\"inventory\",\"enabled\":false}]")
            .On("/venues/v3/modules", "[{\"module\":\"rooms\",\"enabled\":true}]");
        var reads = Reads(handler);

        Assert.True(await reads.GetInventoryEnabledAsync("v1"));
        Assert.False(await reads.GetInventoryEnabledAsync("v2"));
        Assert.False(await reads.GetInventoryEnabledAsync("v3"));
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
