using Dalamud.Bindings.ImGui;
using Silk.NET.Input;

namespace VenueManager.Harness;

public static class InputMap
{
    private static readonly Dictionary<Key, ImGuiKey> Keys = Build();

    public static ImGuiKey? MapKey(Key key) => Keys.TryGetValue(key, out var mapped) ? mapped : null;

    public static (float X, float Y) ToHost(float mouseX, float mouseY, float windowWidth, float windowHeight, int hostWidth, int hostHeight) =>
        (mouseX * hostWidth / windowWidth, mouseY * hostHeight / windowHeight);

    public static int? MapButton(MouseButton button) => button switch
    {
        MouseButton.Left => 0,
        MouseButton.Right => 1,
        MouseButton.Middle => 2,
        _ => null,
    };

    private static Dictionary<Key, ImGuiKey> Build()
    {
        var map = new Dictionary<Key, ImGuiKey>
        {
            [Key.Left] = ImGuiKey.LeftArrow,
            [Key.Right] = ImGuiKey.RightArrow,
            [Key.Up] = ImGuiKey.UpArrow,
            [Key.Down] = ImGuiKey.DownArrow,
            [Key.Home] = ImGuiKey.Home,
            [Key.End] = ImGuiKey.End,
            [Key.PageUp] = ImGuiKey.PageUp,
            [Key.PageDown] = ImGuiKey.PageDown,
            [Key.Insert] = ImGuiKey.Insert,
            [Key.Delete] = ImGuiKey.Delete,
            [Key.Backspace] = ImGuiKey.Backspace,
            [Key.Enter] = ImGuiKey.Enter,
            [Key.KeypadEnter] = ImGuiKey.KeypadEnter,
            [Key.Tab] = ImGuiKey.Tab,
            [Key.Escape] = ImGuiKey.Escape,
            [Key.Space] = ImGuiKey.Space,
            [Key.ControlLeft] = ImGuiKey.LeftCtrl,
            [Key.ControlRight] = ImGuiKey.RightCtrl,
            [Key.ShiftLeft] = ImGuiKey.LeftShift,
            [Key.ShiftRight] = ImGuiKey.RightShift,
            [Key.AltLeft] = ImGuiKey.LeftAlt,
            [Key.AltRight] = ImGuiKey.RightAlt,
            [Key.SuperLeft] = ImGuiKey.LeftSuper,
            [Key.SuperRight] = ImGuiKey.RightSuper,
        };

        for (var i = 0; i < 26; i++) map[Key.A + i] = ImGuiKey.A + i;
        for (var i = 0; i < 10; i++) map[Key.Number0 + i] = ImGuiKey.Key0 + i;
        for (var i = 0; i < 12; i++) map[Key.F1 + i] = ImGuiKey.F1 + i;
        return map;
    }
}
