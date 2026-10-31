using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VenueManager
{
  public class XIVAppVenueApi
  {
    private static readonly LogTransactionResult NotLinked = new() { Success = false, Error = "Link your account in settings first." };

    private readonly XIVAppApiClient _client;

    internal XIVAppVenueApi(XIVAppApiClient client)
    {
      _client = client;
    }

    public async Task<List<XIVAppVenue>> GetVenuesAsync()
    {
      if (_client.Xvm is not { } xvm)
        throw new XIVAppApiException("Link your account first: Settings, Account link.");

      try
      {
        return await xvm.GetVenuesAsync();
      }
      catch (XIVAppApiException)
      {
        throw;
      }
      catch (Exception ex)
      {
        throw new XIVAppApiException($"Error fetching venues: {ex.Message}", ex);
      }
    }

    public Task<ServicesResponse?> GetServicesAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync<ServicesResponse?>(async () => await xvm.GetServicesAsync(venueId), null, "fetch services")
        : Task.FromResult<ServicesResponse?>(null);

    public Task<List<Role>> GetRolesAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync(() => xvm.GetRolesAsync(venueId), new List<Role>(), "get roles")
        : Task.FromResult(new List<Role>());

    public Task<List<BannedPatron>?> GetBannedPatronsAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync<List<BannedPatron>?>(async () => await xvm.GetBannedPatronsAsync(venueId), null, "get banned patrons")
        : Task.FromResult<List<BannedPatron>?>(null);

    public Task<ActiveEventResponse?> GetActiveEventAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync<ActiveEventResponse?>(async () => await xvm.GetActiveEventAsync(venueId), null, "fetch active event")
        : Task.FromResult<ActiveEventResponse?>(new ActiveEventResponse { Active = false });

    public Task<List<Room>?> GetRoomsAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync<List<Room>?>(async () => await xvm.GetRoomsAsync(venueId), null, "get rooms")
        : Task.FromResult<List<Room>?>(null);

    public Task<bool> GetInventoryEnabledAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync(() => xvm.GetInventoryEnabledAsync(venueId), false, "get inventory settings")
        : Task.FromResult(false);

    public Task<LogTransactionResult> ReserveRoomAsync(string venueId, string roomId, int durationMinutes) =>
      _client.XvmWrites is { } xvm ? xvm.ReserveRoomAsync(venueId, roomId, durationMinutes, DateTime.UtcNow) : Task.FromResult(NotLinked);

    public Task<LogTransactionResult> ReleaseRoomAsync(string venueId, string roomId) =>
      _client.XvmWrites is { } xvm ? xvm.ReleaseRoomAsync(venueId, roomId) : Task.FromResult(NotLinked);

    public Task<LogTransactionResult> UpdateRoomAsync(string venueId, string roomId, bool? locked = null, bool? disabled = null) =>
      _client.XvmWrites is { } xvm ? xvm.UpdateRoomAsync(venueId, roomId, locked, disabled) : Task.FromResult(NotLinked);

    public Task<LogTransactionResult> LinkItemAsync(string venueId, string serviceId, int itemId, string itemName, int? iconId) =>
      _client.XvmWrites is { } xvm ? xvm.LinkItemAsync(venueId, serviceId, itemId, itemName, iconId) : Task.FromResult(NotLinked);

    public Task<LogTransactionResult> RestockAsync(string venueId, string serviceId, int stockCount) =>
      _client.XvmWrites is { } xvm ? xvm.RestockAsync(venueId, serviceId, stockCount) : Task.FromResult(NotLinked);
  }
}
