using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace VenueManager.UI;

public static class IconFont
{
  public static Func<ImFontPtr> Font { get; set; } = () => UiBuilder.IconFont;

  public static Func<FontAwesomeIcon, bool> IconButton { get; set; } = icon => ImGuiComponents.IconButton(icon);

  public static Func<string, FontAwesomeIcon, bool> IconButtonWithId { get; set; } = (id, icon) => ImGuiComponents.IconButton(id, icon);
}
