using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace VenueManager
{
  internal sealed class XvmApiWrites
  {
    private readonly HttpClient _http;
    private readonly string _root;
    private readonly string _secret;
    private readonly TimeSpan _settle;
    private readonly Action<bool>? _linkState;
    private int? _personId;

    public XvmApiWrites(HttpClient http, string baseUrl, string secret, TimeSpan? settle = null, Action<bool>? linkState = null)
    {
      _linkState = linkState;
      _settle = settle ?? TimeSpan.FromMilliseconds(300);
      _http = http;
      _root = baseUrl.Trim().TrimEnd('/');
      _secret = secret;
    }

    private readonly record struct Reply(bool Ok, int Status, string Body)
    {
      public string Error => Status == 0 ? Body : XvmApiErrors.Describe(Status, Body);
    }

    private async Task<Reply> SendAsync(HttpMethod method, string path, object? body = null)
    {
      try
      {
        using var request = new HttpRequestMessage(method, $"{_root}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secret);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        using var response = await _http.SendAsync(request);
        if ((int)response.StatusCode == 401) _linkState?.Invoke(true);
        else if (response.IsSuccessStatusCode) _linkState?.Invoke(false);
        if (response.IsSuccessStatusCode && method != HttpMethod.Get) await Task.Delay(_settle);
        return new Reply(response.IsSuccessStatusCode, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
      {
        return new Reply(false, 0, "Could not reach the server. Check your connection and try again.");
      }
    }

    private static T? Read<T>(string body)
    {
      try { return JsonSerializer.Deserialize<T>(body); }
      catch (JsonException) { return default; }
    }

    private static string Venue(string venueId) => $"/venues/{Uri.EscapeDataString(venueId)}";

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static LogTransactionResult Result(Reply reply) =>
      reply.Ok ? new LogTransactionResult { Success = true } : new LogTransactionResult { Success = false, Error = reply.Error };

    private async Task<int?> PersonIdAsync()
    {
      if (_personId is null)
      {
        var reply = await SendAsync(HttpMethod.Get, "/me");
        if (reply.Ok) _personId = Read<XvmMeReply>(reply.Body)?.Person.Id;
      }
      return _personId;
    }

    public async Task<ClockResult> ClaimShiftAsync(string venueId, string shiftId)
    {
      var reply = await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/shifts/{shiftId}/claim");
      if (!reply.Ok) return new ClockResult { Success = false, Error = reply.Error };
      var status = Read<XvmShiftReply>(reply.Body)?.Status;
      return new ClockResult { Success = true, Status = status is null ? "PENDING" : XvmApiMapping.ShiftStatus(status) };
    }

    public async Task<ClockResult> ClockInAsync(string venueId, string shiftId)
    {
      var reply = await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/shifts/{shiftId}/clock-in");
      return reply.Ok ? new ClockResult { Success = true, Status = "ACTIVE" } : new ClockResult { Success = false, Error = reply.Error };
    }

    public async Task<ClockResult> ClockOutAsync(string venueId, string shiftId)
    {
      var reply = await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/shifts/{shiftId}/clock-out");
      if (!reply.Ok) return new ClockResult { Success = false, Error = reply.Error };
      var minutes = Read<XvmShiftReply>(reply.Body)?.WorkedMinutes;
      return new ClockResult { Success = true, Status = "COMPLETED", HoursWorked = minutes / 60.0 };
    }

    public async Task<bool> LogPatronVisitAsync(string venueId, string characterName, string world, string action, DateTime nowUtc) =>
      (await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/patrons/visits", new XvmVisitBody(characterName, world, action, Iso(nowUtc)))).Ok;

    public async Task<LogTransactionResult> LogTransactionAsync(
      string venueId, string? serviceId, int amount, string? customerName, string? notes, string? type)
    {
      int? serviceKey = int.TryParse(serviceId, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;
      int? eventId = null;
      var active = await SendAsync(HttpMethod.Get, $"{Venue(venueId)}/events/active");
      if (active.Ok) eventId = Read<XvmEvent>(active.Body)?.Id;

      var kind = string.Equals(type, "TIP", StringComparison.OrdinalIgnoreCase) ? "tip" : "sale";
      var body = new XvmTransactionBody(kind, amount, Guid.NewGuid().ToString("N"), serviceKey, eventId, customerName, notes);
      var reply = await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/finance/transactions", body);
      if (!reply.Ok) return Result(reply);

      var result = new LogTransactionResult { Success = true, ServiceId = serviceId };
      if (serviceKey is not null && kind == "sale")
      {
        var stock = await SendAsync(HttpMethod.Get, $"{Venue(venueId)}/services/{serviceKey}");
        if (stock.Ok) result.ServiceStockCount = Read<XvmServiceStockReply>(stock.Body)?.Inventory?.StockCount;
      }
      return result;
    }

    public async Task<LogTransactionResult> BanPatronAsync(string venueId, string characterName, string world, string reason) =>
      Result(await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/patrons/bans", new XvmBanBody(characterName, world, reason)));

    public async Task<LogTransactionResult> ReserveRoomAsync(string venueId, string roomId, int durationMinutes, DateTime nowUtc)
    {
      var personId = await PersonIdAsync();
      if (personId is null) return new LogTransactionResult { Success = false, Error = "Could not read your account. Link your account again in settings." };
      var body = new XvmReservationBody(personId.Value, Iso(nowUtc), Iso(nowUtc.AddMinutes(durationMinutes)), "plugin_manual");
      return Result(await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/rooms/{roomId}/reservations", body));
    }

    public async Task<LogTransactionResult> ReleaseRoomAsync(string venueId, string roomId) =>
      Result(await SendAsync(HttpMethod.Post, $"{Venue(venueId)}/rooms/{roomId}/release"));

    public async Task<LogTransactionResult> UpdateRoomAsync(string venueId, string roomId, bool? locked, bool? disabled) =>
      Result(await SendAsync(HttpMethod.Patch, $"{Venue(venueId)}/rooms/{roomId}", new XvmRoomPatchBody(locked, disabled)));

    public async Task<LogTransactionResult> LinkItemAsync(string venueId, string serviceId, int itemId, string itemName, int? iconId) =>
      Result(await SendAsync(HttpMethod.Put, $"{Venue(venueId)}/services/{serviceId}/inventory", new XvmLinkBody(itemId, itemName, iconId)));

    public async Task<CharacterLinkResult> LinkCharacterAsync(string characterName, string world)
    {
      var reply = await SendAsync(HttpMethod.Post, "/me/characters", new XvmCharacterBody(characterName, world));
      if (reply.Ok) return new CharacterLinkResult(true, reply.Status == 201, null);
      return new CharacterLinkResult(false, false, reply.Status == 409 ? $"{reply.Error} Trying again will not change that." : reply.Error);
    }

    public async Task<LogTransactionResult> UnlinkCharacterAsync(int characterId) =>
      Result(await SendAsync(HttpMethod.Delete, $"/me/characters/{characterId}"));

    public async Task<LogTransactionResult> RestockAsync(string venueId, string serviceId, int stockCount) =>
      Result(await SendAsync(HttpMethod.Put, $"{Venue(venueId)}/services/{serviceId}/inventory/stock", new XvmStockBody(stockCount)));
  }
}
