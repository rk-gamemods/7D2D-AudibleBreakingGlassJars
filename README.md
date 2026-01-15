# Audible Breaking Glass Jars

A 7 Days to Die mod that adds audio feedback when drinking from glass jars causes the jar to break.

**Target Version:** 7 Days to Die V2.5 (b23)

## ⬇️ Downloads

- **[Main Mod](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/raw/master/Release/AudibleBreakingGlassJars-1.1.0.zip)** - Glass breaking sound
- **[Addon: Broken Glass](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/raw/master/Release/AudibleBreakingGlassJars_Addon_BrokenGlass-0.2.0.zip)** - Get broken glass from broken jars
- **[Addon: Jar Return](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/raw/master/Release/AudibleBreakingGlassJars_Addon_JarReturn-0.1.1.zip)** - Return jars when crafting starts

## The Problem

In 7 Days to Die, when you drink from a glass jar (water, tea, etc.), the game has a "Jar Return Percentage" setting that determines whether you get the empty jar back. But there's **no audio feedback** when a jar breaks - you only notice later when checking your inventory.

## The Solution

This mod plays a glass breaking sound effect when your jar doesn't survive the drink. Now you'll know immediately whether you got your jar back or lost it.

## Features

- Plays a glass shattering sound when jars break during consumption
- Works with the game's Jar Return Percentage setting (0-100%)
- Includes a custom high-quality glass breaking sound (royalty-free)
- Can use built-in game sounds instead if preferred
- Configurable via XML config file
- Debug mode for troubleshooting
- Lightweight - only observes the game, doesn't modify core mechanics

## Installation

1. Download the zip(s) using the links above
2. Extract to `7 Days To Die/Mods/`
3. Ensure EAC is disabled (required for all DLL mods)

Your folder structure should be:
```
7 Days To Die/
└── Mods/
    └── AudibleBreakingGlassJars/           # Main mod
        ├── AudibleBreakingGlassJars.dll
        ├── ModInfo.xml
        ├── Config/
        │   └── config.xml
        └── Sounds/
            └── glass-shatter.ogg
    └── AudibleBreakingGlassJars_Addon_BrokenGlass/   # Optional
        ├── BrokenGlassFromBrokenJars.dll
        └── ModInfo.xml
    └── AudibleBreakingGlassJars_Addon_JarReturn/     # Optional
        ├── JarReturnOnCraft.dll
        ├── ModInfo.xml
        └── Config/
            └── JarContents.xml              # Optional overrides
```

## Configuration

Edit `Config/config.xml` in the mod folder:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<config>
    <SoundName>glass-shatter</SoundName>
    <DebugMode>false</DebugMode>
</config>
```

### Sound Options

| Sound Name | Description |
|------------|-------------|
| `glass-shatter` | (default) Included custom sound - sounds like a jar breaking |
| `glassdestroy` | Game's built-in glass sound - louder, like a window |
| *Your own* | Add a `.ogg` or `.wav` to `Sounds/`, set the filename in config |

The custom sound is the default because the game's glass sound is designed for breaking glass blocks, not jars.

---

## Addon: Broken Glass From Broken Jars

**v0.2.0**

When a jar breaks from drinking, you get 1x **Broken Glass** instead of nothing.

### Features
- Gives 1 broken glass when jars break from drinking
- Tries backpack first, then toolbelt, then drops on ground
- Works with or without the main sound mod

### Installation
Copy `AudibleBreakingGlassJars_Addon_BrokenGlass` folder to `Mods/`

---

## Addon: Jar Return On Craft

**v0.1.1**

When you craft with water, you get the empty jar back right away instead of it staying inside the food.

### Features
- Returns empty jar when craft job starts
- Canceling does NOT refund the liquid (you already got the jar)
- Exception: jar-to-jar recipes (water→tea) use vanilla behavior
- Works with or without the main sound mod

### Installation
Copy `AudibleBreakingGlassJars_Addon_JarReturn` folder to `Mods/`

### Recommended: VanillaJarFix

Use with [VanillaJarFix](https://www.nexusmods.com/7daystodie/mods/9353) which removes incorrect jar properties from solid foods like cornbread and boiled meat.

---

## Testing & Multiplayer

**Addons are partially tested** for basic functionality with a few recipes. Please report any edge cases or recipes that don't work correctly.

**Multiplayer:** Not tested, but expected to work client-side only (no server install needed). All operations stay within player inventory using standard game APIs. No direct manipulation of workstation outputs or shared containers. Please report your results.

**EAC must be disabled** for all DLL mods.

---

## How It Works

The mod uses Harmony patches to detect when you consume a jarred beverage:

1. **Before drinking:** Count empty jars in your inventory
2. **After drinking:** Wait for inventory update, count again
3. **If count didn't increase:** Jar broke → play sound

This approach is non-invasive and highly compatible with other mods.

## Compatibility

This mod is highly compatible because it only **reads** inventory counts:

| Mod Type | Status |
|----------|--------|
| Inventory mods | Compatible |
| Backpack expansions | Compatible |
| UI overhauls | Compatible |
| ProxiCraft | Compatible |
| Other jar mods | Should work |

## Technical Details

- **Target:** `ItemActionEat.consume()` and `ExecuteInstantAction()`
- **Patch Type:** Harmony Prefix/Postfix (non-invasive)
- **Detection:** Item count comparison with frame delay
- **Audio:** Runtime loading via Unity's `UnityWebRequest`

See [TECHNICAL.md](TECHNICAL.md) for implementation details.

## Building

```powershell
cd AudibleBreakingGlassJars

# Build main mod
dotnet build AudibleBreakingGlassJars.csproj -c Release

# Build addons
dotnet build BrokenGlassFromBrokenJars.csproj -c Release
dotnet build JarReturnOnCraft.csproj -c Release
```

Outputs:
- `Release/AudibleBreakingGlassJars/` - Main mod
- `Release/AudibleBreakingGlassJars_Addon_BrokenGlass/` - Broken glass addon
- `Release/AudibleBreakingGlassJars_Addon_JarReturn/` - Jar return addon

## Project Structure

```
AudibleBreakingGlassJars/
├── AudibleBreakingGlassJars.csproj        # Main mod build
├── BrokenGlassFromBrokenJars.csproj       # Addon build
├── JarReturnOnCraft.csproj                # Addon build
├── src/
│   ├── AudibleBreakingGlassJars/
│   │   └── Harmony_ItemActionEat.cs       # Main mod code
│   ├── BrokenGlassFromBrokenJars/
│   │   └── BrokenGlassFromBrokenJars.cs   # Broken glass addon
│   └── JarReturnOnCraft/
│       ├── JarReturnOnCraft.cs            # Jar detection logic
│       ├── Patch_ReturnJarOnCraftStart.cs # Return jar when crafting
│       └── Patch_SkipJarContentOnCancel.cs # Skip refund on cancel
├── TECHNICAL.md                           # Implementation details
├── README.md                              # This file
├── LICENSE                                # MIT License
├── NEXUS_DESCRIPTION.txt                  # Nexus Mods description
└── Release/
    ├── AudibleBreakingGlassJars/
    ├── AudibleBreakingGlassJars_Addon_BrokenGlass/
    └── AudibleBreakingGlassJars_Addon_JarReturn/
```

## Changelog

### v1.1.0 - AudibleBreakingGlassJars
- Fixed: Jar returned to toolbelt incorrectly triggered break sound (reported by DanColeman86)
- Fixed: Jar dropped on ground (inventory full) incorrectly triggered break sound
- Now checks bag, toolbelt, AND nearby ground drops before playing sound

### v1.0.0 - AudibleBreakingGlassJars
- Jar break detection via item count comparison
- Custom sound loading at runtime (OGG/WAV)
- Fallback to game sounds if custom sound fails
- Configurable sound name and debug mode

### v0.2.0 - BrokenGlassFromBrokenJars
- Fixed: Jar returned to toolbelt incorrectly gave broken glass
- Fixed: Jar dropped on ground (inventory full) incorrectly gave broken glass
- Now checks bag, toolbelt, AND nearby ground drops

### v0.1.1 - JarReturnOnCraft
- Added debug mode toggle (reduces log spam)
- Jar return logs now only show when debug mode enabled

### v0.1.0 - Addons Initial Release
- BrokenGlassFromBrokenJars: Gives broken glass when jars break from drinking
- JarReturnOnCraft: Returns empty jar when crafting starts, skips refund on cancel

## License

MIT License - See [LICENSE](LICENSE) for details.

## Credits

- Glass breaking sound: Royalty-free audio
- Inspired by the need for better audio feedback in 7D2D

## Links

- [Nexus Mods Page](https://www.nexusmods.com/7daystodie/mods/XXXX) (coming soon)
- [GitHub Repository](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars)
- [Report Issues](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/issues)
