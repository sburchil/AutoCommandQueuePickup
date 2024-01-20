# AutoCommandQueuePickup

Automatically grants items on the ground to players when the teleport is initiated or an item spawns.
Also will grant CommandQueue items to player on drop if the CommandQueue for the specific dropped tier has been set.


## Features:

- Distributing on item drop and/or teleport
- Filtering by item tier, only distributes white, green and red items by default
- If distributing essences on drop, the Command essence will be teleported to the player. 
  - NOTE: This functionality will only occur if the CommandQueue for the dropped tier has NOT been set.
- If distributing essences on teleport is enabled, Command essences go near the teleporter after it charges.

## Installation

Copy the `AutoCommandQueuePickup` folder to `Risk of Rain 2/BepInEx/plugins`

## Mod Authors
Please support the original author - **KubeRoot** - of which I simply refactored and combined both:
- CommandQueue
- AutoItemPickup