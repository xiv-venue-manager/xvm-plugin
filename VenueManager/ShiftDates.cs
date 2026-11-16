using System;
using System.Globalization;

namespace VenueManager
{
  internal static class ShiftDates
  {
    public static DateTime? LocalStart(string iso) =>
      DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc)
        ? utc.ToLocalTime()
        : null;

    public static bool BreakBetween(DateTime? last, DateTime? next) =>
      last.HasValue && next.HasValue && last.Value.Date != next.Value.Date;
  }
}
