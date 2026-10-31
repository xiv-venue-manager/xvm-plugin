using VenueManager;
using Xunit;

public class ShiftVenuePickerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);

    private static MyShift Scheduled(int id, string venue, int startedMinutesAgo, int endsInMinutes, string status = "scheduled") =>
        new(id, venue, status, Now.AddMinutes(-startedMinutesAgo), Now.AddMinutes(endsInMinutes), null, null);

    private static MyShift ClockedIn(int id, string venue, int startedMinutesAgo = 30, int endsInMinutes = 90) =>
        new(id, venue, "active", Now.AddMinutes(-startedMinutesAgo), Now.AddMinutes(endsInMinutes), Now.AddMinutes(-startedMinutesAgo), null);

    private static ShiftVenueChoice Pick(params MyShift[] shifts) => ShiftVenuePicker.Pick(Now, shifts);

    [Fact]
    public void One_running_shift_picks_its_venue()
    {
        Assert.Equal(new ShiftVenueChoice(ShiftVenueChoiceKind.Venue, "a"), Pick(Scheduled(1, "a", 10, 110)));
    }

    [Fact]
    public void No_shifts_picks_nothing()
    {
        Assert.Equal(ShiftVenueChoiceKind.None, Pick().Kind);
    }

    [Fact]
    public void Shifts_that_have_not_started_or_have_ended_pick_nothing()
    {
        Assert.Equal(ShiftVenueChoiceKind.None, Pick(Scheduled(1, "a", -30, 150), Scheduled(2, "b", 200, -20)).Kind);
    }

    [Fact]
    public void A_shift_starting_this_instant_is_running_and_one_ending_this_instant_is_not()
    {
        Assert.Equal("a", Pick(Scheduled(1, "a", 0, 60)).VenueId);
        Assert.Equal(ShiftVenueChoiceKind.None, Pick(Scheduled(2, "a", 60, 0)).Kind);
    }

    [Theory]
    [InlineData("pending_approval")]
    [InlineData("open")]
    [InlineData("cancelled")]
    [InlineData("completed")]
    public void Only_approved_shifts_count(string status)
    {
        Assert.Equal(ShiftVenueChoiceKind.None, Pick(Scheduled(1, "a", 10, 110, status)).Kind);
    }

    [Fact]
    public void Being_clocked_in_picks_that_venue_even_past_the_scheduled_end()
    {
        Assert.Equal("a", Pick(ClockedIn(1, "a", startedMinutesAgo: 200, endsInMinutes: -20)).VenueId);
    }

    [Fact]
    public void A_clocked_out_shift_is_not_working()
    {
        var done = new MyShift(1, "a", "active", Now.AddMinutes(-60), Now.AddMinutes(60), Now.AddMinutes(-60), Now.AddMinutes(-1));
        Assert.Equal(ShiftVenueChoiceKind.None, Pick(done).Kind);
    }

    [Fact]
    public void Being_clocked_in_at_one_venue_beats_a_shift_running_at_another()
    {
        var choice = Pick(ClockedIn(1, "a"), Scheduled(2, "b", 10, 110));
        Assert.Equal(new ShiftVenueChoice(ShiftVenueChoiceKind.Venue, "a"), choice);
    }

    [Fact]
    public void Two_running_shifts_at_one_venue_are_not_a_conflict()
    {
        Assert.Equal(new ShiftVenueChoice(ShiftVenueChoiceKind.Venue, "a"), Pick(Scheduled(1, "a", 10, 110), Scheduled(2, "a", 5, 60)));
    }

    [Fact]
    public void Running_shifts_at_two_venues_are_a_conflict_and_pick_none()
    {
        Assert.Equal(new ShiftVenueChoice(ShiftVenueChoiceKind.Conflict), Pick(Scheduled(1, "a", 10, 110), Scheduled(2, "b", 5, 60)));
    }

    [Fact]
    public void Being_clocked_in_at_two_venues_is_a_conflict()
    {
        Assert.Equal(ShiftVenueChoiceKind.Conflict, Pick(ClockedIn(1, "a"), ClockedIn(2, "b")).Kind);
    }

    private static readonly ShiftVenueChoice None = new(ShiftVenueChoiceKind.None);
    private static readonly ShiftVenueChoice OneVenue = new(ShiftVenueChoiceKind.Venue, "a");
    private static readonly ShiftVenueChoice Conflict = new(ShiftVenueChoiceKind.Conflict);

    [Fact]
    public void A_conflict_is_announced_when_it_starts()
    {
        Assert.True(ShiftVenuePicker.EnteredConflict(null, Conflict));
        Assert.True(ShiftVenuePicker.EnteredConflict(None, Conflict));
        Assert.True(ShiftVenuePicker.EnteredConflict(OneVenue, Conflict));
    }

    [Fact]
    public void A_conflict_that_carries_on_is_not_announced_again()
    {
        Assert.False(ShiftVenuePicker.EnteredConflict(Conflict, Conflict));
    }

    [Fact]
    public void Leaving_a_conflict_or_never_having_one_announces_nothing()
    {
        Assert.False(ShiftVenuePicker.EnteredConflict(Conflict, OneVenue));
        Assert.False(ShiftVenuePicker.EnteredConflict(Conflict, None));
        Assert.False(ShiftVenuePicker.EnteredConflict(null, OneVenue));
        Assert.False(ShiftVenuePicker.EnteredConflict(None, None));
    }

    [Fact]
    public void A_conflict_that_clears_and_comes_back_is_announced_again()
    {
        Assert.False(ShiftVenuePicker.EnteredConflict(Conflict, OneVenue));
        Assert.True(ShiftVenuePicker.EnteredConflict(OneVenue, Conflict));
    }

    [Fact]
    public void A_later_shift_at_another_venue_does_not_make_a_conflict_yet()
    {
        Assert.Equal(new ShiftVenueChoice(ShiftVenueChoiceKind.Venue, "a"), Pick(Scheduled(1, "a", 10, 110), Scheduled(2, "b", -120, 240)));
    }
}
