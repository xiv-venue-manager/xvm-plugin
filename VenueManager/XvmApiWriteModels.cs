using System.Text.Json.Serialization;

namespace VenueManager
{
  internal sealed record XvmVisitBody(
    [property: JsonPropertyName("character_name")] string CharacterName,
    [property: JsonPropertyName("world")] string World,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("ts")] string Ts);

  internal sealed record XvmTransactionBody(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("service_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ServiceId,
    [property: JsonPropertyName("event_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? EventId,
    [property: JsonPropertyName("customer_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomerName,
    [property: JsonPropertyName("notes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Notes);

  internal sealed record XvmBanBody(
    [property: JsonPropertyName("character_name")] string CharacterName,
    [property: JsonPropertyName("world")] string World,
    [property: JsonPropertyName("reason")] string Reason);

  internal sealed record XvmReservationBody(
    [property: JsonPropertyName("reserved_person_id")] int ReservedPersonId,
    [property: JsonPropertyName("start_at")] string StartAt,
    [property: JsonPropertyName("end_at")] string EndAt,
    [property: JsonPropertyName("source")] string Source);

  internal sealed record XvmRoomPatchBody(
    [property: JsonPropertyName("locked"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Locked,
    [property: JsonPropertyName("disabled"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Disabled);

  internal sealed record XvmLinkBody(
    [property: JsonPropertyName("linked_item_id")] int LinkedItemId,
    [property: JsonPropertyName("linked_item_name")] string? LinkedItemName,
    [property: JsonPropertyName("linked_item_icon")] int? LinkedItemIcon);

  internal sealed record XvmStockBody(
    [property: JsonPropertyName("stock_count")] int StockCount);

  internal sealed record XvmShiftReply(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("worked_minutes")] int? WorkedMinutes);

  internal sealed record XvmMeReply(
    [property: JsonPropertyName("person")] XvmMePerson Person);

  internal sealed record XvmMePerson(
    [property: JsonPropertyName("id")] int Id);

  internal sealed record XvmServiceStockReply(
    [property: JsonPropertyName("inventory")] XvmStockReply? Inventory);

  internal sealed record XvmStockReply(
    [property: JsonPropertyName("stock_count")] int? StockCount);
}
