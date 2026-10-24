# XIV Venue Manager Sync

A Dalamud plugin for FFXIV that syncs patron visits, sales, and shift tracking with [XIVVenueManager](https://xivvenuemanager.com).

## Features

- **Patron Tracking** — Automatically detect and log patron enter/leave events with chat alerts and doorbell sounds
- **Sales Logging** — Log transactions via in-game UI or slash commands, with session tallies on the dashboard strip
- **Shift Management** — Clock in/out from the Shifts tab, chat commands, or a notification when a shift starts or runs past its end, at any venue you work at
- **Venue Management** — Houses are linked to your venues by address automatically (plots, FC houses, apartments), with per-venue guest lists
- **XIV-App Integration** — Link your XIVVenueManager account with a one-time code; venues, shifts, services, rooms, bans and sales all go through it

## Tabs

| Tab | Description |
|-----|-------------|
| **Patrons** | Live patron list with entry count, time tracking, and friend status |
| **Sales** | Log transactions with service selection, customer name, and amount |
| **History** | Patron log per saved venue |
| **My Shift** | View and manage your scheduled/active shifts, and claim open ones |
| **Rooms** | Reserve, release, lock and disable rooms for the house you are standing in |
| **Bans** | The venue's banned patrons, always visible |
| **Inventory** | Link items to services and set stock (shown when the venue has inventory turned on) |
| **Venues** | Saved houses, linked to your venues automatically or by hand |
| **Settings** | Account link, venue selection, alert preferences |

## Commands

| Command | Description |
|---------|-------------|
| `/xvenue` or `/xvm` | Open the main plugin window |
| `/xvm snooze` | Pause alerts until leaving the current house |
| `/xvm sale [amount] [customer]` | Open Sales tab with optional prefill |
| `/xvm sale! <amount> [customer]` | Log a sale instantly without opening UI |
| `/xvm target [amount]` | Open Sales tab with current target as customer |
| `/xvm target! <amount>` | Log a sale for current target without opening UI |
| `/xvm start` | Clock into your next scheduled shift |
| `/xvm end` | Clock out of your active shift |

## Installation

Add the custom plugin repository to Dalamud (https://raw.githubusercontent.com/BluntEXE/XIVVenueManagerSync/master/repo.json), then search for **XIVVenueManagerSync**.

## Configuration

1. Log in to the XIVVenueManager dashboard with Discord, open API Keys, and press **Generate link code** (the code lasts 10 minutes)
2. Open plugin settings (`/xvm` → Settings tab), find **Account link**, type the code and press **Link**
3. Tick **Enable XIV-App Sync** so patron visits are logged
4. Walk into a house that matches one of your venues' addresses. The plugin recognises it, saves it under the venue's name, and switches to that venue. The Venue picker in Settings is a manual override.

## Credits

Plugin by **Ehno** · Built on the Dalamud framework
