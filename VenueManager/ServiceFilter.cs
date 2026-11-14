using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueManager
{
  internal static class ServiceFilter
  {
    public static List<Service> Apply(IEnumerable<Service> services, string query)
    {
      var needle = query.Trim();
      if (needle.Length == 0) return services.ToList();

      return services
        .Where(s => Contains(s.Name, needle) || Contains(s.LinkedItemName, needle) || Contains(s.Category, needle))
        .ToList();
    }

    private static bool Contains(string? haystack, string needle) =>
      haystack != null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
  }
}
