using System;
using System.Threading.Tasks;

namespace VenueManager
{
  public class XIVAppShiftApi
  {
    private static readonly ClockResult NotLinked = new() { Success = false, Error = "Link your account in settings first." };

    private readonly XIVAppApiClient _client;

    internal XIVAppShiftApi(XIVAppApiClient client)
    {
      _client = client;
    }

    public Task<ShiftsResponse> GetShiftsResponseAsync(string venueId) =>
      _client.Xvm is { } xvm
        ? _client.SafelyAsync(() => xvm.GetShiftsAsync(venueId, DateTime.UtcNow), new ShiftsResponse(), "get shifts")
        : Task.FromResult(new ShiftsResponse());

    public Task<ClockResult> ClaimShiftAsync(string venueId, string shiftId) =>
      _client.XvmWrites is { } xvm ? xvm.ClaimShiftAsync(venueId, shiftId) : Task.FromResult(NotLinked);

    public Task<ClockResult> ClockInAsync(string venueId, string shiftId) =>
      _client.XvmWrites is { } xvm ? xvm.ClockInAsync(venueId, shiftId) : Task.FromResult(NotLinked);

    public Task<ClockResult> ClockOutAsync(string venueId, string shiftId) =>
      _client.XvmWrites is { } xvm ? xvm.ClockOutAsync(venueId, shiftId) : Task.FromResult(NotLinked);
  }
}
