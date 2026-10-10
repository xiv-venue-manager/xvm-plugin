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
    public void A_shift_that_has_not_started_yet_gets_no_prompt()
    {
        Assert.Empty(Plan(new[] { Shift(1, "scheduled", -10, 120) }));
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
