using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VenueManager
{
  internal static class XvmApiMapping
  {
    public static string ShiftStatus(string status) => status switch
    {
      "scheduled" => "SCHEDULED",
      "active" => "ACTIVE",
      "completed" => "COMPLETED",
      "pending_approval" => "PENDING",
      _ => status.ToUpperInvariant(),
    };

    public static List<XIVAppVenue> MapVenues(IEnumerable<XvmMyVenue> venues) =>
      venues.Select(v => new XIVAppVenue
      {
        Id = v.Venue.Id,
        Name = v.Venue.Name,
        Slug = v.Venue.Slug,
        Role = v.EffectiveTier.ToUpperInvariant(),
      }).ToList();

    public static ShiftsResponse MapShifts(IEnumerable<XvmShift> mine, IEnumerable<XvmBoardShift> open) =>
      new()
      {
        Shifts = mine.Select(s => new ShiftDto
        {
          Id = s.Id.ToString(CultureInfo.InvariantCulture),
          ScheduledStart = s.ScheduledStart ?? "",
          ScheduledEnd = s.ScheduledEnd ?? "",
          ActualStart = s.ActualStart,
          ActualEnd = s.ActualEnd,
          Status = ShiftStatus(s.Status),
          Notes = s.Notes,
        }).ToList(),
        OpenShifts = open.Where(s => mine.All(m => m.EventShiftId != s.Id)).Select(s => new OpenShiftDto
        {
          Id = s.Id.ToString(CultureInfo.InvariantCulture),
          ScheduledStart = s.StartsAt,
          ScheduledEnd = s.EndsAt,
          RoleName = s.PositionName,
        }).ToList(),
      };

    public static List<Role> MapRoles(IEnumerable<int>? positionIds, IEnumerable<XvmPosition> positions)
    {
      if (positionIds is null) return new List<Role>();
      var byId = positions.ToDictionary(p => p.Id);
      return positionIds
        .Where(byId.ContainsKey)
        .Select(id => new Role { Id = id.ToString(CultureInfo.InvariantCulture), Name = byId[id].Name })
        .ToList();
    }

    public static ServicesResponse MapServices(IEnumerable<XvmService> services, IEnumerable<XvmCategory> categories)
    {
      var names = categories.ToDictionary(c => c.Id, c => c.Name);
      return new ServicesResponse
      {
        Services = services.Select(s => new Service
        {
          Id = s.Id.ToString(CultureInfo.InvariantCulture),
          Name = s.Name,
          Description = s.Description,
          Price = (s.PriceMinor ?? 0).ToString(CultureInfo.InvariantCulture),
          Category = s.CategoryId is { } id && names.TryGetValue(id, out var name) ? name : null,
          StockCount = s.Inventory?.StockCount,
          LinkedItemId = s.Inventory?.LinkedItemId,
          LinkedItemName = s.Inventory?.LinkedItemName,
          LinkedItemIcon = s.Inventory?.LinkedItemIcon,
        }).ToList(),
      };
    }

    public static List<BannedPatron> MapBanned(IEnumerable<XvmBanned> banned) =>
      banned.Select(b => new BannedPatron { CharacterName = b.CharacterName, World = b.World, Reason = b.BanReason ?? "" }).ToList();

    public static ActiveEventResponse MapActiveEvent(XvmEvent? active) =>
      active is null
        ? new ActiveEventResponse { Active = false }
        : new ActiveEventResponse { Active = true, EventId = active.Id.ToString(CultureInfo.InvariantCulture), Title = active.Title, Status = "ACTIVE" };

    public static List<Room> MapRooms(IEnumerable<XvmRoom> rooms) =>
      rooms.Select(r => new Room
      {
        Id = r.Id.ToString(CultureInfo.InvariantCulture),
        Name = r.Name ?? $"Room {r.RoomNumber}",
        IsOccupied = r.Status == "reserved",
        Note = r.Notes,
        Locked = r.Locked,
        Disabled = r.Disabled,
        RoomNumber = r.RoomNumber ?? 0,
      }).ToList();

  }
}
