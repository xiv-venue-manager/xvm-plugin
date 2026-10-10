using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;

using System.Drawing;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Memory;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Map = Lumina.Excel.Sheets.Map;
using VenueManager.UI;

namespace VenueManager.Tabs;

public class SettingsTab : ITab
{
  private Plugin plugin;
  private Configuration configuration;

  public string Name => "Settings";
  public FontAwesomeIcon Icon => FontAwesomeIcon.Cog;
  public string Tooltip => "Settings";
  public bool IsVisible => true;

  // Status line under Fetch Venues — set by the async Fetch* methods below
  private string xivAppStatus = "";
  private static readonly HttpClient XvmHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
  private string pairingCode = "";
  private bool linking = false;
  private string linkStatus = "";
  private Vector4 linkStatusColor = Colors.StatusWarn;
  private bool charactersAttempted = false;
  private bool charactersBusy = false;
  private string characterStatus = "";
  private Vector4 characterStatusColor = Colors.StatusWarn;
  private Vector4 xivAppStatusColor = Colors.XivOverlay0;

  // Not persisted; default hidden so screenshots/shares don't leak the key


  private static Vector4 StatusOk   => Colors.StatusOk;
  private static Vector4 StatusWarn => Colors.StatusWarn;
  private static Vector4 StatusErr  => Colors.StatusErr;

  public SettingsTab(Plugin plugin)
  {
    this.plugin = plugin;
    this.configuration = plugin.Configuration;
  }

  public unsafe void draw()
  {
    ImGui.BeginChild("SettingsRoot");
    ImGui.Indent(5);

    if (string.IsNullOrEmpty(this.configuration.xvmApiSecret))
      DrawWelcomeBanner();

    // Primary workflow — surface first so a fresh install lands on the setup it needs
    DrawSetupChecklist();
    DrawAccountLink();
    DrawXivAppSettings();

    DrawSectionSeparator();
    DrawSectionHeader("Status Bar (DTR)");
    DrawDtrSettings();

    DrawSectionSeparator();
    DrawSectionHeader("Tab Visibility");
    DrawTabVisibility();

    DrawSectionSeparator();
    DrawSectionHeader("Patron List");
    DrawPatronListSettings();

    DrawSectionSeparator();
    DrawBanAlerts();

    DrawSectionSeparator();
    DrawShiftPrompts();

    DrawSectionSeparator();
    DrawPatronChatAlerts();

    DrawSectionSeparator();
    DrawGreeterSettings();

    DrawSectionSeparator();
    DrawPatronSoundAlerts();

    DrawSectionSeparator();
    DrawDebugInfo();

    DrawSectionSeparator();
    DrawAboutSection();

    ImGui.Unindent();
    ImGui.EndChild();
  }

  private void DrawAboutSection()
  {
    DrawSectionHeader("About");
    ImGui.TextColored(Colors.XivSubtext0, $"XIV Venue Manager Sync  v{plugin.PluginVersion}  by Ehno");
    ImGui.Spacing();
    float btnW = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2f;
    if (ImGui.Button("What's New", new Vector2(btnW, 0)))
      plugin.OpenChangelog();
    ImGui.SameLine();
    if (ImGui.Button("GitHub", new Vector2(btnW, 0)))
      System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
        FileName = "https://github.com/BluntEXE/XIVVenueManagerSync",
        UseShellExecute = true
      });
  }

  // -- Section helpers ------------------------------------------------------

  private static void DrawSectionSeparator()
  {
    ImGui.Separator();
    ImGui.Spacing();
  }

  private static void DrawSectionHeader(string label)
  {
    ImGui.TextColored(Colors.XivBlue, label);
  }

  // -- Tab Visibility -------------------------------------------------------

  private void DrawTabVisibility()
  {
    var showGuestsTab = this.configuration.showGuestsTab;
    if (ImGui.Checkbox("Patrons Tab", ref showGuestsTab))
    {
      this.configuration.showGuestsTab = showGuestsTab;
      this.configuration.Save();
    }
    ImGui.Indent(20);

    if (!this.configuration.showGuestsTab) ImGui.BeginDisabled();
    ImGui.TextWrapped("Hiding the Patrons Tab also disables chat/sound alerts and stops logging patron visits to the XVM Website entirely - no Live dashboard or Analytics data will be recorded while this is off.");
    if (!this.configuration.showGuestsTab) ImGui.EndDisabled();

    ImGui.Unindent();
    var showVenueTab = this.configuration.showVenueTab;
    if (ImGui.Checkbox("Venues Tab", ref showVenueTab))
    {
      this.configuration.showVenueTab = showVenueTab;
      this.configuration.Save();
    }

    if (!this.configuration.showGuestsTab && !this.configuration.showVenueTab)
    {
      ImGui.TextColored(Colors.StripMuted, "Both tabs hidden — re-enable at least one to see patron data.");
    }
  }

  // -- Status Bar (DTR) -----------------------------------------------------
  // Only owns content — Dalamud handles show/hide + reordering via /xlsettings
  private void DrawDtrSettings()
  {
    ImGui.TextWrapped("Show a short plugin status in the game's Server Info Bar (where world/time displays). Reorder or hide via Dalamud settings → Server Info Bar.");

    var mode = this.configuration.dtrDisplayMode;
    if (ImGui.BeginCombo("Display##dtrMode", DtrLabel(mode)))
    {
      foreach (var option in (DtrDisplayMode[])Enum.GetValues(typeof(DtrDisplayMode)))
      {
        var selected = option == mode;
        if (ImGui.Selectable(DtrLabel(option), selected))
        {
          this.configuration.dtrDisplayMode = option;
          this.configuration.Save();
          this.plugin.UpdateDtrBar(force: true);
        }
        if (selected) ImGui.SetItemDefaultFocus();
      }
      ImGui.EndCombo();
    }

    ImGui.TextColored(Colors.StripMuted, DtrDescription(mode));
  }

  private static string DtrLabel(DtrDisplayMode mode) => mode switch
  {
    DtrDisplayMode.Disabled     => "Disabled",
    DtrDisplayMode.PatronCount  => "Patron count",
    DtrDisplayMode.VenueName    => "Venue name",
    DtrDisplayMode.SessionSales => "Session sales",
    DtrDisplayMode.ShiftStatus  => "Shift status",
    DtrDisplayMode.Combined     => "Combined",
    _ => mode.ToString(),
  };

  private static string DtrDescription(DtrDisplayMode mode) => mode switch
  {
    DtrDisplayMode.Disabled     => "Nothing shown in the Server Info Bar.",
    DtrDisplayMode.PatronCount  => "Patron count while inside a venue, e.g. \"VM: 12 patrons\".",
    DtrDisplayMode.VenueName    => "Name of the current venue (falls back to ward/plot if unlinked).",
    DtrDisplayMode.SessionSales => "Running tally of sales logged this session.",
    DtrDisplayMode.ShiftStatus  => "Clock-in status: \"On shift 1h23m\", \"Shift in 45m\", or \"Off shift\".",
    DtrDisplayMode.Combined     => "Shift • patrons • venue • sales • snooze — whichever apply right now.",
    _ => "",
  };

  // -- Patron List ----------------------------------------------------------

  private void DrawPatronListSettings()
  {
    var sortCurrentVisitorsTop = this.configuration.sortCurrentVisitorsTop;
    if (ImGui.Checkbox("Pin current visitors to top", ref sortCurrentVisitorsTop))
    {
      this.configuration.sortCurrentVisitorsTop = sortCurrentVisitorsTop;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered()) {
      ImGui.SetTooltip("Pin current visitors to the top of the patron list");
    }

    var sortFriendsToTop = this.configuration.sortFriendsToTop;
    if (ImGui.Checkbox("Pin friends to top", ref sortFriendsToTop))
    {
      this.configuration.sortFriendsToTop = sortFriendsToTop;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered()) {
      ImGui.SetTooltip("Pin friends to the top of the patron list");
    }
  }

  // -- Chat Alerts ----------------------------------------------------------

  private void DrawShiftPrompts()
  {
    DrawSectionHeader("Shift Prompts");
    var prompts = this.configuration.shiftPrompts;
    if (ImGui.Checkbox("Remind me to clock in and out", ref prompts))
    {
      this.configuration.shiftPrompts = prompts;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Shows a notification when a shift of yours starts or runs past its end, at any venue. It never clocks you in or out by itself.");
    }
  }

  private void DrawBanAlerts()
  {
    DrawSectionHeader("Ban Alerts");
    var alertBanned = this.configuration.alertBannedPatrons;
    if (ImGui.Checkbox("Alert when a banned patron arrives", ref alertBanned))
    {
      this.configuration.alertBannedPatrons = alertBanned;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Shows a chat warning with the ban reason. Works even if chat alerts, snooze or the Patrons tab are turned off.");
    }
  }

  private void DrawPatronChatAlerts()
  {
    // Depends on Patrons tab being visible — it owns the patron events this subtree reads
    if (!this.configuration.showGuestsTab) ImGui.BeginDisabled();

    DrawSectionHeader("Patron Chat Alerts");
    var showChatAlerts = this.configuration.showChatAlerts;
    if (ImGui.Checkbox("Enabled##showChatAlerts", ref showChatAlerts))
    {
      this.configuration.showChatAlerts = showChatAlerts;
      this.configuration.Save();
    }

    if (!this.configuration.showChatAlerts) ImGui.BeginDisabled();
    ImGui.Indent(20);
    var showChatAlertEntry = this.configuration.showChatAlertEntry;
    if (ImGui.Checkbox("Entry Alerts", ref showChatAlertEntry))
    {
      this.configuration.showChatAlertEntry = showChatAlertEntry;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Display chat message when a patron enters a venue");
    }

    var showChatAlertReentry = this.configuration.showChatAlertReentry;
    if (ImGui.Checkbox("Re-entry Alerts", ref showChatAlertReentry))
    {
      this.configuration.showChatAlertReentry = showChatAlertReentry;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Display chat message when a patron re-enters a venue after leaving");
    }

    var showChatAlertAlreadyHere = this.configuration.showChatAlertAlreadyHere;
    if (ImGui.Checkbox("Current Visitors on Entry", ref showChatAlertAlreadyHere))
    {
      this.configuration.showChatAlertAlreadyHere = showChatAlertAlreadyHere;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Display chat message for all current patrons when re-entering a house");
    }

    var showChatAlertLeave = this.configuration.showChatAlertLeave;
    if (ImGui.Checkbox("Leave Alerts", ref showChatAlertLeave))
    {
      this.configuration.showChatAlertLeave = showChatAlertLeave;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Display chat message when a patron leaves");
    }

    // "Include Plugin Name" is a display preference, not an event toggle — visually separated
    ImGui.Spacing();
    ImGui.Separator();
    ImGui.Spacing();

    var showPluginNameInChat = this.configuration.showPluginNameInChat;
    if (ImGui.Checkbox("Include Plugin Name", ref showPluginNameInChat))
    {
      this.configuration.showPluginNameInChat = showPluginNameInChat;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Prefix patron chat alerts with \"[XIV Venue Manager Sync]\" so they stand out from regular chat.");
    }

    if (!this.configuration.showChatAlerts) ImGui.EndDisabled();
    ImGui.Unindent();
    if (!this.configuration.showGuestsTab) ImGui.EndDisabled();
  }

  // -- Greeter Automation ---------------------------------------------------

  private void DrawGreeterSettings()
  {
    if (!this.configuration.showGuestsTab) ImGui.BeginDisabled();

    DrawSectionHeader("Greeter Automation");
    ImGui.TextWrapped("Automatically sends a /tell to each patron when they enter the venue.");
    ImGui.Spacing();

    var enableGreeterMode = this.configuration.enableGreeterMode;
    if (ImGui.Checkbox("Enable Auto-Greeter##greeterMode", ref enableGreeterMode))
    {
      this.configuration.enableGreeterMode = enableGreeterMode;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Sends a /tell to each patron as they enter.\nOnly fires for new entries — not for players already inside when you arrive.");
    }

    if (enableGreeterMode)
    {
      ImGui.Indent(20);
      var greeterMessage = this.configuration.greeterMessage;
      if (ImGui.InputTextWithHint("First visit##greeterMsg", "Welcome! Let us know if you need anything ♥", ref greeterMessage, 400))
      {
        this.configuration.greeterMessage = greeterMessage;
        this.configuration.Save();
      }
      if (ImGui.IsItemHovered())
        ImGui.SetTooltip("Sent on a patron's first entry this session.");
      ImGui.Unindent(20);
    }

    ImGui.Spacing();

    var enableReentryGreeter = this.configuration.enableReentryGreeter;
    if (ImGui.Checkbox("Re-entry Greeter##reentryGreeter", ref enableReentryGreeter))
    {
      this.configuration.enableReentryGreeter = enableReentryGreeter;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
      ImGui.SetTooltip("Sends a different /tell when a patron returns after leaving.\nUseful for DJ announcements or updated venue info.");

    if (enableReentryGreeter)
    {
      ImGui.Indent(20);
      var reentryMsg = this.configuration.reentryGreeterMessage;
      if (ImGui.InputTextWithHint("Re-entry##reentryMsg", "Welcome back!", ref reentryMsg, 400))
      {
        this.configuration.reentryGreeterMessage = reentryMsg;
        this.configuration.Save();
      }
      if (ImGui.IsItemHovered())
        ImGui.SetTooltip("Sent every time a patron re-enters after leaving.");
      ImGui.Unindent(20);
    }

    if (enableGreeterMode || enableReentryGreeter)
    {
      ImGui.Spacing();
      var greeterFreeEnable = this.configuration.greeterFreeEnable;
      if (ImGui.Checkbox("⚠ Free Enable##greeterFree", ref greeterFreeEnable))
      {
        this.configuration.greeterFreeEnable = greeterFreeEnable;
        this.configuration.Save();
      }
      if (ImGui.IsItemHovered())
        ImGui.SetTooltip("Greets in any house you're in, on shift or not, including houses that aren't your venue.\nStill skips players already inside when you arrive.");
    }

    if (!this.configuration.showGuestsTab) ImGui.EndDisabled();
  }

  // -- Sound Alerts ---------------------------------------------------------

  private void DrawPatronSoundAlerts()
  {
    // Gated on showGuestsTab like chat alerts; grayed not removed, so users see why it's inactive
    if (!this.configuration.showGuestsTab) ImGui.BeginDisabled();

    DrawSectionHeader("Patron Sound Alerts");
    var soundAlerts = this.configuration.soundAlerts;
    if (ImGui.Checkbox("Enabled##soundAlerts", ref soundAlerts))
    {
      this.configuration.soundAlerts = soundAlerts;
      this.configuration.Save();
    }
    if (!this.configuration.soundAlerts) ImGui.BeginDisabled();
    if (ImGui.BeginCombo("Doorbell sound", DoorbellSound.DoorbellSoundTypes[(int)configuration.doorbellType]))
    {
      var doorbells = (DOORBELL_TYPE[])Enum.GetValues(typeof(DOORBELL_TYPE));
      for (int i = 0; i < doorbells.Length; i++)
      {
        bool is_selected = configuration.doorbellType == doorbells[i];
        if (ImGui.Selectable(DoorbellSound.DoorbellSoundTypes[i], is_selected))
        {
          configuration.doorbellType = doorbells[i];
          configuration.Save();
          plugin.reloadDoorbell();
        }
        if (is_selected)
          ImGui.SetItemDefaultFocus();
      }
      ImGui.EndCombo();
    }
    ImGui.SameLine();
    if (ImGuiComponents.IconButton(FontAwesomeIcon.Music))
    {
      plugin.playDoorbell();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Play test sound");
    }
    var volume = this.configuration.soundVolume;
    if (ImGui.SliderFloat("Volume", ref volume, 0, 5))
    {
      this.configuration.soundVolume = volume;
      configuration.Save();
      plugin.reloadDoorbell();
    }

    if (!this.configuration.soundAlerts) ImGui.EndDisabled();
    if (!this.configuration.showGuestsTab) ImGui.EndDisabled();
  }

  // -- Debug Info -----------------------------------------------------------

  private unsafe void DrawDebugInfo()
  {
    // Collapsed by default; ReadOnly field gives users a copy-paste target for bug reports
    if (!ImGui.CollapsingHeader("Debug Info"))
      return;

    string body;
    try {
      var housingManager = HousingManager.Instance();
      var mapData = Plugin.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);
      string[] parts = mapData.PlaceName.Value.Name.ExtractText().Split(" - ");
      string district = parts.Length == 2 ? parts[1] : "";

      body =
        $"Territory Id: {plugin.pluginState.territory}\n" +
        $"In House: {plugin.pluginState.userInHouse}\n\n" +
        $"HouseID: {housingManager->GetCurrentIndoorHouseId()}\n" +
        $"Plot: {housingManager->GetCurrentPlot() + 1}\n" +
        $"Ward: {housingManager->GetCurrentWard() + 1}\n" +
        $"Room: {housingManager->GetCurrentRoom()}\n" +
        $"Division: {housingManager->GetCurrentDivision()}\n" +
        $"District: {district}\n" +
        $"PlaceName: {mapData.PlaceName.Value.Name.ExtractText()}";
    } catch (Exception ex) {
      // Housing-manager reads fail outside houses — log once for support ticket correlation
      Plugin.Log.Debug("Debug info read failed: {0}", ex.Message);
      body = $"(unavailable — not currently in a house)\n\n{ex.Message}";
    }

    ImGui.InputTextMultiline(
      "##debuginfo",
      ref body,
      4096,
      new Vector2(-1, ImGui.GetTextLineHeightWithSpacing() * 10),
      ImGuiInputTextFlags.ReadOnly);
  }

  // -- Welcome banner (first-run, account not linked) --------------------------------

  private void DrawWelcomeBanner()
  {
    ImGui.TextColored(Colors.XivGreen, "Welcome to XIV Venue Manager Sync!");
    ImGui.TextWrapped("To get started, link the plugin to your xivvenuemanager.com account.");
    ImGui.Spacing();
    ImGui.TextWrapped("1. Log in with Discord at xivvenuemanager.com");
    ImGui.TextWrapped("2. Open the Dashboard, go to API Keys and press Generate link code");
    ImGui.TextWrapped("3. Type the code into Link code below and press Link");
    ImGui.Spacing();
    if (ImGui.Button("Open xivvenuemanager.com"))
      Util.OpenLink("https://xivvenuemanager.com");
    ImGui.Spacing();
    ImGui.Separator();
    ImGui.Spacing();
  }

  // -- XIV-App Sync ---------------------------------------------------------

  private void DrawAccountLink()
  {
    DrawSectionHeader("Account link");
    ImGui.TextColored(Colors.XivSubtext0, "Link this plugin to your xivvenuemanager.com account with a one-time code.");
    ImGui.Spacing();

    if (!string.IsNullOrEmpty(this.configuration.xvmApiSecret))
    {
      var who = string.IsNullOrEmpty(this.configuration.xvmApiPersonName) ? "your account" : this.configuration.xvmApiPersonName;
      ImGui.TextColored(StatusOk, $"Linked as {who}");
      if (plugin.xivAppClient?.LinkRejected == true)
        ImGui.TextColored(StatusErr, "Your link was rejected (revoked or expired). Unlink and link again.");
      if (ImGui.Button("Unlink"))
      {
        this.configuration.xvmApiSecret = "";
        this.configuration.xvmApiPersonName = "";
        this.configuration.Save();
        plugin.xivAppClient?.ClearXvm();
        linkStatus = "";
        plugin.myCharacters = new();
        plugin.charactersLoaded = false;
        charactersAttempted = false;
        characterStatus = "";
        return;
      }
      ImGui.Spacing();
      DrawFetchVenuesButton();
      DrawVenueSelector();
      DrawCharacters();
      ImGui.Spacing();
      return;
    }

    ImGui.InputTextWithHint("Link code", "ABCD-1234", ref pairingCode, 16);
    ImGui.SameLine();
    if (linking) ImGui.BeginDisabled();
    if (ImGui.Button("Link") && !linking) _ = LinkAccountAsync();
    if (linking) ImGui.EndDisabled();
    ImGui.TextDisabled("Get a code at xivvenuemanager.com/dashboard/api-keys.");
    if (!string.IsNullOrEmpty(linkStatus)) ImGui.TextColored(linkStatusColor, linkStatus);
    ImGui.Spacing();
  }

  private void DrawSetupChecklist()
  {
    var accountLinked = !string.IsNullOrEmpty(this.configuration.xvmApiSecret);
    var current = plugin.CurrentCharacter();
    var characterLinked = accountLinked && plugin.charactersLoaded && current is { } me && plugin.CharacterIsLinked(me.Name, me.World);
    var syncOn = this.configuration.syncToXivApp;
    if (accountLinked && characterLinked && syncOn) return;

    DrawSectionHeader("Setup");
    ImGui.TextColored(accountLinked ? StatusOk : Colors.XivOverlay0, accountLinked ? "✓ Link your account" : "○ Link your account");
    var characterText = current is { } who ? $"Link your current character ({who.Name} @ {who.World})" : "Link your current character (log in to a character first)";
    ImGui.TextColored(characterLinked ? StatusOk : Colors.XivOverlay0, (characterLinked ? "✓ " : "○ ") + characterText);
    ImGui.TextColored(syncOn ? StatusOk : Colors.XivOverlay0, syncOn ? "✓ Turn on XIV-App Sync" : "○ Turn on XIV-App Sync");
    ImGui.Spacing();
  }

  private void DrawCharacters()
  {
    ImGui.Spacing();
    ImGui.TextColored(Colors.XivSubtext0, "Your characters");
    if (!charactersAttempted && !charactersBusy) _ = RefreshCharactersAsync();

    if (plugin.charactersLoaded && plugin.myCharacters.Count == 0)
      ImGui.TextDisabled("None linked yet. Your visits are not logged until your current character is linked.");

    int? remove = null;
    foreach (var character in plugin.myCharacters)
    {
      ImGui.Text($"{character.CharacterName} @ {character.World}{(character.IsPrimary ? " (primary)" : "")}");
      ImGui.SameLine();
      if (ImGui.SmallButton($"Unlink##char{character.Id}") && !charactersBusy) remove = character.Id;
    }
    if (remove is int id) _ = UnlinkCharacterAsync(id);

    var current = plugin.CurrentCharacter();
    var disabled = current is null || charactersBusy;
    if (disabled) ImGui.BeginDisabled();
    var label = current is { } c ? $"Link current character ({c.Name} @ {c.World})" : "Link current character";
    if (ImGui.Button(label + "##linkchar") && current is { } toLink) _ = LinkCharacterAsync(toLink.Name, toLink.World);
    if (disabled) ImGui.EndDisabled();
    if (current is null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
      ImGui.SetTooltip("Log in to a character first.");

    if (!string.IsNullOrEmpty(characterStatus)) ImGui.TextColored(characterStatusColor, characterStatus);
  }

  private async Task RefreshCharactersAsync()
  {
    charactersAttempted = true;
    charactersBusy = true;
    try
    {
      if (!await plugin.LoadCharactersAsync())
      {
        characterStatus = "Could not load your characters.";
        characterStatusColor = StatusErr;
      }
    }
    finally
    {
      charactersBusy = false;
    }
  }

  private async Task LinkCharacterAsync(string name, string world)
  {
    if (plugin.xivAppClient?.XvmWrites is not { } writes) return;
    charactersBusy = true;
    try
    {
      var result = await writes.LinkCharacterAsync(name, world);
      characterStatus = result.Success ? (result.Created ? $"Linked {name}." : $"{name} is already linked.") : (result.Error ?? "Could not link the character.");
      characterStatusColor = result.Success ? StatusOk : StatusErr;
    }
    finally
    {
      charactersBusy = false;
    }
    await RefreshCharactersAsync();
  }

  private async Task UnlinkCharacterAsync(int characterId)
  {
    if (plugin.xivAppClient?.XvmWrites is not { } writes) return;
    charactersBusy = true;
    try
    {
      var result = await writes.UnlinkCharacterAsync(characterId);
      characterStatus = result.Success ? "Unlinked." : (result.Error ?? "Could not unlink the character.");
      characterStatusColor = result.Success ? StatusOk : StatusErr;
    }
    finally
    {
      charactersBusy = false;
    }
    await RefreshCharactersAsync();
  }

  private async Task LinkAccountAsync()
  {
    linking = true;
    linkStatus = "Linking…";
    linkStatusColor = StatusWarn;
    try
    {
      var url = XvmApiPairing.ResolveUrl(this.configuration.xvmApiUrl);
      var result = await XvmApiPairing.LinkAsync(XvmHttp, url, pairingCode);
      if (result.Success)
      {
        this.configuration.xvmApiSecret = result.Secret!;
        this.configuration.xvmApiPersonName = result.PersonName ?? "";
        this.configuration.Save();
        plugin.xivAppClient?.ConfigureXvm(url, result.Secret!);
        pairingCode = "";
        linkStatus = "";
        _ = FetchXivAppVenuesAsync();
      }
      else
      {
        linkStatus = string.IsNullOrEmpty(result.Detail) ? result.Message : $"{result.Message} ({result.Detail})";
        linkStatusColor = StatusErr;
        if (!string.IsNullOrEmpty(result.Detail)) Plugin.Log.Warning("Account link failed: {0}", result.Detail);
      }
    }
    finally
    {
      linking = false;
    }
  }

  public unsafe void DrawXivAppSettings()
  {
    DrawSectionHeader("XIV-App Sync");
    ImGui.TextColored(Colors.XivSubtext0, "Sync patrons, sales, and shifts with xivvenuemanager.com.");
    ImGui.Spacing();

    var syncEnabled = this.configuration.syncToXivApp;
    if (ImGui.Checkbox("Enable XIV-App Sync", ref syncEnabled))
    {
      this.configuration.syncToXivApp = syncEnabled;
      this.configuration.Save();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Sync patron visits to XIV-App website");
    }

  }

  private void DrawFetchVenuesButton()
  {
    if (ImGui.Button("Fetch Venues"))
    {
      _ = FetchXivAppVenuesAsync();
    }
    if (ImGui.IsItemHovered())
    {
      ImGui.SetTooltip("Refresh the list of venues your linked account can use. The plugin fetches when you link, so you only need this after your venue access changes on the website.");
    }
    // Reserve the row either way so appearing/disappearing text doesn't shove the section
    if (!string.IsNullOrEmpty(xivAppStatus))
    {
      ImGui.TextColored(xivAppStatusColor, xivAppStatus);
    }
    else
    {
      ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight()));
    }
  }

  private void DrawVenueSelector()
  {
    if (plugin.xivAppVenues.Count == 0) return;

    var selectedVenueId = this.configuration.selectedVenueId ?? "";
    var selectedVenue = plugin.xivAppVenues.FirstOrDefault(v => v.Id == selectedVenueId);
    var displayName = selectedVenue?.Name ?? "Select Venue";

    if (ImGui.BeginCombo("Venue", displayName))
    {
      foreach (var venue in plugin.xivAppVenues)
      {
        bool isSelected = venue.Id == selectedVenueId;
        if (ImGui.Selectable(venue.Name, isSelected))
        {
          this.configuration.selectedVenueId = venue.Id;
          this.configuration.Save();
          // Separate from Configuration so other code paths get a single source of truth that updates instantly
          plugin.currentXivAppVenueId = venue.Id;
          plugin.ResetShiftForVenueChange();
          _ = LoadVenueDataWithFeedbackAsync(venue.Id, venue.Name);
        }
        if (isSelected)
          ImGui.SetItemDefaultFocus();
      }
      ImGui.EndCombo();
    }

    // Truncated with hover-to-reveal — a venue with many roles/services wraps ugly otherwise
    DrawTruncatedNameList("Roles", plugin.xivAppRoles.ConvertAll(r => r.Name));
    DrawTruncatedNameList("Services", plugin.availableServices.ConvertAll(s => s.Name));
  }

  private static void DrawTruncatedNameList(string label, List<string> names, int maxShow = 3)
  {
    if (names.Count == 0)
    {
      ImGui.TextDisabled($"{label}: (none fetched yet)");
      return;
    }

    string display;
    if (names.Count <= maxShow)
    {
      display = string.Join(", ", names);
    }
    else
    {
      display = string.Join(", ", names.GetRange(0, maxShow)) + $", +{names.Count - maxShow} more";
    }
    ImGui.TextDisabled($"{label}: {display}");
    if (names.Count > maxShow && ImGui.IsItemHovered())
    {
      ImGui.SetTooltip(string.Join("\n", names));
    }
  }

  // Writes terminal "✓ Loaded: …" status regardless of per-fetch errors — Fetch*Async helpers already log+swallow, partial data is still useful
  private async Task LoadVenueDataWithFeedbackAsync(string venueId, string venueName)
  {
    xivAppStatus = $"Loading roles + services + bans for {venueName}…";
    xivAppStatusColor = Colors.XivOverlay0;

    await FetchXivAppRolesAsync(venueId);
    await FetchXivAppServicesAsync(venueId);
    await FetchXivAppBannedPatronsAsync(venueId);
    await FetchXivAppInventoryEnabledAsync(venueId);

    xivAppStatus = $"✓ Loaded: {plugin.xivAppRoles.Count} roles, {plugin.availableServices.Count} services, {plugin.xivAppBannedPatrons.Count} banned";
    xivAppStatusColor = StatusOk;
  }

  private async Task FetchXivAppVenuesAsync()
  {
    try {
      if (plugin.xivAppClient == null)
      {
        xivAppStatus = "XIV-App client not initialized — restart the plugin.";
        xivAppStatusColor = StatusErr;
        return;
      }
      if (!plugin.xivAppClient.IsConfigured)
      {
        xivAppStatus = "Link your account first: Account link above.";
        xivAppStatusColor = StatusWarn;
        return;
      }

      xivAppStatus = "Fetching…";
      xivAppStatusColor = Colors.XivOverlay0;

      plugin.xivAppVenues = await plugin.xivAppClient.Venue.GetVenuesAsync();
      Plugin.Log.Information("Fetched {Count} venues from XIV-App", plugin.xivAppVenues.Count);
      await plugin.LoadVenueAddressesAsync();

      if (plugin.xivAppVenues.Count == 0)
      {
        xivAppStatus = "No venues found for this key — check that the key is scoped to a venue you own or staff.";
        xivAppStatusColor = StatusWarn;
        return;
      }

      var known = plugin.xivAppVenues.Select(v => v.Id).ToHashSet();
      if (!string.IsNullOrEmpty(this.configuration.selectedVenueId) && !known.Contains(this.configuration.selectedVenueId))
        this.configuration.selectedVenueId = "";
      foreach (var stale in this.configuration.houseToXivAppVenue.Where(kv => !known.Contains(kv.Value)).Select(kv => kv.Key).ToList())
        this.configuration.houseToXivAppVenue.Remove(stale);
      this.configuration.Save();

      xivAppStatus = $"✓ Fetched {plugin.xivAppVenues.Count} venue(s)";
      xivAppStatusColor = StatusOk;

      // Auto-select first venue, or re-hydrate a previously-selected one so "(none fetched yet)" doesn't stick
      string? targetVenueId = null;
      string? targetVenueName = null;
      if (string.IsNullOrEmpty(this.configuration.selectedVenueId))
      {
        targetVenueId = plugin.xivAppVenues[0].Id;
        targetVenueName = plugin.xivAppVenues[0].Name;
        this.configuration.selectedVenueId = targetVenueId;
        this.configuration.Save();
      }
      else
      {
        var match = plugin.xivAppVenues.FirstOrDefault(v => v.Id == this.configuration.selectedVenueId);
        if (match != null)
        {
          targetVenueId = match.Id;
          targetVenueName = match.Name;
        }
      }

      if (targetVenueId != null)
      {
        plugin.currentXivAppVenueId = targetVenueId;
        await LoadVenueDataWithFeedbackAsync(targetVenueId, targetVenueName ?? "venue");
      }
    } catch (Exception ex) {
      xivAppStatus = $"✗ {ex.Message}";
      xivAppStatusColor = StatusErr;
      Plugin.Log.Error("Failed to fetch venues: {0}", ex.Message);
    }
  }

  private async Task FetchXivAppBannedPatronsAsync(string venueId)
  {
    try {
      if (plugin.xivAppClient == null || !plugin.xivAppClient.IsConfigured) return;

      var bannedPatrons = await plugin.xivAppClient.Venue.GetBannedPatronsAsync(venueId);
      if (bannedPatrons != null) plugin.xivAppBannedPatrons = bannedPatrons;
      plugin.xivAppBannedPatronsUnavailable = bannedPatrons == null;
      Plugin.Log.Information("Fetched {Count} banned patron(s) for venue {VenueId}", plugin.xivAppBannedPatrons.Count, venueId);
    } catch (Exception ex) {
      Plugin.Log.Error("Failed to fetch banned patrons: {0}", ex.Message);
    }
  }

  private async Task FetchXivAppInventoryEnabledAsync(string venueId)
  {
    try {
      if (plugin.xivAppClient == null || !plugin.xivAppClient.IsConfigured) return;

      plugin.xivAppInventoryEnabled = await plugin.xivAppClient.Venue.GetInventoryEnabledAsync(venueId);
      Plugin.Log.Information("Fetched inventory-enabled={0} for venue {1}", plugin.xivAppInventoryEnabled, venueId);
    } catch (Exception ex) {
      Plugin.Log.Error("Failed to fetch inventory settings: {0}", ex.Message);
    }
  }

  private async Task FetchXivAppRolesAsync(string venueId)
  {
    try {
      if (plugin.xivAppClient == null || !plugin.xivAppClient.IsConfigured) return;

      var roles = await plugin.xivAppClient.Venue.GetRolesAsync(venueId);
      plugin.xivAppRoles = roles;
      Plugin.Log.Information("Fetched {Count} roles for venue {VenueId}", roles.Count, venueId);
    } catch (Exception ex) {
      Plugin.Log.Error("Failed to fetch roles: {0}", ex.Message);
    }
  }

  private async Task FetchXivAppServicesAsync(string venueId)
  {
    try {
      if (plugin.xivAppClient == null || !plugin.xivAppClient.IsConfigured) return;

      var response = await plugin.xivAppClient.Venue.GetServicesAsync(venueId);
      if (response == null)
      {
        plugin.availableServices = new List<Service>();
        Plugin.Log.Warning("No services response for venue {VenueId}", venueId);
        return;
      }
      plugin.availableServices = response.Services;
      Plugin.Log.Information("Fetched {Count} services for venue {VenueId}", response.Services.Count, venueId);
    } catch (Exception ex) {
      Plugin.Log.Error("Failed to fetch services: {0}", ex.Message);
    }
  }
}
