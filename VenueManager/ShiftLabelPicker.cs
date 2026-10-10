using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueManager
{
  internal static class ShiftLabelPicker
  {
    public static MyShift? Pick(DateTime nowUtc, IEnumerable<MyShift> shifts, string? currentVenueId)
    {
      var all = shifts.ToList();
      var active = all.Where(s => s.Status == "active" && s.ActualStart is not null && s.ActualEnd is null).ToList();
      if (active.Count > 0)
        return active.FirstOrDefault(s => s.VenueId == currentVenueId) ?? active.OrderBy(s => s.ActualStart).First();

      return all.Where(s => s.Status == "scheduled" && s.ScheduledStart > nowUtc)
        .OrderBy(s => s.ScheduledStart)
        .FirstOrDefault();
    }
  }
}
