using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VenueManager
{
  /// <summary>
  /// Holds the xvm-api reads and writes for the linked account. Domain logic
  /// lives in the three sub-API classes accessible as properties:
  ///   xivAppClient.Venue   — venues, roles, services, active event, rooms, inventory
  ///   xivAppClient.Patron  — patron visits, transactions, bans
  ///   xivAppClient.Shift   — shifts, claim, clock-in, clock-out
  /// </summary>
  public class XIVAppApiClient
  {
    private static readonly HttpClient XvmHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    internal XvmApiReads? Xvm { get; private set; }

    internal XvmApiWrites? XvmWrites { get; private set; }

    public XIVAppVenueApi Venue { get; }
    public XIVAppPatronApi Patron { get; }
    public XIVAppShiftApi Shift { get; }

    public XIVAppApiClient()
    {
      Venue  = new XIVAppVenueApi(this);
      Patron = new XIVAppPatronApi(this);
      Shift  = new XIVAppShiftApi(this);
    }

    public bool IsConfigured => Xvm is not null;

    public void ConfigureXvm(string url, string secret)
    {
      var linked = !string.IsNullOrWhiteSpace(secret);
      Xvm = linked ? new XvmApiReads(XvmHttp, url, secret) : null;
      XvmWrites = linked ? new XvmApiWrites(XvmHttp, url, secret) : null;
    }

    public void ClearXvm() => ConfigureXvm("", "");

    internal async Task<T> SafelyAsync<T>(Func<Task<T>> call, T fallback, string errorContext)
    {
      try
      {
        return await call();
      }
      catch (Exception ex)
      {
        Plugin.Log.Warning($"Failed to {errorContext}: {ex.Message}");
        return fallback;
      }
    }
  }
}
