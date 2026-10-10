using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VenueManager
{
  public enum PairingOutcome
  {
    Linked,
    UnknownCode,
    CodeUsedOrExpired,
    RateLimited,
    Unreachable,
    Failed,
  }

  public record PairingResult(PairingOutcome Outcome, string? Secret = null, string? PersonName = null, string? Detail = null)
  {
    public bool Success => Outcome == PairingOutcome.Linked;

    public string Message => Outcome switch
    {
      PairingOutcome.Linked => "Linked.",
      PairingOutcome.UnknownCode => "That code was not recognised. Check it and try again.",
      PairingOutcome.CodeUsedOrExpired => "That code has already been used or has expired. Generate a new one on the website.",
      PairingOutcome.RateLimited => "Too many attempts. Wait a minute and try again.",
      PairingOutcome.Unreachable => "Could not reach the server. Check your connection and try again.",
      _ => "Linking failed. Try again, and generate a new code if it keeps failing.",
    };
  }

  public static class XvmApiPairing
  {
    public const string ProdUrl = "https://api.xivvenuemanager.com";
    public const string DevUrl = "https://dev-api.xivvenuemanager.com";

#if DEV_FLAVOUR
    public const string DefaultUrl = DevUrl;
    public const bool IsDevFlavour = true;
    public const string WebUrl = "https://dev.xivvenuemanager.com";
#else
    public const string DefaultUrl = ProdUrl;
    public const bool IsDevFlavour = false;
    public const string WebUrl = "https://xivvenuemanager.com";
#endif

    private static readonly string[] DevAllowedHosts = { "dev-api.xivvenuemanager.com", "localhost", "127.0.0.1", "100.80.129.7" };

    public static string ResolveUrl(string? configured, bool dev)
    {
      var fallback = dev ? DevUrl : ProdUrl;
      if (string.IsNullOrWhiteSpace(configured)) return fallback;
      var trimmed = configured.Trim().TrimEnd('/');
      if (!dev) return trimmed;
      return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && Array.IndexOf(DevAllowedHosts, uri.Host.ToLowerInvariant()) >= 0
        ? trimmed
        : fallback;
    }

    public static string ResolveUrl(string? configured) => ResolveUrl(configured, IsDevFlavour);

    private const int MeAttempts = 6;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);

    internal sealed record ExchangeResponse([property: JsonPropertyName("secret")] string? Secret);

    internal sealed record MePerson([property: JsonPropertyName("display_name")] string? DisplayName);

    internal sealed record MeResponse([property: JsonPropertyName("person")] MePerson? Person);

    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient http, string root, string secret, TimeSpan delay)
    {
      var attempt = 0;
      while (true)
      {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        var response = await http.SendAsync(request);
        attempt++;
        if (response.StatusCode != HttpStatusCode.Unauthorized || attempt >= MeAttempts) return response;
        response.Dispose();
        await Task.Delay(delay);
      }
    }

    private static async Task<string> DescribeAsync(string step, HttpResponseMessage response, int? secretLength = null)
    {
      var body = await response.Content.ReadAsStringAsync();
      var reason = body;
      try
      {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("detail", out var detail)) reason = detail.ToString();
      }
      catch (JsonException)
      {
      }
      if (reason.Length > 200) reason = reason[..200];
      var text = $"{step} {(int)response.StatusCode}: {reason}";
      return secretLength is null ? text : $"{text}, secret length {secretLength}";
    }

    public static string NormalizeCode(string? raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return "";
      var compact = raw.Replace(" ", "").Replace("-", "").ToUpperInvariant();
      return compact.Length == 8 ? $"{compact[..4]}-{compact[4..]}" : compact;
    }

    public static async Task<PairingResult> LinkAsync(HttpClient http, string baseUrl, string? rawCode, TimeSpan? retryDelay = null)
    {
      var code = NormalizeCode(rawCode);
      if (code.Length == 0) return new PairingResult(PairingOutcome.UnknownCode);

      var root = baseUrl.Trim().TrimEnd('/');
      try
      {
        using var exchange = await http.PostAsJsonAsync($"{root}/pairing/exchange", new { code, client = "plugin" });
        switch (exchange.StatusCode)
        {
          case HttpStatusCode.BadRequest:
            return new PairingResult(PairingOutcome.UnknownCode);
          case HttpStatusCode.Conflict:
            return new PairingResult(PairingOutcome.CodeUsedOrExpired);
          case HttpStatusCode.TooManyRequests:
            return new PairingResult(PairingOutcome.RateLimited);
        }
        if (!exchange.IsSuccessStatusCode)
          return new PairingResult(PairingOutcome.Failed, Detail: await DescribeAsync("exchange", exchange));

        var issued = await exchange.Content.ReadFromJsonAsync<ExchangeResponse>();
        if (string.IsNullOrEmpty(issued?.Secret))
          return new PairingResult(PairingOutcome.Failed, Detail: "exchange reply had no secret");

        using var me = await GetMeAsync(http, root, issued.Secret, retryDelay ?? DefaultRetryDelay);
        if (!me.IsSuccessStatusCode)
          return new PairingResult(PairingOutcome.Failed, Detail: await DescribeAsync("/me", me, issued.Secret.Length));

        var identity = await me.Content.ReadFromJsonAsync<MeResponse>();
        return new PairingResult(PairingOutcome.Linked, issued.Secret, identity?.Person?.DisplayName);
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
      {
        return new PairingResult(PairingOutcome.Unreachable);
      }
    }
  }
}
