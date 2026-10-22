using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VenueManager
{
  public enum HouseKind { Plot, Apartment, ApartmentSubdivision }

  internal sealed record HouseAddress(string World, string District, int Ward, HouseKind Kind, int Plot, int Room);

  internal sealed record XvmVenueAddress(
    [property: JsonPropertyName("id")] string VenueId,
    [property: JsonPropertyName("world")] string World,
    [property: JsonPropertyName("district")] string? District,
    [property: JsonPropertyName("ward")] int? Ward,
    [property: JsonPropertyName("plot")] int? Plot,
    [property: JsonPropertyName("room")] int? Room,
    [property: JsonPropertyName("subdivision")] bool? Subdivision);

  internal static class VenueAddressMatcher
  {
    public static HouseKind KindOf(int plot) =>
      plot > 0 ? HouseKind.Plot : plot == -126 ? HouseKind.ApartmentSubdivision : HouseKind.Apartment;

    public static string? FindUnique(HouseAddress house, IEnumerable<XvmVenueAddress> venues)
    {
      var hits = venues.Where(v => Matches(house, v)).Select(v => v.VenueId).Distinct().ToList();
      return hits.Count == 1 ? hits[0] : null;
    }

    private static bool Matches(HouseAddress house, XvmVenueAddress venue)
    {
      if (!Same(house.World, venue.World) || !Same(house.District, venue.District) || venue.Ward != house.Ward) return false;
      if (house.Kind == HouseKind.Plot) return venue.Plot == house.Plot;
      return venue.Plot is null && venue.Room == house.Room
        && (venue.Subdivision ?? false) == (house.Kind == HouseKind.ApartmentSubdivision);
    }

    private static bool Same(string? a, string? b) =>
      string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
  }
}
