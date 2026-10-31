using VenueManager;
using Xunit;

public class ShiftPromptPlannerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);

    private static MyShift Shift(int id, string status, int startMinutesAgo, int endMinutesFromNow, bool clockedIn = false, bool clockedOut = false) =>
        new(id, "v1", status,
            Now.AddMinutes(-startMinutesAgo), Now.AddMinutes(endMinutesFromNow),
            clockedIn ? Now.AddMinutes(-startMinutesAgo) : null,
            clockedOut ? Now : null);

    private static List<ShiftPrompt> Plan(IEnumerable<MyShift> shifts, Dictionary<int, PromptMemory>? memory = null, HashSet<int>? muted = null) =>
        ShiftPromptPlanner.Due(Now, shifts, memory ?? new(), muted ?? new());

    [Fact]
    public void A_running_shift_that_is_not_clocked_in_gets_one_start_prompt()
    {
        var due = Plan(new[] { Shift(1, "scheduled", 2, 120) });
        Assert.Equal(new ShiftPrompt(1, "v1", PromptKind.Start), Assert.Single(due));

        var remembered = new Dictionary<int, PromptMemory> { [1] = new(PromptKind.Start, Now) };
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", 2, 120) }, remembered));
    }

    [Fact]
    public void A_shift_more_than_thirty_minutes_away_gets_no_prompt()
    {
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", -31, 120) }));
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", -45, 120) }));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(10)]
    [InlineData(1)]
    public void A_shift_within_thirty_minutes_of_its_start_gets_one_heads_up(int minutesAway)
    {
        var shift = Shift(1, "scheduled", -minutesAway, 120);
        Assert.Equal(new ShiftPrompt(1, "v1", PromptKind.Soon, Now.AddMinutes(minutesAway)), Assert.Single(Plan(new[] { shift })));

        var remembered = new Dictionary<int, PromptMemory> { [1] = new(PromptKind.Soon, Now) };
        Assert.Empty(Plan(new[] { shift }, remembered));
    }

    [Fact]
    public void The_start_prompt_still_follows_the_heads_up()
    {
        var remembered = new Dictionary<int, PromptMemory> { [1] = new(PromptKind.Soon, Now.AddMinutes(-30)) };
        Assert.Equal(new ShiftPrompt(1, "v1", PromptKind.Start), Assert.Single(Plan(new[] { Shift(1, "scheduled", 0, 120) }, remembered)));
    }

    [Fact]
    public void A_muted_shift_gets_no_heads_up()
    {
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", -10, 120) }, muted: new HashSet<int> { 1 }));
    }

    [Theory]
    [InlineData("pending_approval")]
    [InlineData("open")]
    [InlineData("cancelled")]
    [InlineData("completed")]
    public void Only_approved_shifts_get_a_heads_up(string status)
    {
        Assert.Empty(Plan(new[] { Shift(1, status, -10, 120) }));
    }

    [Fact]
    public void A_shift_that_is_already_clocked_in_gets_no_heads_up()
    {
        var early = new MyShift(1, "v1", "scheduled", Now.AddMinutes(10), Now.AddMinutes(130), Now.AddMinutes(-5), null);
        Assert.Empty(Plan(new[] { early }));
    }

    [Fact]
    public void Each_shift_gets_its_own_heads_up()
    {
        var due = Plan(new[] { Shift(1, "scheduled", -10, 120), Shift(2, "scheduled", -20, 120) });
        Assert.Equal(new[] { 1, 2 }, due.Select(p => p.ShiftId));
        Assert.All(due, p => Assert.Equal(PromptKind.Soon, p.Kind));
    }

    [Fact]
    public void A_shift_that_has_already_ended_unclocked_gets_no_start_prompt()
    {
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", 180, -5) }));
    }

    [Theory]
    [InlineData("pending_approval")]
    [InlineData("open")]
    [InlineData("cancelled")]
    [InlineData("completed")]
    public void Only_approved_shifts_prompt(string status)
    {
        Assert.Empty(Plan(new[] { Shift(1, status, 2, 120) }));
        Assert.Empty(Plan(new[] { Shift(2, status, 200, -5, clockedIn: true) }));
    }

    [Fact]
    public void A_clocked_in_shift_past_its_end_gets_an_end_prompt_that_repeats_every_fifteen_minutes()
    {
        var shift = Shift(1, "active", 200, -5, clockedIn: true);
        Assert.Equal(new ShiftPrompt(1, "v1", PromptKind.End), Assert.Single(Plan(new[] { shift })));

        var justShown = new Dictionary<int, PromptMemory> { [1] = new(PromptKind.End, Now.AddMinutes(-10)) };
        Assert.Empty(Plan(new[] { shift }, justShown));

        var longAgo = new Dictionary<int, PromptMemory> { [1] = new(PromptKind.End, Now.AddMinutes(-16)) };
        Assert.Single(Plan(new[] { shift }, longAgo));
    }

    [Fact]
    public void A_clocked_in_shift_before_its_end_gets_no_prompt()
    {
        Assert.Empty(Plan(new[] { Shift(1, "active", 60, 30, clockedIn: true) }));
    }

    [Fact]
    public void An_end_prompt_stops_twelve_hours_after_the_end()
    {
        Assert.Single(Plan(new[] { Shift(1, "active", 800, -(11 * 60), clockedIn: true) }));
        Assert.Empty(Plan(new[] { Shift(1, "active", 1000, -(13 * 60), clockedIn: true) }));
    }

    [Fact]
    public void A_shift_clocked_out_gets_no_end_prompt()
    {
        Assert.Empty(Plan(new[] { Shift(1, "active", 200, -5, clockedIn: true, clockedOut: true) }));
    }

    [Fact]
    public void A_muted_shift_never_prompts_again()
    {
        var muted = new HashSet<int> { 1 };
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", 2, 120), Shift(1, "active", 200, -5, clockedIn: true) }, muted: muted));
    }

    [Fact]
    public void Shifts_at_different_venues_each_get_their_own_prompt()
    {
        var shifts = new[]
        {
            Shift(1, "scheduled", 2, 120) with { VenueId = "v1" },
            Shift(2, "active", 200, -5, clockedIn: true) with { VenueId = "v2" },
        };
        var due = Plan(shifts);
        Assert.Contains(new ShiftPrompt(1, "v1", PromptKind.Start), due);
        Assert.Contains(new ShiftPrompt(2, "v2", PromptKind.End), due);
    }
}
