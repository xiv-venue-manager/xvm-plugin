using System.Net;
using System.Text;
using System.Text.Json;
using VenueManager;
using Xunit;

public class XvmApiWritesTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly List<(string Method, string Prefix, HttpStatusCode Status, string Json)> _routes = new();
        public List<(string Method, string Path, string Body, string? Auth)> Requests { get; } = new();

        public Handler On(string method, string prefix, string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _routes.Add((method, prefix, status, json));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, path, body, request.Headers.Authorization?.ToString()));
            var hit = _routes.Where(r => r.Method == request.Method.Method && path.StartsWith(r.Prefix)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            if (hit.Prefix is null) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
            return new HttpResponseMessage(hit.Status) { Content = new StringContent(hit.Json, Encoding.UTF8, "application/json") };
        }
    }

    private static XvmApiWrites Writes(Handler handler) => new(new HttpClient(handler), "https://api.test/", "s3cret", TimeSpan.Zero);

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact]
    public async Task Claim_joins_the_event_shift_and_reports_the_pending_status()
    {
        var handler = new Handler().On("POST", "/venues/v1/event-shifts/42/join", "{\"status\":\"pending_approval\",\"worked_minutes\":null}");

        var result = await Writes(handler).ClaimShiftAsync("v1", "42");

        Assert.True(result.Success);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("Bearer s3cret", handler.Requests[0].Auth);
    }

    [Fact]
    public async Task A_refused_clock_in_carries_the_servers_sentence()
    {
        var handler = new Handler().On("POST", "/venues/v1/shifts/42/clock-in", "{\"detail\":\"Pending approval.\"}", HttpStatusCode.Conflict);

        var result = await Writes(handler).ClockInAsync("v1", "42");

        Assert.False(result.Success);
        Assert.Equal("Pending approval.", result.Error);
    }

    [Fact]
    public async Task Clock_out_reports_hours_from_worked_minutes()
    {
        var handler = new Handler().On("POST", "/venues/v1/shifts/42/clock-out", "{\"status\":\"completed\",\"worked_minutes\":135}");

        var result = await Writes(handler).ClockOutAsync("v1", "42");

        Assert.True(result.Success);
        Assert.Equal(2.25, result.HoursWorked);
        Assert.Equal("COMPLETED", result.Status);
    }

    [Fact]
    public async Task A_visit_posts_the_door_fields_and_a_duplicate_still_counts()
    {
        var handler = new Handler().On("POST", "/venues/v1/patrons/visits", "{\"id\":1,\"deduped\":true}", HttpStatusCode.OK);

        var ok = await Writes(handler).LogPatronVisitAsync("v1", "Ehno Cure", "Twintania", "enter", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(ok);
        var body = Json(handler.Requests[0].Body);
        Assert.Equal("Ehno Cure", body.GetProperty("character_name").GetString());
        Assert.Equal("enter", body.GetProperty("action").GetString());
        Assert.Equal("2026-10-10T12:00:00Z", body.GetProperty("ts").GetString());
    }

    [Fact]
    public async Task A_sale_sends_the_event_a_key_and_whole_gil()
    {
        var handler = new Handler()
            .On("GET", "/venues/v1/events/active", "{\"id\":9,\"title\":\"Friday\"}")
            .On("POST", "/venues/v1/finance/transactions", "{\"id\":5,\"service_id\":11}", HttpStatusCode.Created)
            .On("GET", "/venues/v1/services/11", "{\"id\":11,\"inventory\":{\"stock_count\":7}}");

        var result = await Writes(handler).LogTransactionAsync("v1", "11", 1500, "Guest", null, null);

        Assert.True(result.Success);
        Assert.Equal("11", result.ServiceId);
        Assert.Equal(7, result.ServiceStockCount);
        var body = Json(handler.Requests.Single(r => r.Method == "POST").Body);
        Assert.Equal("sale", body.GetProperty("kind").GetString());
        Assert.Equal(1500, body.GetProperty("amount").GetInt32());
        Assert.Equal(11, body.GetProperty("service_id").GetInt32());
        Assert.Equal(9, body.GetProperty("event_id").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("idempotency_key").GetString()));
        Assert.False(body.TryGetProperty("notes", out _));
    }

    [Fact]
    public async Task A_tip_with_no_service_skips_the_stock_read()
    {
        var handler = new Handler()
            .On("GET", "/venues/v1/events/active", "{}", HttpStatusCode.NotFound)
            .On("POST", "/venues/v1/finance/transactions", "{\"id\":5,\"service_id\":null}", HttpStatusCode.Created);

        var result = await Writes(handler).LogTransactionAsync("v1", null, 200, null, null, "TIP");

        Assert.True(result.Success);
        Assert.Null(result.ServiceStockCount);
        var body = Json(handler.Requests.Single(r => r.Method == "POST").Body);
        Assert.Equal("tip", body.GetProperty("kind").GetString());
        Assert.False(body.TryGetProperty("event_id", out _));
        Assert.DoesNotContain(handler.Requests, r => r.Path.Contains("/inventory"));
    }

    [Fact]
    public async Task Out_of_stock_shows_the_servers_sentence()
    {
        var handler = new Handler()
            .On("GET", "/venues/v1/events/active", "{}", HttpStatusCode.NotFound)
            .On("POST", "/venues/v1/finance/transactions", "{\"detail\":\"House Cocktail is out of stock.\"}", HttpStatusCode.Conflict);

        var result = await Writes(handler).LogTransactionAsync("v1", "11", 1500, null, null, null);

        Assert.False(result.Success);
        Assert.Equal("House Cocktail is out of stock.", result.Error);
    }

    [Fact]
    public async Task A_ban_posts_name_world_and_reason()
    {
        var handler = new Handler().On("POST", "/venues/v1/patrons/bans", "{\"id\":3}", HttpStatusCode.Created);

        var result = await Writes(handler).BanPatronAsync("v1", "Trouble Maker", "Twintania", "Harassment");

        Assert.True(result.Success);
        Assert.Equal("Harassment", Json(handler.Requests[0].Body).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_staff_ban_says_manager_needed()
    {
        var handler = new Handler().On("POST", "/venues/v1/patrons/bans", "{\"detail\":\"Insufficient role for this action.\"}", HttpStatusCode.Forbidden);

        var result = await Writes(handler).BanPatronAsync("v1", "Trouble Maker", "Twintania", "Harassment");

        Assert.False(result.Success);
        Assert.Equal("You need to be a manager at this venue to do that.", result.Error);
    }

    [Fact]
    public async Task Reserve_holds_the_room_for_the_caller_for_the_chosen_minutes_and_reads_me_once()
    {
        var handler = new Handler()
            .On("GET", "/me", "{\"person\":{\"id\":2}}")
            .On("POST", "/venues/v1/rooms/4/reservations", "{\"id\":8}", HttpStatusCode.Created);
        var writes = Writes(handler);
        var now = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.True((await writes.ReserveRoomAsync("v1", "4", 90, now)).Success);
        Assert.True((await writes.ReserveRoomAsync("v1", "4", 30, now)).Success);

        var first = Json(handler.Requests.First(r => r.Method == "POST").Body);
        Assert.Equal(2, first.GetProperty("reserved_person_id").GetInt32());
        Assert.Equal("2026-10-10T12:00:00Z", first.GetProperty("start_at").GetString());
        Assert.Equal("2026-10-10T13:30:00Z", first.GetProperty("end_at").GetString());
        Assert.Equal("plugin_manual", first.GetProperty("source").GetString());
        Assert.Equal(1, handler.Requests.Count(r => r.Path == "/me"));
    }

    [Fact]
    public async Task A_taken_room_shows_the_servers_sentence()
    {
        var handler = new Handler()
            .On("GET", "/me", "{\"person\":{\"id\":2}}")
            .On("POST", "/venues/v1/rooms/4/reservations", "{\"detail\":\"That time overlaps an existing reservation for this room.\"}", HttpStatusCode.Conflict);

        var result = await Writes(handler).ReserveRoomAsync("v1", "4", 60, DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.Equal("That time overlaps an existing reservation for this room.", result.Error);
    }

    [Fact]
    public async Task Release_and_lock_and_disable_hit_their_routes()
    {
        var handler = new Handler()
            .On("POST", "/venues/v1/rooms/4/release", "{\"id\":8}")
            .On("PATCH", "/venues/v1/rooms/4", "{\"id\":4}");
        var writes = Writes(handler);

        Assert.True((await writes.ReleaseRoomAsync("v1", "4")).Success);
        Assert.True((await writes.UpdateRoomAsync("v1", "4", locked: true, disabled: null)).Success);

        var patch = Json(handler.Requests.Single(r => r.Method == "PATCH").Body);
        Assert.True(patch.GetProperty("locked").GetBoolean());
        Assert.False(patch.TryGetProperty("disabled", out _));
    }

    [Fact]
    public async Task Link_and_stock_count_hit_the_inventory_routes()
    {
        var handler = new Handler()
            .On("PUT", "/venues/v1/services/11/inventory/stock", "{\"stock_count\":12}")
            .On("PUT", "/venues/v1/services/11/inventory", "{\"service_id\":11}");
        var writes = Writes(handler);

        Assert.True((await writes.LinkItemAsync("v1", "11", 4825, "Grilled Tuna", 21545)).Success);
        Assert.True((await writes.RestockAsync("v1", "11", 12)).Success);

        Assert.Equal(4825, Json(handler.Requests.First(r => r.Path == "/venues/v1/services/11/inventory").Body).GetProperty("linked_item_id").GetInt32());
        Assert.Equal(12, Json(handler.Requests.First(r => r.Path.EndsWith("/stock")).Body).GetProperty("stock_count").GetInt32());
    }

    [Fact]
    public async Task A_401_on_a_write_reports_the_link_rejected_and_a_success_clears_it()
    {
        var states = new List<bool>();
        var rejected = new Handler().On("POST", "/venues/v1/shifts/42/clock-in", "{\"detail\":\"Invalid credential\"}", HttpStatusCode.Unauthorized);
        var first = await new XvmApiWrites(new HttpClient(rejected), "https://api.test/", "s3cret", TimeSpan.Zero, states.Add).ClockInAsync("v1", "42");

        var ok = new Handler().On("POST", "/venues/v1/shifts/42/clock-in", "{\"status\":\"active\"}");
        var second = await new XvmApiWrites(new HttpClient(ok), "https://api.test/", "s3cret", TimeSpan.Zero, states.Add).ClockInAsync("v1", "42");

        Assert.False(first.Success);
        Assert.True(second.Success);
        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task Linking_a_character_posts_name_and_world_and_says_whether_it_was_new()
    {
        var handler = new Handler().On("POST", "/me/characters", "{\"id\":3,\"character_name\":\"Ehno Cure\",\"world\":\"Raiden\",\"is_primary\":true}", HttpStatusCode.Created);
        var created = await Writes(handler).LinkCharacterAsync("Ehno Cure", "Raiden");

        var again = new Handler().On("POST", "/me/characters", "{\"id\":3,\"character_name\":\"Ehno Cure\",\"world\":\"Raiden\",\"is_primary\":true}", HttpStatusCode.OK);
        var existing = await Writes(again).LinkCharacterAsync("Ehno Cure", "Raiden");

        Assert.True(created.Success);
        Assert.True(created.Created);
        Assert.True(existing.Success);
        Assert.False(existing.Created);
        var body = Json(handler.Requests[0].Body);
        Assert.Equal("Ehno Cure", body.GetProperty("character_name").GetString());
        Assert.Equal("Raiden", body.GetProperty("world").GetString());
    }

    [Fact]
    public async Task A_character_linked_to_another_account_says_trying_again_will_not_help()
    {
        var handler = new Handler().On("POST", "/me/characters", "{\"detail\":\"That character is linked to another account.\"}", HttpStatusCode.Conflict);

        var result = await Writes(handler).LinkCharacterAsync("Someone Else", "Raiden");

        Assert.False(result.Success);
        Assert.Equal("That character is linked to another account. Trying again will not change that.", result.Error);
    }

    [Fact]
    public async Task Any_other_refusal_to_link_a_character_keeps_the_servers_sentence()
    {
        var handler = new Handler().On("POST", "/me/characters", "{\"detail\":\"World is not a known world.\"}", HttpStatusCode.UnprocessableEntity);

        var result = await Writes(handler).LinkCharacterAsync("Someone Else", "Nowhere");

        Assert.False(result.Success);
        Assert.DoesNotContain("Trying again", result.Error);
    }

    [Fact]
    public async Task Unlinking_a_character_deletes_it_by_id()
    {
        var handler = new Handler().On("DELETE", "/me/characters/3", "", HttpStatusCode.NoContent);

        var result = await Writes(handler).UnlinkCharacterAsync(3);

        Assert.True(result.Success);
        Assert.Equal("DELETE", handler.Requests[0].Method);
    }

    [Fact]
    public async Task A_dropped_connection_becomes_a_failed_result_not_an_exception()
    {
        var handler = new ThrowingHandler();
        var writes = new XvmApiWrites(new HttpClient(handler), "https://api.test/", "s3cret");

        var result = await writes.ClockInAsync("v1", "42");

        Assert.False(result.Success);
        Assert.Contains("connection", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("boom");
    }
}
