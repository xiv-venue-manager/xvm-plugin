using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueManager
{
  internal enum ShiftVenueChoiceKind { None, Venue, Conflict }

  internal sealed record ShiftVenueChoice(ShiftVenueChoiceKind Kind, string? VenueId = null);

  internal static class ShiftVenuePicker
  {
    public static ShiftVenueChoice Pick(DateTime nowUtc, IEnumerable<MyShift> shifts)
    {
      var all = shifts.ToList();

      var clockedIn = VenuesOf(all.Where(s => s.Status == "active" && s.ActualStart is not null && s.ActualEnd is null));
      if (clockedIn.Count > 0) return Decide(clockedIn);

      var running = VenuesOf(all.Where(s => s.Status == "scheduled" && s.ActualStart is null
        && s.ScheduledStart <= nowUtc && (s.ScheduledEnd is null || nowUtc < s.ScheduledEnd)));
      return running.Count > 0 ? Decide(running) : new ShiftVenueChoice(ShiftVenueChoiceKind.None);
    }

    public static bool EnteredConflict(ShiftVenueChoice? before, ShiftVenueChoice now) =>
      now.Kind == ShiftVenueChoiceKind.Conflict && before?.Kind != ShiftVenueChoiceKind.Conflict;

    private static List<string> VenuesOf(IEnumerable<MyShift> shifts) => shifts.Select(s => s.VenueId).Distinct().ToList();

    private static ShiftVenueChoice Decide(List<string> venues) =>
      venues.Count == 1
        ? new ShiftVenueChoice(ShiftVenueChoiceKind.Venue, venues[0])
        : new ShiftVenueChoice(ShiftVenueChoiceKind.Conflict);
  }
}
