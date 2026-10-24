using System.Linq;
using VenueManager;
using Xunit;

public class XvmApiMappingTests
{
    [Theory]
    [InlineData("scheduled", "SCHEDULED")]
    [InlineData("active", "ACTIVE")]
    [InlineData("completed", "COMPLETED")]
    [InlineData("pending_approval", "PENDING")]
    [InlineData("missed", "MISSED")]
    [InlineData("cancelled", "CANCELLED")]
    [InlineData("something_new", "SOMETHING_NEW")]
    public void ShiftStatus_uses_the_vocabulary_the_shifts_tab_acts_on(string xvm, string expected)
    {
        Assert.Equal(expected, XvmApiMapping.ShiftStatus(xvm));
    }

    [Fact]
    public void Venues_keep_id_name_slug_and_show_the_tier_as_the_role()
    {
        var venues = XvmApiMapping.MapVenues(new[]
        {
            new XvmMyVenue(new XvmVenue("ven_1", "The Velvet Lotus", "velvet-lotus"), "owner", 11, new List<int>()),
            new XvmMyVenue(new XvmVenue("ven_2", "Moon Bar", "moon-bar"), "staff", 12, null),
        });

        Assert.Equal(2, venues.Count);
        Assert.Equal("ven_1", venues[0].Id);
        Assert.Equal("The Velvet Lotus", venues[0].Name);
        Assert.Equal("velvet-lotus", venues[0].Slug);
        Assert.Equal("OWNER", venues[0].Role);
        Assert.Equal("STAFF", venues[1].Role);
        Assert.Empty(venues[0].Addresses);
    }

    [Fact]
    public void Shifts_become_the_plugins_dtos_with_ids_as_strings()
    {
        var mine = new[]
        {
            new XvmShift(7, 3, "2026-10-10T19:00:00Z", "2026-10-10T23:00:00Z", null, null, "bring the shout", "scheduled"),
            new XvmShift(8, null, "2026-10-09T19:00:00Z", "2026-10-09T23:00:00Z", "2026-10-09T19:02:00Z", "2026-10-09T23:10:00Z", null, "completed"),
        };

        var result = XvmApiMapping.MapShifts(mine, new XvmBoardShift[0]);

        Assert.Equal(2, result.Shifts.Count);
        Assert.Equal("7", result.Shifts[0].Id);
        Assert.Equal("SCHEDULED", result.Shifts[0].Status);
        Assert.Equal("2026-10-10T19:00:00Z", result.Shifts[0].ScheduledStart);
        Assert.Equal("bring the shout", result.Shifts[0].Notes);
        Assert.Null(result.Shifts[0].ActualStart);
        Assert.Equal("COMPLETED", result.Shifts[1].Status);
        Assert.Equal("2026-10-09T23:10:00Z", result.Shifts[1].ActualEnd);
    }

    [Fact]
    public void A_shift_with_no_scheduled_times_gets_empty_strings_not_nulls()
    {
        var walkIn = new XvmShift(9, null, null, null, "2026-10-10T20:00:00Z", null, null, "active");
        var result = XvmApiMapping.MapShifts(new[] { walkIn }, new XvmBoardShift[0]);
        Assert.Equal("", result.Shifts[0].ScheduledStart);
        Assert.Equal("", result.Shifts[0].ScheduledEnd);
        Assert.Equal("ACTIVE", result.Shifts[0].Status);
    }

    [Fact]
    public void Open_shifts_carry_their_position_name()
    {
        var open = new[]
        {
            new XvmBoardShift(20, "2026-10-11T19:00:00Z", "2026-10-11T23:00:00Z", "Shout runner"),
            new XvmBoardShift(21, "2026-10-12T19:00:00Z", "2026-10-12T23:00:00Z", "Bartender"),
        };

        var result = XvmApiMapping.MapShifts(new XvmShift[0], open);

        Assert.Equal("20", result.OpenShifts[0].Id);
        Assert.Equal("Shout runner", result.OpenShifts[0].RoleName);
        Assert.Equal("2026-10-12T19:00:00Z", result.OpenShifts[1].ScheduledStart);
    }

    [Fact]
    public void An_open_shift_the_caller_already_holds_a_seat_on_is_left_off_the_board()
    {
        var mine = new[] { new XvmShift(7, 3, "2026-10-11T19:00:00Z", "2026-10-11T23:00:00Z", null, null, null, "scheduled", 20) };
        var open = new[]
        {
            new XvmBoardShift(20, "2026-10-11T19:00:00Z", "2026-10-11T23:00:00Z", "Shout runner"),
            new XvmBoardShift(21, "2026-10-12T19:00:00Z", "2026-10-12T23:00:00Z", "Bartender"),
        };

        var result = XvmApiMapping.MapShifts(mine, open);

        Assert.Equal(new[] { "21" }, result.OpenShifts.Select(s => s.Id));
    }

    [Fact]
    public void Roles_are_the_positions_the_callers_membership_holds()
    {
        var positions = new[] { new XvmPosition(3, "Bartender"), new XvmPosition(4, "Host"), new XvmPosition(5, "Shout runner") };

        var roles = XvmApiMapping.MapRoles(new List<int> { 3, 5 }, positions);

        Assert.Equal(new[] { "3", "5" }, roles.Select(r => r.Id));
        Assert.Equal(new[] { "Bartender", "Shout runner" }, roles.Select(r => r.Name));
    }

    [Fact]
    public void No_position_ids_or_an_unknown_position_gives_no_role()
    {
        Assert.Empty(XvmApiMapping.MapRoles(null, new[] { new XvmPosition(3, "Bartender") }));
        Assert.Empty(XvmApiMapping.MapRoles(new List<int> { 99 }, new[] { new XvmPosition(3, "Bartender") }));
    }

    [Fact]
    public void Services_carry_price_as_gil_text_category_name_and_linked_item()
    {
        var services = new[]
        {
            new XvmService(11, "House Cocktail", "Fruity", 1500, 2, new XvmInventory(4825, "Grilled Tuna", 21545, 12)),
            new XvmService(12, "Dance", null, null, null, null),
        };
        var categories = new[] { new XvmCategory(2, "Drinks") };

        var result = XvmApiMapping.MapServices(services, categories);

        Assert.Equal("11", result.Services[0].Id);
        Assert.Equal("1500", result.Services[0].Price);
        Assert.Equal("Drinks", result.Services[0].Category);
        Assert.Equal(4825, result.Services[0].LinkedItemId);
        Assert.Equal("Grilled Tuna", result.Services[0].LinkedItemName);
        Assert.Equal(21545, result.Services[0].LinkedItemIcon);
        Assert.Equal(12, result.Services[0].StockCount);
        Assert.Equal("0", result.Services[1].Price);
        Assert.Null(result.Services[1].Category);
        Assert.Null(result.Services[1].LinkedItemId);
    }

    [Fact]
    public void Bans_use_an_empty_reason_when_none_was_given()
    {
        var bans = XvmApiMapping.MapBanned(new[]
        {
            new XvmBanned("Some Person", "Cactuar", "harassment"),
            new XvmBanned("Other Person", "Faerie", null),
        });

        Assert.Equal("Some Person", bans[0].CharacterName);
        Assert.Equal("Cactuar", bans[0].World);
        Assert.Equal("harassment", bans[0].Reason);
        Assert.Equal("", bans[1].Reason);
    }

    [Fact]
    public void An_event_means_active_and_none_means_not_active()
    {
        var active = XvmApiMapping.MapActiveEvent(new XvmEvent(42, "Friday Night"));
        Assert.True(active.Active);
        Assert.Equal("42", active.EventId);
        Assert.Equal("Friday Night", active.Title);
        Assert.Equal("ACTIVE", active.Status);

        var none = XvmApiMapping.MapActiveEvent(null);
        Assert.False(none.Active);
        Assert.Null(none.EventId);
    }

    [Fact]
    public void Rooms_are_occupied_when_reserved_and_fall_back_to_a_number_for_their_name()
    {
        var rooms = XvmApiMapping.MapRooms(new[]
        {
            new XvmRoom(5, "Moonlit Suite", "back stairs", 2, false, false, "reserved"),
            new XvmRoom(6, null, null, 3, true, false, "locked"),
            new XvmRoom(7, "Attic", null, null, false, true, "disabled"),
        });

        Assert.True(rooms[0].IsOccupied);
        Assert.Equal("5", rooms[0].Id);
        Assert.Equal("Moonlit Suite", rooms[0].Name);
        Assert.Equal("back stairs", rooms[0].Note);
        Assert.Equal(2, rooms[0].RoomNumber);
        Assert.False(rooms[1].IsOccupied);
        Assert.True(rooms[1].Locked);
        Assert.Equal("Room 3", rooms[1].Name);
        Assert.True(rooms[2].Disabled);
        Assert.Equal(0, rooms[2].RoomNumber);
    }

}
