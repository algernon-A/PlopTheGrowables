## Plop the Growables
A mod for Cities: Skylines 2.  Available for download only on [Paradox Mods](https://mods.paradoxplaza.com/mods/75826/Windows).

The primary platform for support is the [Cities Skylines Modding Discord](https://discord.gg/HTav7ARPs2).

### Features
- **Disables growable building zone checks** - you can place growable buildings anywhere, and they won't despawn (be condemned) if the zoning underneath them is changed or removed.
- Optionally **still require correct zoning for non-plopped buildings** - so any naturally spawned growables will be condemned if the zoning underneath them is changed or removed.
- Optionally **disable building level changes for user-selected individual buildings**
- Optionally **disable building level changes for manually plopped growable buildings**
- Optionally **disable building level changes for ALL buildings**
- Toggle level-locking (on/off) for all existing buildings

### Disables growable building zone checksW
This means that you can place them anywhere (using the dev UI) - on zoning, off zoning, partially on zoning, on incorrect zoning, wherever - and they won't despawn.

This allows the manual placement of growable buildings directly, without needing any workarounds such as making them signature buildings.

Note that buildings will still require road access (including for water/sewerage/electricity) to function properly, but they don't have to be perfectly aligned with the road.

### Require zoning for naturally spawned (non-plopped) growables
This is an option that can be enabled in the mod's options panel.  When this is enabled then any growable buildings that weren't plopped will still require the correct zoning; if this option is enabled and the zoning underneath a spawned building is changed or removed then that building will become condemed and be demolished (as it would normally without this mod).

### Disable level changes for individual buildings
This mod adds a 'lock building level' toggle to the building info panel for growable buildings.  Select this toggle to keep that building at the same level.

Note that building level-up progress within the current level will still show on the building's level up progress panel, but it will never reach the level required to upgrade to the next level.

### Lock building levels when plopped (optional)
Automatically applies level locking to any growable building that is manually placed (plopped) while this setting is active.  This includes placement via Find It, Line Tool, or the dev mode prefab menu.  Buildings with their levels locked also won't become abandoned (but people can and will still move out - see the notes below under disable building abandonment).

### Disable ALL building level changes (optional)
This can be toggled in the mod's options panel (the default is **off**).  When this is enabled all growable buildings will keep their current level, ensuring that they also keep their appearance.

Note that building level-up progress within the current level will still show on the building's level up progress panel, but it will never reach the level required to upgrade to the next level.

### Disable building abandonment (optional)
This prevents buildings from being flagged as 'abandoned'.

**Note:** this does **NOT** prevent people from actually moving out, so the building will still become empty (it just won't get "abandoned" status; the building will remain in place, ready for somebody else to move in).   Nor does this remove the underlying issue that caused the occupants to move out in the first place.

Buildings are open for re-occupancy, but depending on what caused the original occupants to leave it may take a long period (sometimes a *very* long period of time) of time before people move back in.

### Toggle level locking for all existing buildings
This is available via the buttons in the options panel to either **lock** or **unlock** levels for all buildings currently on the map.  These are only enabled when you're in-game (as otherwise there's no buildings to toggle).

### Remove abandonment for all existing buildings
This is available via the button in the options panel for all buildings currently on the map.  This is only enabled when you're in-game (as otherwise there's no buildings to toggle).

Note that this just removes the building's 'abandoned' status and does not force people to move back in, and nor does it remove the underlying issue that caused the building to become abandoned in the first place.  Buildings are open for re-occupancy, but depending on what caused the original occupants to leave it may take a long period (sometimes a *very* long period of time) of time before people move back in.

## How to select and place growable buildings
This mod doesn't have its own UI - instead, use TDW's **Find It** mod to select and place growable buildings (or use the game's developer mode).

## Meta
### Known mod conflicts
- [Realistic Workplaces and Households](https://mods.paradoxplaza.com/mods/87755/Windows) is not compatible with this mod, as it replaces the same game system that this mod needs to modify.
- [Urban Inequality](https://mods.paradoxplaza.com/mods/110245/Windows) is not compatible with this mod, as it replaces the same game system that this mod needs to modify.

### Translations
This mod supports localization via a [CrowdIn project](https://crowdin.com/project/plop-the-growables).  Please help out if you can!

### Credits
This mod uses the [Harmony patching library](https://github.com/pardeike/Harmony) by Andreas Pardeike.

### Modders
Modders (and aspiring modders!), as always I'm available and happy to chat about what I've done and answer any questions, and also about how you can implement anything that I've done for your own mods.  Come grab me on the [Cities Skylines Modding Discord](https://discord.gg/HTav7ARPs2)!

Pull requests welcome! Note that translations should be submitted via CrowdIn (see link above), and not by PR.

### Credits
Thanks to TPB, the creator of the original Plop the Growables mod for Cities: Skylines 1, which inspired the idea of this one.