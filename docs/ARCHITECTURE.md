# Architecture Notes

WHY-rationale extracted from inline comments in `Plugin.cs` and `UI/Tabs/SettingsTab.cs` (2026-08-14, Phase 5 of the codebase-cleanup roadmap). Inline comments now carry a short pointer where the reasoning here matters; the full context lives here so it doesn't get lost or duplicated across call sites.

## Housing / location detection

Detecting "the player is at a venue" combines two separate signals — a discrete Dalamud event (`OnTerritoryChanged`) and a per-tick poll — because walking through a door and walking onto a plot's exterior boundary don't both fire the same events.

**"In house" means "at the venue," not "inside the building."** `OnTerritoryChanged` treats `housingManager->IsInside() || housingManager->IsOutside()` as the same state — both interior instance and standing on the plot exterior feed the same tracking/logging path, so a patron never gets double-counted for walking through the door versus already being outside on the plot.

**Exterior entry has no discrete event to hook.** `OnTerritoryChanged` fires on zone *load*. Walking from a ward's general area onto a specific plot's exterior boundary isn't a zone load, so a low-frequency poll (`entryPollStopwatch`, runs always, independent of the in-house `stopwatch`) is the only way to catch that transition. `HousingManager` calls are safe to make unconditionally everywhere in the game world, not just near housing — this mirrors the same assumption Aetherphone's own tracker relies on for its equivalent detector.

**Leaving the plot exterior without a territory change is debounced.** `OnTerritoryChanged` never fires when a player walks off the plot exterior but stays in the same ward zone — the only place that notices is the per-tick check in `OnFrameworkUpdate`. That check is debounced via `notAtPlotSinceMs`: it tracks how long `HouseIdentity.Current()` has continuously read null while the player is marked in-house, and only calls `leftHouse()` once that gate (2000ms in the exterior-tick path, 1000ms in `OnTerritoryChanged`'s older comment for the same style of gate) has been crossed. A single transient misread right after entering would otherwise trip `leftHouse()` almost immediately, with nothing left to recover it — only an actual zone change restarts tracking, and none of the early-return paths in the debounce restart the underlying stopwatch, so once the gate has been crossed once, those paths get hit every frame (not once a second) until a full pass succeeds.

**Composite house IDs need the player's world resolved first.** The computed ID (works both inside and outside, unlike the native `GetCurrentIndoorHouseId()` which needs no world argument) requires `Objects[0]` to resolve to a valid `IPlayerCharacter` for the current world. If it doesn't, the code bails and retries next tick rather than falling back to a stale or zero world id — `Objects[0]` resolution is already known to be flaky the instant after a territory change, and a wrong world id here would silently corrupt the computed house identity instead of failing loudly like the surrounding `catch` already does for other errors.

**Legacy house ID migration — two paths, deliberately not nested.** Houses saved before exterior tracking shipped are keyed under the legacy interior-only `GetCurrentIndoorHouseId()` value. `MigrateLegacyHouseId()` re-keys a house to the new composite id, moving `venueList`, the xiv-app venue link, and the on-disk guest-list file together in one step so patron history isn't orphaned under a dead key — it runs at most once per house, the first time it's visited after this shipped.

Two migration paths exist because the legacy id genuinely isn't available while outside (see `HouseIdentity.cs`):
1. If `housingManager->IsInside()`, migrate by the legacy interior id directly — this path only fires once the player has stepped inside at least once.
2. Otherwise, `MigrateVenueByLocation()` matches the saved venue by physical location (world/ward/plot/room/type) instead of the unavailable legacy id — works inside or outside, so a legacy-keyed venue heals on the very first exterior visit instead of requiring an interior visit first.

The migration check is deliberately **not** nested inside the "houseId just changed" branch below it. If the player walked the exterior first, `pluginState.currentHouse.houseId` already equals the computed house id (same physical plot, same formula) by the time they step inside — so no "change" is ever detected, and a transition-gated migration would never run. After a successful migration, `pluginState.currentHouse.houseId` is force-cleared to `0` so the "if changed" branch below correctly treats the now-known id as a fresh transition — nothing else would notice the migration happened otherwise.

**Guest-list load uses upsert, not add.** Re-entering a house already visited this session (leave, then come back — trivial now via the exterior) would otherwise throw on a duplicate key, which the surrounding `catch` swallowed silently, cutting off the name-resolution code that ran right after it.

**Display name resolution uses the same precedence in two places.** Both `BuildVenueLabel()` (DTR text) and the main-window-header resolution in `OnFrameworkUpdate` prefer the xiv-app linked name first (nicer branding, e.g. "Rose Garden") and fall back to the locally-saved venue name. The header specifically was never set until this logic ran, so it always showed "(no venue)" regardless of whether the house was actually registered — this fixed that.

**Guest-list reconciliation on load is silent.** On the first pass after loading a guest list from disk, guests restored as `inHouse=true` who aren't actually here anymore left during a prior session. The code reconciles local state silently (`skipLeaveSync`) instead of syncing a "leave" with no matching "enter" in this session, which would otherwise produce a phantom departure event with no arrival to pair it with.

## Status bar (DTR)

**The DTR entry is always created; visibility is config-driven.** Get/Remove churn isn't used to hide the entry — `dtrEntry.Shown` is toggled by the display-mode config instead. Clicking it opens the main window, matching what users expect from a plugin tray icon.

**Text refresh is throttled to ~2s, but callable with `force=true`.** `UpdateDtrBar()` runs every framework tick, but the body only re-allocates the `SeString` every ~2s — the DTR is at-a-glance, not something a player watches update in real time, so a fresh string 60×/s for a strip they only glance at would be wasted work. `force=true` bypasses the throttle to push an immediate update on state transitions (mode change, entering/leaving a house) so the UI feels responsive instead of lagged — used by `leftHouse()` so "Outside" replaces the venue name without waiting for the throttle window.

**The shift poller is kicked on the same tick as the DTR refresh.** Cheap to call unconditionally since `PollActiveShiftAsync()` internally no-ops unless 30s have passed and no previous call is in flight.

**`BuildVenueLabel()` precedence:** xiv-app linked name first (nice branding), then the locally-saved venue name (functional but plain, e.g. a raw ward/plot tag), then "Outside" if the player isn't in a house at all.

**`BuildShiftLabel()` renders three shapes:**
- `ACTIVE` → `"On shift 1h23m"` (elapsed time since `actualStart`)
- `SCHEDULED` → `"Shift in 45m"` (time until `scheduledStart`), but only when within the next 2 hours — further out isn't worth surfacing on an at-a-glance strip
- Neither → `"Off shift"` in prefix mode, or `""` in `compact` mode

`compact` mode exists because the `Combined` display mode joins several of these sub-labels into one string and needs to drop empty/off-shift entries entirely rather than padding them in with visible "Off shift" noise.

**`InvalidateShiftPollCache()` exists to make clock actions feel instant.** Called after any successful clock-in/out so the DTR label reflects the new state within one frame, rather than waiting up to 30s for the normal poll interval to naturally expire.

**The label can show a shift at another venue, and the tooltip says which.** It reads the cross-venue shift list, so a shift you are clocked into elsewhere shows instead of "Off shift"; hovering the bar adds "Shift at <venue>". Venue names stay out of the label text because the strip is short. `activeShift` (the current venue's shift, used by the greeter) is kept separately from the shift the label shows, so greeting patrons never fires at venue B because you are clocked in at venue A.

## Shift tracking

**One poll of `GET /me/shifts` feeds the status bar label, the greeter's current-venue shift, and the clock prompts.** It runs every 30 seconds when the status bar shows shifts, shift prompts are on, "Switch venue to match my shift" is on, or a greeter is on, and not otherwise. The greeter is in that list because its "a shift is active" check reads `activeShift`, which used to be refreshed only when the status bar was in a shift mode. `null` means either no shift or "not polled yet / the call failed", and those two are not distinguished. `ShiftsTab` polls the current venue on its own schedule for its own list.

**`clockSem` is a single mutex for all clock-in/clock-out operations**, regardless of which path triggers them (chat command, UI button, or a prompt). `WaitAsync(0)` is a non-blocking try-acquire: if the semaphore is already held, the caller is told "a clock action is already in progress" immediately rather than blocking the calling thread.

**`ShiftLabelPicker` chooses the label's shift.** A clocked-in shift at any venue wins, and if there are several the current venue's wins. Otherwise the soonest approved shift that has not started yet, anywhere. A shift that has started but is not clocked in is not shown, and neither is one waiting for approval. Overlapping clocks at two venues are allowed: the server's one-active-clock rule is per membership (venue), not per person.

**Clock prompts are prompts, never automation.** `ShiftPromptPlanner` decides, from the same list, which shifts are due: an approved shift that has started and is not clocked in gets one start prompt (including on login mid-shift; it returns on the next poll if clicking Clock in failed because the server could not be reached or answered with a 5xx, but not on a refusal such as a 409), and a clocked-in shift past its scheduled end gets an end prompt, repeated every 15 minutes and given up 12 hours after the end because the server sweeps forgotten shifts. Each prompt is a Dalamud notification with Clock in or Clock out and Not now buttons plus one chat line. Not now mutes that shift until the plugin restarts; closing the notification does not. The buttons use the prompting shift's own venue id. The muted set and the record of which prompts were shown are touched from the notification's UI thread and the poll's background thread, so both take the same lock. This replaced a chat reminder that only knew the current venue.

**A running shift chooses the active venue.** With "Switch venue to match my shift" on (the default), `ShiftVenuePicker` reads the same list: if you are clocked in, the venues of those shifts count; otherwise the venues of approved shifts that have started and are not clocked in. Exactly one venue switches the plugin to it, saves it as the selected venue and prints one chat line. With no running shift nothing is forced, so a hand-picked venue stays. Running shifts at more than one venue are a conflict: the plugin leaves the venue alone, shows a warning notification and one chat line when the conflict starts (`ShiftVenuePicker.EnteredConflict`, so a conflict that carries on does not repeat it, and one that clears and returns announces again), and Settings keeps showing a warning while it lasts. Settings greys out the venue dropdown while a shift is choosing, so a hand-pick cannot be reverted by the next poll. The shift outranks the house: standing in another venue's house during a shift does not change the venue, and the address match skips its switch while a shift is deciding (it still learns the house link). The API's `conflicts` flag is not used, because it also marks overlaps with shifts that are not running and overlaps at one venue, and neither is ambiguous.

**The banned list polls every 30 seconds.** It used to load only at startup and from Fetch Venues, so a ban set on the dashboard never reached the plugin without a manual resync. `PollBannedPatronsAsync` closes that with an in-flight guard, a cooldown, and silent error swallowing. The Bans tab reads the same list and is always visible; a banned patron arriving prints a warning with the reason whatever the chat alert, snooze and Patrons tab settings are (Settings, Ban Alerts).

## Patron sync & chat alerts

**Session sales counters (`SessionSalesTotal`/`SessionSalesCount`) are intentionally not persisted.** They reset on plugin reload and drive the dashboard strip's session tally. "Session" here means plugin lifetime, not calendar day — persisting them via `Configuration` would conflate the two. Incremented from `SalesTab.LogSaleAsync`'s success branch and from the silent slash-command paths (`/xvm sale!`, `/xvm tip!`).

**The event-presence cache (`eventPresence`) has a 60s TTL per venue** and gates patron-visit sync when the user has opted into "sync only during events" — see `EventPresenceCache` itself for the caching mechanics.

**`TryLogPatronVisit` deliberately does NOT filter out the plugin user's own character.** Staff who are off-duty (no active shift) count as patrons visiting their own venue, and the server classifies attribution via `wasWorking` at insert time — the plugin doesn't need to make that call client-side.

Gating order (cheapest checks first, to avoid unnecessary async work):
1. Sync enabled + account linked.
2. Current house → venue ID mapping exists. It is normally filled in by address (see Account link and venue matching); if it is not, the visit is skipped silently and the VenuesTab dropdown is the manual remedy.
3. If `syncOnlyDuringEvents`, the cached event-presence flag must be true — on a cache miss, fetch async and bail for *this* arrival; the next arrival within the TTL will go through once the cache is warm.
4. Post.

All failures log at Debug level and swallow — a sync hiccup should never surface in chat during live service.

**The Patrons Tab setting only hides the tabs and silences patron output.** Tracking itself (the guest list, visit logging to xvm-api, the patron count and the banned arrival alert) always runs, because staff who tidy their window by hiding the tab must not silently stop syncing visits or lose ban warnings. With the tab hidden, the regular arrival and leave chat alerts, the doorbell sound, the self-entry line and the greeter stay off, which is what the greyed-out Settings imply. A banned patron already inside when you arrive also raises the ban alert, whatever the already-here setting says.

**Chat/sound alerts are only meaningful at a registered venue.** Without a known venue (i.e. `venueList.venues` doesn't contain the current house), every house the player walks into — theirs or not — would spam the same "has entered/left" line with no venue name to attach it to, so unregistered houses are silently skipped for both entry and leave alerts.

**The auto-greeter fires independently of chat alert settings and snooze.** A venue owner may want automated `/tell` greetings without the visual chat noise of entry/leave alerts, so the greeter check happens before (and regardless of) the snooze/chat-alert-enabled checks further down. It skips already-here players when the greeter re-enters the venue (only fires on `entryCount == 1` for the first-visit message, `> 1` for the re-entry message) and only fires at registered venues while a shift is active, unless **Free Enable** is on, in which case it fires in any house, on shift or not. It runs before the registered-venue check rather than after it, because that check also guards the self-entry line and the banned alert, which look the venue up by house id and would throw in an unregistered house.

## Slash commands

**`/xvm sale`/`tip`/`target` family** — split on whitespace, first token is the verb, second token is the amount (integer), the rest is a free-text customer name (may contain spaces):

```
/xvm sale                    → open Sales tab, no prefill
/xvm sale 500                → open Sales tab, amount=500
/xvm sale 500 Ehno Smith     → open Sales tab, amount=500, customer="Ehno Smith"
/xvm sale! 500 Ehno          → log immediately, no UI shown, chat toast on result
/xvm tip 500 Ehno            → open Sales tab, Tip selected, amount=500, customer="Ehno"
/xvm tip! 500 Ehno           → log a tip immediately, no UI shown, chat toast on result
/xvm target                  → open Sales tab with current target prefilled
/xvm target 500              → open Sales tab with current target + amount
```

For `/xvm target` (not `target!`), the "customer" override is the game target, not an args field — if the player has no target, the code falls through with `null` and lets the Sales tab's own "Use Target" flow handle it next frame instead of failing here.

**Per-subcommand `AddHandler` calls exist purely so `/xlhelp` lists each subcommand.** Dalamud dispatches everything on the parent command (`OnCommand` receives e.g. `args="sale 500 Ehno"`) — the actual routing logic lives entirely in `OnCommand`'s args parser. The `AddHandler` calls for `sale`, `sale!`, `tip`, `tip!`, `target`, `target!`, `ban!`, `start`, `end` are sugar for discoverability only.

**The four silent (`!`-suffixed) command handlers** (`LogSaleSilentAsync`, `LogTipSilentAsync`, `BanPatronSilentAsync`, `ShiftClockInSilentAsync`/`ShiftClockOutSilentAsync`) all bypass their respective tab's form state entirely, write straight through to the XIV-App API, and post a chat toast on the result:
- `LogSaleSilentAsync` — success increments the dashboard session tally so the strip readout stays consistent regardless of which code path logged the sale.
- `LogTipSilentAsync` — same shape as the sale path, just tagged `type="TIP"` so the server (and the website's revenue/tips-pool breakdown) counts it separately.
- `BanPatronSilentAsync` — works even for a character with no prior visit history; the server finds-or-creates the `Patron` row.
- The shift clock silent commands share `clockSem` with the UI buttons (see Shift Tracking above) and reset the shift-poll cache on success so the DTR updates within one frame.

## Account link and venue matching

**The plugin talks to xvm-api directly with its own account-wide credential.** A person generates a one-time code on the dashboard (API Keys, Generate link code), types it into Settings, Account link, and the plugin exchanges it for a secret that is stored in the config. Reads are in `XvmApiReads`, writes in `XvmApiWrites`, and the `XIVApp*Api` classes keep their method signatures and delegate to them. There is no fallback to the old `/api/plugin/*` routes. The credential has to be account-wide because `/me/shifts` and `/me/venues` refuse a credential narrowed to one venue.

**Characters are linked from Settings, under Account link.** The list comes from `GET /me/characters`, and Link current character posts the character the player is logged in as, with the home world (the same name and world the visit log reports). It is offered only for the current character, with no free-text box. xvm-api does not verify the claim today: Discord login plus the link code identifies the person, and a character is simply linked to them. The link matters because the server marks a visit as working only when the visiting character is linked to a person who holds a membership at the venue and is clocked in there; an unlinked staff character is recorded as a patron.

**A 401 marks the link as rejected, and any later success clears it.** One stray 401 therefore cannot leave the plugin stuck. While it is set the plugin prints one chat line, the header dot goes red, Settings shows red text under Account link, and the tabs show a banner. The polls keep running, so a restored link recovers by itself.

**`XvmApiWrites` still waits 300 ms after a successful write, a leftover from when xvm-api committed after it had sent its response.** That cause is gone: xvm-api #164 ("Commit before responding") merged on 2026-10-12, so a read right after a write no longer sees the old state. The wait does nothing useful now, and #12 removes it.

**A house is matched to a venue by address.** `VenueAddressMatcher` compares world, district, ward and then either the plot or the apartment room plus main or subdivision. House size is never compared. A link is only filled in when exactly one venue matches, an existing link is never replaced, and a clash links nothing. The game values were checked in game: a main-division apartment reads plot -127, a subdivision apartment -126, and a chamber keeps the plot with a room number.

**Your own arrival and departure are reported even though nobody else's departure can be.** Leaving a house marks your own character as left in the guest list and sends a leave for it, so your next arrival registers as a new visit. Nothing is sent for the other guests, because the plugin cannot know whether they left too. Players already inside when you arrive are reported as `present`, not `enter`, which is the server's wording for "already there when the plugin started watching". Ghost presence (a guest the server still thinks is inside after the last reporter left) is closed at the next reporter's arrival, and otherwise by the server's 12 hour presence window.

**Each FC chamber has its own house id, so chambers are never linked or saved as venues.** Entering one only switches the current venue to the FC house's venue (matched by address) and prints the room's status, built from a fresh fetch of the venue's rooms. Apartments have no rooms to show, so the Rooms tab auto-open and the room message only run for plot houses.

**Venue addresses are loaded with the venue list, and refetched when an entered house matches nothing**, at most once a minute, so an address edited on the website while the plugin runs links without a restart.

**The Lifestream teleport is disabled for apartment venues.** Lifestream cannot be handed an apartment address, and the plugin used to send the plot value -127, which Lifestream read as plot 12.

## Startup / configuration

**The XIV-App API client is always instantiated, even when the account is not linked**, so the Settings tab can configure it the moment a code is accepted, with no restart. `IsConfigured` means "an account is linked".

**Startup auto-load (`AutoLoadXivAppDataAsync`) is fire-and-forget from the constructor**, so a server outage at launch can't block plugin init. It loads the venue list and their addresses, picks the previously-selected venue if it still exists (otherwise the first), and loads that venue's roles, services, bans and inventory flag through `LoadVenueDataAsync`, which "follow the house" also uses. The manual Fetch Venues button remains as a retry path.

**The cached `PluginVersion` string is read from the loaded assembly**, not hardcoded — `Plugin.cs`, `XIVVenueManagerSync.json`, and `repo.json` are kept in lockstep by the build+ship ritual, so reading from the running assembly means the dashboard strip and the changelog-on-update check auto-follow whatever version the user actually has installed.

## Settings UI (`SettingsTab.cs`)

**Account link is drawn first**, with Fetch Venues, the venue picker and the roles and services lists directly under it once linked, because linking is the one thing a fresh install has to do. XIV-App Sync below it is only the Enable checkbox.

**The status line under "Fetch Venues"** (`xivAppStatus`/`xivAppStatusColor`) exists so users see success/failure of button-press actions instead of a silent no-op. Default color is the muted overlay so a fresh "Fetching…" line reads as in-progress rather than success or failure. The row is reserved (via a `Dummy` placeholder) whether or not a status is active, so appearing/disappearing text doesn't shove the rest of the section up and down.

**Venue auto-select/re-hydrate on fetch** avoids a "(none fetched yet)" status sticking around: if no venue is selected yet, the first fetched venue becomes selected; if one was already selected, its roles/services get re-fetched so an already-configured plugin's first load after an update doesn't show stale placeholder text.

**`LoadVenueDataWithFeedbackAsync` writes a terminal "✓ Loaded: …" status regardless of per-fetch errors.** The individual `FetchXivApp*Async` helpers already log-and-swallow their own failures, so partial data (e.g. roles fetched but VIPs failed) is still useful to surface rather than blocking the whole status line on one failed sub-fetch.

**`FetchXivAppRolesAsync` storing the result was itself a bug fix** — the fetch previously logged the count and discarded the actual list, which was the root cause of a "roles not updating" report. The Settings indicator (and any future role dropdown) now reads from `plugin.xivAppRoles`, which this call populates.

**The venue selector tracks `plugin.currentXivAppVenueId` separately from `Configuration.selectedVenueId`.** Other code paths (service logging, role dropdowns) need a single source of truth that updates the instant the user switches venues in the dropdown, before a `Configuration.Save()` round-trip would otherwise be visible elsewhere.

**Roles/Services status lines truncate to 3 names with hover-to-reveal.** A venue with many roles or services would otherwise wrap ugly across the settings tab; users specifically need to see Services for the Sales tab workflow (what they're actually able to log sales against), so the label is present even when truncated.

**Sound alerts and chat alerts are both gated on `showGuestsTab`** for the same reason: the Patrons tab owns the patron-event source that fires both the doorbell and chat lines. When the tab is hidden, both sections are grayed (via `BeginDisabled`), not removed entirely — so users can still see the section exists and understand *why* it's inactive, rather than wondering where it went.

**Debug Info is collapsed by default and read-only.** It's support-surface content — only interesting when debugging a ticket — so it doesn't clutter the tab for everyday use. `InputTextMultiline` with the `ReadOnly` flag gives users a copy-paste target for bug reports instead of having to retype fields by hand. Housing-manager reads inside this block fail outside houses; the failure is swallowed but logged once at Debug level so a blank Debug block in a support ticket can still be correlated to a real cause via the plugin log.

**Section-break rhythm was previously inconsistent** (some breaks used `Separator()` alone, others `Separator()+Spacing()`) — `DrawSectionSeparator()` now standardizes every break to the same shape. Section headers use a blue accent color specifically so users can scan section boundaries at a glance on what is otherwise a long, single-scroll tab.
