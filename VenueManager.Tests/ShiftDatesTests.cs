using System.Globalization;
using VenueManager;
using Xunit;

public class ShiftDatesTests
{
    [Fact]
    public void LocalStart_returns_the_local_time_of_a_utc_string()
    {
        var expected = DateTimeOffset.Parse("2026-10-11T19:00:00Z", CultureInfo.InvariantCulture).LocalDateTime;

        Assert.Equal((DateTime?)expected, ShiftDates.LocalStart("2026-10-11T19:00:00Z"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    public void LocalStart_is_null_when_the_string_does_not_parse(string iso)
    {
        Assert.Null(ShiftDates.LocalStart(iso));
    }

    [Fact]
    public void BreakBetween_is_true_when_the_calendar_day_changes()
    {
        Assert.True(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 23, 0, 0), new DateTime(2026, 10, 12, 1, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_within_one_day()
    {
        Assert.False(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 9, 0, 0), new DateTime(2026, 10, 11, 23, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_for_the_first_row()
    {
        Assert.False(ShiftDates.BreakBetween(null, new DateTime(2026, 10, 11, 9, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_when_the_next_date_is_unknown()
    {
        Assert.False(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 9, 0, 0), null));
    }
}
