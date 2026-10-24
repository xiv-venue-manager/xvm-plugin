using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VenueManager
{
  public class XIVAppVenue
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("addresses")]
    public List<string> Addresses { get; set; } = new();

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
  }

  public class Service
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; } = "";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("stockCount")]
    public int? StockCount { get; set; }

    [JsonPropertyName("linkedItemId")]
    public int? LinkedItemId { get; set; }

    [JsonPropertyName("linkedItemName")]
    public string? LinkedItemName { get; set; }

    [JsonPropertyName("linkedItemIcon")]
    public int? LinkedItemIcon { get; set; }
  }

  public class ServicesResponse
  {
    [JsonPropertyName("services")]
    public List<Service> Services { get; set; } = new();

    [JsonPropertyName("userRole")]
    public string? UserRole { get; set; }
  }

  public class Role
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
  }

  public class BannedPatron
  {
    [JsonPropertyName("characterName")]
    public string CharacterName { get; set; } = "";

    [JsonPropertyName("world")]
    public string World { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
  }

  public class Room
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("isOccupied")]
    public bool IsOccupied { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("roomNumber")]
    public int RoomNumber { get; set; }
  }

  public class ActiveEventResponse
  {
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("eventId")]
    public string? EventId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
  }

  public class LogTransactionResult
  {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ServiceId { get; set; }
    public int? ServiceStockCount { get; set; }
    public string? Warning { get; set; }
  }

  public class ShiftDto
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("scheduledStart")]
    public string ScheduledStart { get; set; } = "";

    [JsonPropertyName("scheduledEnd")]
    public string ScheduledEnd { get; set; } = "";

    [JsonPropertyName("actualStart")]
    public string? ActualStart { get; set; }

    [JsonPropertyName("actualEnd")]
    public string? ActualEnd { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
  }

  public class OpenShiftDto
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("scheduledStart")]
    public string ScheduledStart { get; set; } = "";

    [JsonPropertyName("scheduledEnd")]
    public string ScheduledEnd { get; set; } = "";

    [JsonPropertyName("roleName")]
    public string? RoleName { get; set; }
  }

  public class ShiftsResponse
  {
    [JsonPropertyName("shifts")]
    public List<ShiftDto> Shifts { get; set; } = new();

    [JsonPropertyName("openShifts")]
    public List<OpenShiftDto> OpenShifts { get; set; } = new();
  }

  public class ClockResult
  {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Status { get; set; }
    public double? HoursWorked { get; set; }
  }

  public class XIVAppApiException : Exception
  {
    public XIVAppApiException(string message) : base(message) { }
    public XIVAppApiException(string message, Exception inner) : base(message, inner) { }
  }
}
