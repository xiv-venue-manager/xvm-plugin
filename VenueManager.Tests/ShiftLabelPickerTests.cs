using VenueManager;
using Xunit;

public class ShiftLabelPickerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);

    private static MyShift Shift(int id, string venue, string status, int startMinutesFromNow, bool clockedIn = false, bool clockedOut = false) =>
        new(id, venue, status, Now.AddMinutes(startMinutesFromNow), Now.AddMinutes(startMinutesFromNow + 120),
            clockedIn ? Now.AddMinutes(startMinutesFromNow) : null, clockedOut ? Now : null);

    [Fact]
    public void A_clocked_in_shift_anywhere_beats_an_upcoming_one_here()
    {
        var elsewhere = Shift(1, "v2", "active", -30, clockedIn: true);
        var here = Shift(2, "v1", "scheduled", 30);
        Assert.Equal(1, ShiftLabelPicker.Pick(Now, new[] { here, elsewhere }, "v1")!.Id);
    }

    [Fact]
    public void With_two_clocked_in_shifts_the_current_venue_wins()
    {
        var a = Shift(1, "v1", "active", -60, clockedIn: true);
        var b = Shift(2, "v2", "active", -90, clockedIn: true);
        Assert.Equal(1, ShiftLabelPicker.Pick(Now, new[] { b, a }, "v1")!.Id);
        Assert.Equal(2, ShiftLabelPicker.Pick(Now, new[] { a, b }, "v9")!.Id);
    }

    [Fact]
    public void With_nothing_clocked_in_the_soonest_upcoming_shift_anywhere_is_shown()
    {
        var later = Shift(1, "v1", "scheduled", 90);
        var sooner = Shift(2, "v2", "scheduled", 40);
        Assert.Equal(2, ShiftLabelPicker.Pick(Now, new[] { later, sooner }, "v1")!.Id);
    }

    [Fact]
    public void A_started_shift_that_is_not_clocked_in_is_not_shown()
    {
        Assert.Null(ShiftLabelPicker.Pick(Now, new[] { Shift(1, "v1", "scheduled", -10) }, "v1"));
    }

    [Theory]
    [InlineData("pending_approval")]
    [InlineData("open")]
    [InlineData("completed")]
    [InlineData("cancelled")]
    public void Only_approved_shifts_are_shown(string status)
    {
        Assert.Null(ShiftLabelPicker.Pick(Now, new[] { Shift(1, "v1", status, 30) }, "v1"));
    }

    [Fact]
    public void A_shift_clocked_out_is_not_shown_as_active()
    {
        Assert.Null(ShiftLabelPicker.Pick(Now, new[] { Shift(1, "v1", "active", -60, clockedIn: true, clockedOut: true) }, "v1"));
    }

    [Fact]
    public void No_shifts_gives_nothing()
    {
        Assert.Null(ShiftLabelPicker.Pick(Now, Array.Empty<MyShift>(), "v1"));
    }
}
