using System.Text.Json;

namespace VenueManager
{
  internal static class XvmApiErrors
  {
    public static string Describe(int status, string body)
    {
      var detail = ReadDetail(body);
      if (status == 401) return "Your account link was rejected. Link your account again in settings.";
      if (status == 403 && detail is not null && detail == "Insufficient role for this action.") return "You need to be a manager at this venue to do that.";
      return detail ?? $"The server answered {status}.";
    }

    private static string? ReadDetail(string body)
    {
      try
      {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("detail", out var detail)) return null;
        if (detail.ValueKind == JsonValueKind.String) return detail.GetString();
        if (detail.ValueKind == JsonValueKind.Array && detail.GetArrayLength() > 0
            && detail[0].TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String)
          return msg.GetString();
        return null;
      }
      catch (JsonException)
      {
        return null;
      }
    }
  }
}
