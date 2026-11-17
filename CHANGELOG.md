# Changelog

All notable changes to the XIV Venue Manager plugin are listed here, newest first. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [4.0.1] - 2026-11-17

### Added
- A filter box on the Inventory tab and inside the Sales service dropdown. It matches the service name, the linked item and the category.
- Open event shifts show the event they belong to.

### Changed
- The open shift board only lists shifts for your roles.
- Shifts are grouped by date, and your own shifts sit above the open ones on the Shifts tab.
- Your roles refresh with the shift poll, so a role change made on the dashboard shows up without reloading the plugin.

### Fixed
- The Link button no longer leaves the rest of the window greyed out while a link is in progress.

## [4.0.0] - 2026-11-09

### Added
- The plugin talks to the new XIV Venue Manager system. Link it with a one-time code from the dashboard (API Keys, Generate link code).
- A setup checklist at the top of Settings, and a Link current character button. Your own visits are not logged until your character is linked.
- Shift prompts: a Clock in button 30 minutes before a scheduled shift and when it starts, and Clock out when it runs past its end. Not now silences a shift. The status bar shows your shift at any venue you work at.
- Join event shifts from the open board in the My Shift tab.
- Rooms: reserve, release, lock, unlock and disable from the Rooms tab. Staff cannot lock or disable, managers can.
- A Bans tab, always visible. A banned patron arriving prints a warning with the reason, even when chat alerts are snoozed or the Patrons tab is hidden.
- Houses, apartments (main and subdivision) and FC houses are matched to your venues by address and saved automatically.
- Chat tells you when a sale or tip is logged without a live event, and when a venue conflict is found.

### Changed
- Old API keys no longer work, so everyone links again once. The API key and server URL fields are gone from Settings.
- A visit is logged as staff only when your character is linked and you are clocked in at that venue. Otherwise it is logged as a patron visit.
- With Switch venue to match my shift on (the default), the plugin switches to the venue of a shift you are clocked in at and says so in chat. If you are clocked in at two venues it leaves the venue alone and warns you.
- The Patrons Tab setting now only hides the tab and quiets arrival and leave alerts. Tracking and visit logging keep running.
- Your own leave is reported, and players already inside when you arrive are reported as present.
- Greeter Free Enable ignores your shift and venue.

### Fixed
- A failed event or rooms lookup now shows as a failure instead of looking like an empty list.
- The greeter no longer fails to send welcome messages when the Status Bar display is Disabled (from v3.11.1).

[Unreleased]: https://github.com/xiv-venue-manager/xvm-plugin/compare/v4.0.1...HEAD
[4.0.1]: https://github.com/xiv-venue-manager/xvm-plugin/compare/v4.0.0...v4.0.1
[4.0.0]: https://github.com/xiv-venue-manager/xvm-plugin/compare/v3.11.1...v4.0.0
