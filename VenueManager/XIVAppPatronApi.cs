using System;
using System.Threading.Tasks;

namespace VenueManager
{
  public class XIVAppPatronApi
  {
    private static readonly LogTransactionResult NotLinked = new() { Success = false, Error = "Link your account in settings first." };

    private readonly XIVAppApiClient _client;

    internal XIVAppPatronApi(XIVAppApiClient client)
    {
      _client = client;
    }

    public Task<bool> LogPatronVisitAsync(string venueId, string characterName, string world, string action) =>
      _client.XvmWrites is { } xvm ? xvm.LogPatronVisitAsync(venueId, characterName, world, action, DateTime.UtcNow) : Task.FromResult(false);

    public Task<LogTransactionResult> LogTransactionAsync(
      string venueId,
      string? serviceId,
      decimal amount,
      string? customerName = null,
      string? notes = null,
      string? type = null) =>
      _client.XvmWrites is { } xvm
        ? xvm.LogTransactionAsync(venueId, serviceId, (int)decimal.Round(amount), customerName, notes, type)
        : Task.FromResult(NotLinked);

    public Task<LogTransactionResult> BanPatronAsync(string venueId, string characterName, string world, string reason) =>
      _client.XvmWrites is { } xvm ? xvm.BanPatronAsync(venueId, characterName, world, reason) : Task.FromResult(NotLinked);
  }
}
