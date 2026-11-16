using Dalamud.Bindings.ImGui;
using Silk.NET.Input;
using VenueManager.Harness;
using Xunit;

public class InputMapTests
{
    [Theory]
    [InlineData(Key.A, ImGuiKey.A)]
    [InlineData(Key.Z, ImGuiKey.Z)]
    [InlineData(Key.Number0, ImGuiKey.Key0)]
    [InlineData(Key.Number9, ImGuiKey.Key9)]
    [InlineData(Key.F1, ImGuiKey.F1)]
    [InlineData(Key.F12, ImGuiKey.F12)]
    [InlineData(Key.Left, ImGuiKey.LeftArrow)]
    [InlineData(Key.Down, ImGuiKey.DownArrow)]
    [InlineData(Key.Home, ImGuiKey.Home)]
    [InlineData(Key.PageDown, ImGuiKey.PageDown)]
    [InlineData(Key.Insert, ImGuiKey.Insert)]
    [InlineData(Key.Delete, ImGuiKey.Delete)]
    [InlineData(Key.Backspace, ImGuiKey.Backspace)]
    [InlineData(Key.Enter, ImGuiKey.Enter)]
    [InlineData(Key.KeypadEnter, ImGuiKey.KeypadEnter)]
    [InlineData(Key.Tab, ImGuiKey.Tab)]
    [InlineData(Key.Escape, ImGuiKey.Escape)]
    [InlineData(Key.Space, ImGuiKey.Space)]
    [InlineData(Key.ControlLeft, ImGuiKey.LeftCtrl)]
    [InlineData(Key.ShiftRight, ImGuiKey.RightShift)]
    [InlineData(Key.AltLeft, ImGuiKey.LeftAlt)]
    [InlineData(Key.SuperRight, ImGuiKey.RightSuper)]
    public void MapKey_maps_known_keys(Key key, ImGuiKey expected) => Assert.Equal(expected, InputMap.MapKey(key));

    [Theory]
    [InlineData(Key.Unknown)]
    [InlineData(Key.CapsLock)]
    [InlineData(Key.F13)]
    [InlineData(Key.PrintScreen)]
    public void MapKey_returns_null_for_unmapped_keys(Key key) => Assert.Null(InputMap.MapKey(key));

    [Fact]
    public void ToHost_is_identity_when_sizes_match()
    {
        Assert.Equal((120f, 45f), InputMap.ToHost(120, 45, 900, 700, 900, 700));
    }

    [Fact]
    public void ToHost_scales_a_double_size_window_down()
    {
        Assert.Equal((100f, 50f), InputMap.ToHost(200, 100, 1800, 1400, 900, 700));
    }

    [Fact]
    public void ToHost_scales_each_axis_on_its_own()
    {
        Assert.Equal((450f, 175f), InputMap.ToHost(300, 100, 600, 400, 900, 700));
    }

    [Theory]
    [InlineData(MouseButton.Left, 0)]
    [InlineData(MouseButton.Right, 1)]
    [InlineData(MouseButton.Middle, 2)]
    public void MapButton_maps_the_three_main_buttons(MouseButton button, int expected) => Assert.Equal(expected, InputMap.MapButton(button));

    [Fact]
    public void MapButton_returns_null_for_other_buttons() => Assert.Null(InputMap.MapButton(MouseButton.Button4));
}
