using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace VenueManager
{
  public sealed class XvmApiReads
  {
    private static readonly TimeSpan LookBack = TimeSpan.FromDays(1);
    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(14);

    private readonly HttpClient _http;
    private readonly string _root;
    private readonly string _secret;
    private readonly Action<bool>? _linkState;

    public XvmApiReads(HttpClient http, string baseUrl, string secret, Action<bool>? linkState = null)
    {
      _linkState = linkState;
      _http = http;
      _root = baseUrl.Trim().TrimEnd('/');
      _secret = secret;
    }

    private async Task<T?> GetAsync<T>(string path)
    {
      using var request = new HttpRequestMessage(HttpMethod.Get, $"{_root}{path}");
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secret);
      using var response = await _http.SendAsync(request);
      if ((int)response.StatusCode == 401) _linkState?.Invoke(true);
      else if (response.IsSuccessStatusCode) _linkState?.Invoke(false);
      if (!response.IsSuccessStatusCode)
        throw new XIVAppApiException($"{path}: {(int)response.StatusCode}");
      return await response.Content.ReadFromJsonAsync<T>();
    }

    private static string Venue(string venueId) => $"/venues/{Uri.EscapeDataString(venueId)}";

    private async Task<XvmMyVenue?> MyVenueAsync(string venueId) =>
      (await GetAsync<List<XvmMyVenue>>("/me/venues") ?? new List<XvmMyVenue>()).FirstOrDefault(v => v.Venue.Id == venueId);

    public async Task<List<XIVAppVenue>> GetVenuesAsync() =>
      XvmApiMapping.MapVenues(await GetAsync<List<XvmMyVenue>>("/me/venues") ?? new List<XvmMyVenue>());

    public async Task<ShiftsResponse> GetShiftsAsync(string venueId, DateTime now)
    {
      var window = $"from={Uri.EscapeDataString((now - LookBack).ToString("yyyy-MM-ddTHH:mm:ssZ"))}&to={Uri.EscapeDataString((now + LookAhead).ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
      var mine = await GetAsync<List<XvmShift>>($"{Venue(venueId)}/shifts?{window}&mine=true") ?? new List<XvmShift>();
      var open = await GetAsync<List<XvmShift>>($"{Venue(venueId)}/shifts?{window}&open_only=true") ?? new List<XvmShift>();
      var names = new Dictionary<int, string>();
      if (open.Count > 0)
        names = (await GetAsync<List<XvmPosition>>($"{Venue(venueId)}/positions") ?? new List<XvmPosition>()).ToDictionary(p => p.Id, p => p.Name);
      return XvmApiMapping.MapShifts(mine, open, names);
    }

    internal async Task<List<MyShift>> GetMyShiftsAsync(DateTime now)
    {
      var window = $"from={Uri.EscapeDataString((now - TimeSpan.FromDays(1)).ToString("yyyy-MM-ddTHH:mm:ssZ"))}&to={Uri.EscapeDataString((now + TimeSpan.FromDays(1)).ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
      return await GetAsync<List<MyShift>>($"/me/shifts?{window}") ?? new List<MyShift>();
    }

    internal async Task<List<XvmVenueAddress>> GetVenueAddressesAsync(IEnumerable<string> venueIds)
    {
      var rows = await Task.WhenAll(venueIds.Select(id => GetAsync<XvmVenueAddress>(Venue(id))));
      return rows.Where(r => r is not null).Select(r => r!).ToList();
    }

    internal async Task<List<XvmCharacter>> GetMyCharactersAsync() =>
      await GetAsync<List<XvmCharacter>>("/me/characters") ?? new List<XvmCharacter>();

    public async Task<List<Role>> GetRolesAsync(string venueId)
    {
      var own = await MyVenueAsync(venueId);
      if (own?.PositionIds is not { Count: > 0 }) return new List<Role>();
      var positions = await GetAsync<List<XvmPosition>>($"{Venue(venueId)}/positions") ?? new List<XvmPosition>();
      return XvmApiMapping.MapRoles(own.PositionIds, positions);
    }

    public async Task<ServicesResponse> GetServicesAsync(string venueId)
    {
      var services = await GetAsync<List<XvmService>>($"{Venue(venueId)}/services") ?? new List<XvmService>();
      var categories = await GetAsync<List<XvmCategory>>($"{Venue(venueId)}/services/categories") ?? new List<XvmCategory>();
      return XvmApiMapping.MapServices(services, categories);
    }

    public async Task<bool> GetInventoryEnabledAsync(string venueId) =>
      (await GetAsync<List<XvmModule>>($"{Venue(venueId)}/modules"))?.Any(m => m.Module == "inventory" && m.Enabled) ?? false;

    public async Task<List<BannedPatron>> GetBannedPatronsAsync(string venueId) =>
      XvmApiMapping.MapBanned(await GetAsync<List<XvmBanned>>($"{Venue(venueId)}/patrons/banned") ?? new List<XvmBanned>());

    public async Task<ActiveEventResponse> GetActiveEventAsync(string venueId) =>
      XvmApiMapping.MapActiveEvent(await GetAsync<XvmEvent>($"{Venue(venueId)}/events/active"));

    public async Task<List<Room>> GetRoomsAsync(string venueId) =>
      XvmApiMapping.MapRooms(await GetAsync<List<XvmRoom>>($"{Venue(venueId)}/rooms") ?? new List<XvmRoom>());
  }
}
