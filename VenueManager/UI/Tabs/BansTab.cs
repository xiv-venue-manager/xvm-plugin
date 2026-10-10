using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using VenueManager.UI;

namespace VenueManager.Tabs;

public class BansTab : ITab
{
  private readonly Plugin plugin;

  public string Name => "Bans";
  public FontAwesomeIcon Icon => FontAwesomeIcon.Ban;
  public string Tooltip => "Banned patrons";
  public bool IsVisible => true;

  private string filter = "";
  private bool loading = false;
  private DateTime lastFetch = DateTime.MinValue;

  private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

  public BansTab(Plugin plugin)
  {
    this.plugin = plugin;
  }

  public void draw()
  {
    ImGui.BeginChild(1);

    if (!ThemeManager.RequireXivAppReady(plugin))
    {
      ImGui.EndChild();
      return;
    }

    ThemeManager.PollGate(loading, lastFetch, RefreshInterval, () => { _ = FetchBansAsync(); });

    if (plugin.xivAppBannedPatronsUnavailable)
      ImGui.TextColored(Colors.XivRed, "Ban list unavailable. Check your account link in Settings.");

    var bans = plugin.xivAppBannedPatrons;
    ImGui.TextColored(Colors.XivSubtext0, $"{bans.Count} banned at this venue");
    ImGui.InputTextWithHint("##banfilter", "Filter by name, world or reason", ref filter, 64);
    ImGui.Separator();

    if (bans.Count == 0 && !loading)
    {
      ThemeManager.EmptyState("No bans at this venue.");
      ImGui.EndChild();
      return;
    }

    var shown = bans
      .Where(b => filter.Length == 0
        || b.CharacterName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || b.World.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || b.Reason.Contains(filter, StringComparison.OrdinalIgnoreCase))
      .OrderBy(b => b.CharacterName, StringComparer.OrdinalIgnoreCase)
      .ToList();

    if (ImGui.BeginTable("Bans", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
    {
      ImGui.TableSetupColumn("Name");
      ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 90);
      ImGui.TableSetupColumn("Reason");
      ImGui.TableHeadersRow();

      foreach (var ban in shown)
      {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(Colors.XivRed, ban.CharacterName);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(ban.World);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(string.IsNullOrEmpty(ban.Reason) ? "(no reason given)" : ban.Reason);
      }
      ImGui.EndTable();
    }

    ImGui.EndChild();
  }

  private async Task FetchBansAsync()
  {
    var venueId = plugin.currentXivAppVenueId;
    if (plugin.xivAppClient == null || string.IsNullOrEmpty(venueId)) return;

    loading = true;
    try
    {
      var bans = await plugin.xivAppClient.Venue.GetBannedPatronsAsync(venueId);
      if (bans != null) plugin.xivAppBannedPatrons = bans;
      plugin.xivAppBannedPatronsUnavailable = bans == null;
      lastFetch = DateTime.Now;
    }
    catch (Exception ex)
    {
      Plugin.Log.Warning($"Error fetching bans: {ex.Message}");
    }
    finally
    {
      loading = false;
    }
  }
}
