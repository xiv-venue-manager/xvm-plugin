using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

  public record PairingResult(PairingOutcome Outcome, string? Secret = null, string? PersonName = null)
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
    public const string DefaultUrl = "https://api.xivvenuemanager.com";

    internal sealed record ExchangeResponse([property: JsonPropertyName("secret")] string? Secret);

    internal sealed record MePerson([property: JsonPropertyName("display_name")] string? DisplayName);

    internal sealed record MeResponse([property: JsonPropertyName("person")] MePerson? Person);

    public static string NormalizeCode(string? raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return "";
      var compact = raw.Replace(" ", "").Replace("-", "").ToUpperInvariant();
      return compact.Length == 8 ? $"{compact[..4]}-{compact[4..]}" : compact;
    }

    public static async Task<PairingResult> LinkAsync(HttpClient http, string baseUrl, string? rawCode)
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
        if (!exchange.IsSuccessStatusCode) return new PairingResult(PairingOutcome.Failed);

        var issued = await exchange.Content.ReadFromJsonAsync<ExchangeResponse>();
        if (string.IsNullOrEmpty(issued?.Secret)) return new PairingResult(PairingOutcome.Failed);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.Secret);
        using var me = await http.SendAsync(request);
        if (!me.IsSuccessStatusCode) return new PairingResult(PairingOutcome.Failed);

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
