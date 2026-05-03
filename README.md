# Hopper Collector
A [Stardew Valley](https://www.stardewvalley.net/) mod that extends the vanilla hopper to automatically collect finished machine output and deposit it into an adjacent chest.

## Features
- Hoppers automatically pull finished output from directly adjacent machines.
- Collected items are deposited into any chest within one tile of the hopper.
- If no chest has space, items stay in the machine until room opens up.
- Hoppers re-feed idle machines from their own inventory automatically.
- Works across all locations including barns, sheds, cellars, and coops.
- Multiplayer safe — only runs for the host, farmhands don't need to install it.
- Optional in-game toggle via [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098).

## Installation
1. Install the latest version of [SMAPI](https://smapi.io).
2. Download this mod from [NexusMods](placeholder) and unzip it into `Stardew Valley/Mods`.
3. Run the game using SMAPI.

Optionally, install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) to get an in-game enable/disable toggle.

## Compatibility
- Stardew Valley 1.6 or later.
- Linux, macOS, and Windows.
- Single-player and multiplayer (host only).
- No known mod conflicts.

## Building from source
1. Clone the repository.
2. Open `HopperCollector.sln` in Visual Studio or Rider.
3. Build the solution — [ModBuildConfig](https://smapi.io/package/readme) will resolve all game and SMAPI references automatically.
