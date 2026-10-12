using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VenueManager
{
  internal sealed record XvmVenue(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug);

  internal sealed record XvmModule(
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("enabled")] bool Enabled);

  internal sealed record XvmMyVenue(
    [property: JsonPropertyName("venue")] XvmVenue Venue,
    [property: JsonPropertyName("effective_tier")] string EffectiveTier,
    [property: JsonPropertyName("membership_id")] int MembershipId,
    [property: JsonPropertyName("position_ids")] List<int>? PositionIds);

  internal sealed record XvmShift(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("position_id")] int? PositionId,
    [property: JsonPropertyName("scheduled_start")] string? ScheduledStart,
    [property: JsonPropertyName("scheduled_end")] string? ScheduledEnd,
    [property: JsonPropertyName("actual_start")] string? ActualStart,
    [property: JsonPropertyName("actual_end")] string? ActualEnd,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("status")] string Status);

  internal sealed record XvmPosition(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

  internal sealed record XvmInventory(
    [property: JsonPropertyName("linked_item_id")] int LinkedItemId,
    [property: JsonPropertyName("linked_item_name")] string? LinkedItemName,
    [property: JsonPropertyName("linked_item_icon")] int? LinkedItemIcon,
    [property: JsonPropertyName("stock_count")] int? StockCount);

  internal sealed record XvmService(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("price_minor")] int? PriceMinor,
    [property: JsonPropertyName("category_id")] int? CategoryId,
    [property: JsonPropertyName("inventory")] XvmInventory? Inventory);

  internal sealed record XvmCategory(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

  internal sealed record XvmBanned(
    [property: JsonPropertyName("character_name")] string CharacterName,
    [property: JsonPropertyName("world")] string World,
    [property: JsonPropertyName("ban_reason")] string? BanReason);

  internal sealed record XvmEvent(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title);

  internal sealed record XvmRoom(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("room_number")] int? RoomNumber,
    [property: JsonPropertyName("locked")] bool Locked,
    [property: JsonPropertyName("disabled")] bool Disabled,
    [property: JsonPropertyName("status")] string Status);
}
