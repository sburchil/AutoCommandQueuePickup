# AutoItemPickup

Automatically grants items on the ground to players when the teleport is initiated or an item spawns.

NOTE: Please check out the config options in `BepInEx/config/com.kuberoot.autoitempickup.cfg` before usage.

[Support me on Ko-fi](https://ko-fi.com/kuberoot)

## Features:

- Distributing on item drop and/or teleport
- Filtering by item tier, only distributes white, green and red items by default
- Items can be whitelisted/blacklisted from pickup individually, using a comma-separated config entry. Items can be blacklisted by english name (EN_US locale) or ItemIndex name. For example:
  - `ItemBlacklist = Scrap\, White, Bustling Fungus, Syringe, Infusion, BleedOnHit, Backup Magazine`
- Can grant items to dead players
- Server-only - the mod does not need to be installed on clients
- By default items from printers/cauldrons/similar go to the activator upon distribution, as long as the activator is still a valid target
  - Also includes an option for scrappers
- Features 4 distribution modes:
  - `Sequential` - Grants one item to each player, looping over players when they run out, roughly equally distributed
  - `Random` - Distributes items randomly
  - `Closest` - Grants each item to the nearest player
  - `LeastItems` - Grants each item to the player with least items of its tier
- Support for distributing Command essences
  - If distributing essences on drop, the Command essence will be teleported to the player. 
    - NOTE: The LeastItems distributor does not care about Command essences that have been teleported to a player but haven't yet been picked up, so they might pool up at one player
  - If distributing essences on teleport is enabled, Command essences go near the teleporter after it charges.

## Installation

Copy the `AutoItemPickup` folder to `Risk of Rain 2/BepInEx/plugins`
