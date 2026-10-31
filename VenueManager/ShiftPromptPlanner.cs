using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VenueManager
{
  internal enum PromptKind { Start, End, Soon }

  internal sealed record ShiftPrompt(int ShiftId, string VenueId, PromptKind Kind, DateTime? StartsAt = null);

  internal sealed record PromptMemory(PromptKind Kind, DateTime At);

  internal sealed record MyShift(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("venue_id")] string VenueId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("scheduled_start")] DateTime? ScheduledStart,
    [property: JsonPropertyName("scheduled_end")] DateTime? ScheduledEnd,
    [property: JsonPropertyName("actual_start")] DateTime? ActualStart,
    [property: JsonPropertyName("actual_end")] DateTime? ActualEnd);

  internal static class ShiftPromptPlanner
  {
    private static readonly TimeSpan SoonLead = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EndRepeat = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EndGiveUp = TimeSpan.FromHours(12);

    public static List<ShiftPrompt> Due(
      DateTime nowUtc, IEnumerable<MyShift> shifts, IReadOnlyDictionary<int, PromptMemory> memory, ISet<int> muted)
    {
      var due = new List<ShiftPrompt>();
      foreach (var shift in shifts)
      {
        if (muted.Contains(shift.Id)) continue;
        memory.TryGetValue(shift.Id, out var last);

        if (shift.Status == "scheduled" && shift.ActualStart is null
            && shift.ScheduledStart is { } startsAt && startsAt > nowUtc && startsAt - nowUtc <= SoonLead
            && last is null)
        {
          due.Add(new ShiftPrompt(shift.Id, shift.VenueId, PromptKind.Soon, startsAt));
        }
        else if (shift.Status == "scheduled" && shift.ActualStart is null
            && shift.ScheduledStart <= nowUtc && (shift.ScheduledEnd is null || nowUtc < shift.ScheduledEnd)
            && (last is null || last.Kind != PromptKind.Start))
        {
          due.Add(new ShiftPrompt(shift.Id, shift.VenueId, PromptKind.Start));
        }
        else if (shift.Status == "active" && shift.ActualStart is not null && shift.ActualEnd is null
            && shift.ScheduledEnd <= nowUtc && nowUtc - shift.ScheduledEnd <= EndGiveUp
            && (last is null || last.Kind != PromptKind.End || nowUtc - last.At >= EndRepeat))
        {
          due.Add(new ShiftPrompt(shift.Id, shift.VenueId, PromptKind.End));
        }
      }
      return due;
    }
  }
}
