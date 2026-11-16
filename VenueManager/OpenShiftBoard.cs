using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueManager
{
  internal static class OpenShiftBoard
  {
    public static List<OpenShiftDto> Visible(IEnumerable<OpenShiftDto> open, IEnumerable<Role> roles)
    {
      var held = roles.Select(r => r.Name).ToList();
      return open
        .Where(s => !string.IsNullOrEmpty(s.RoleName) && held.Any(n => string.Equals(n, s.RoleName, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(s => ShiftDates.LocalStart(s.ScheduledStart))
        .ToList();
    }
  }
}
