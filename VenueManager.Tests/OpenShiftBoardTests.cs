using VenueManager;
using Xunit;

public class OpenShiftBoardTests
{
    private static OpenShiftDto Open(string id, string? role, string start) =>
        new() { Id = id, RoleName = role, ScheduledStart = start };

    private static Role Held(string name) => new() { Id = name, Name = name };

    [Fact]
    public void Hides_shifts_for_roles_the_caller_does_not_hold()
    {
        var shifts = new[] { Open("1", "Bartender", "2026-10-11T19:00:00Z"), Open("2", "Host", "2026-10-11T20:00:00Z") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "2" }, visible.Select(s => s.Id));
    }

    [Fact]
    public void Matches_the_role_name_ignoring_case()
    {
        var shifts = new[] { Open("1", "bartender", "2026-10-11T19:00:00Z") };

        Assert.Single(OpenShiftBoard.Visible(shifts, new[] { Held("Bartender") }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hides_shifts_with_no_role_name(string? role)
    {
        var shifts = new[] { Open("1", role, "2026-10-11T19:00:00Z") };

        Assert.Empty(OpenShiftBoard.Visible(shifts, new[] { Held("Bartender") }));
    }

    [Fact]
    public void Shows_nothing_when_the_caller_holds_no_roles()
    {
        var shifts = new[] { Open("1", "Bartender", "2026-10-11T19:00:00Z") };

        Assert.Empty(OpenShiftBoard.Visible(shifts, Array.Empty<Role>()));
    }

    [Fact]
    public void Orders_by_start_time()
    {
        var shifts = new[] { Open("late", "Host", "2026-10-12T19:00:00Z"), Open("early", "Host", "2026-10-11T19:00:00Z") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "early", "late" }, visible.Select(s => s.Id));
    }

    [Fact]
    public void A_shift_with_an_unparseable_start_sorts_first()
    {
        var shifts = new[] { Open("ok", "Host", "2026-10-11T19:00:00Z"), Open("bad", "Host", "nope") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "bad", "ok" }, visible.Select(s => s.Id));
    }
}
