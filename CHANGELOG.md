### 2.0.2
- Fix an error that prevents the game from loading when an item without an english name is present (Thanks @lt.armyfollower)

### 2.0.1
- Fix item filters not applying to teleport distribution
- Fix command essences being distributed to dead players

### 2.0.0 - Major refactor
- Major code refactor - Should reduce unexpected behavior
- Config overhaul - Options should migrate automatically
  - OnDrop distribution is now enabled by default to reduce confusion
  - Filtering is now done on a tier whitelist, item whitelist, item blacklist basis
  - Removed separate distribution modes for drop and teleport - Let me know if you were affected by this change
- Add scrapper support - if enabled, scrapped items will go to the player scrapping
- Command essences now obey target overrides - if you find yourself scrapping/printing with command enabled
- Switch to catalogs for item tiers and interactible costs
  - Configuration now allows for all item tiers, should work for modded ones.
  - Printer support should now work for non-standard costs, for example different tier printers.
- Remove R2API dependency - Was only used to declare NetworkCompatibility

### 1.7.0
- Update for SotV patch 1.2.3

### 1.6.1
- Fix error when giving items to dead players
- Fix error with `Closest` distributor when distributing to dead players is enabled and some players are dead
- Fix off-by-one issues with selecting random players, affecting mostly random distribution, that would prevent the last player from getting items
- Add support in code for unloading the mod at runtime
- The `Sequential` distributor will now try to preserve the current target when a player joins or leaves

### 1.6.0 - Update to RoR2 Survivors of the Void
- Teleporting command essences on teleport will now space them away from the center of the teleporter

### 1.4.3 - Update to RoR2 1.0 update
- Remove code to snap command cubes to the ground, since they now have gravity

### 1.4.2 - Hotfix
- Fix error when distributing command cubes if the stage doesn't have a teleporter

### 1.4.1 - Fixes for Command
- Items chosen from command cubes go to the person selecting the item
- Command cubes that are moved when the teleporter finishes charging are now properly synchronised for clients

### 1.4.0 - Update to RoR2 Artifacts update
- Added `TeleportCommandCubes` for simple Artifact of Command compatibility

### 1.3.2 - Update to RoR2 Skills 2.0 update

### 1.3.1 - Update to RoR2 Scorched Acres update

### 1.3.0
- Update to BepInExPack 2.0.0 and R2API 2.0.6; no changes to function.

### 1.2.1
- Fixed error related to changes in language handling in latest RoR2 update (Build #3830295)

### 1.2.0
- Added item blacklisting, separate for drop and teleport.

### 1.1.4
- Fixed missing UpdateTargets condition, which caused `Closest` distributor to throw after first stage. Added additional exception handling in distributor to avoid interfering with game code.

### 1.1.3
- Fixed `Closest` distribution mode throwing exceptions and preventing teleportation when dead players are not ignored and a player is dead

### 1.1.2
- Fixed `Closest` distribution mode presumably being completely broken, without actually testing to make sure the fix isn't broken. Have fun!

### 1.1.1
- Added `OverridePrinterTarget` config option that ensures the activator receives printer result as long as they're valid

### 1.1.0
- Added on-drop distribution setting
- Made on-teleport distribution togglable
- Reworked code structure
- Fixed wrong code that could've lead to errors when ignoring dead players
- Added filtering for white, green, red and boss items
- Added separate filtering options for teleport and drop

### 1.0.2
- Added config option for ignoring lunar items

### 1.0.1 - Added website url

### 1.0.0 - Initial release