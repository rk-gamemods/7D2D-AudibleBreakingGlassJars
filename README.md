# Audible Breaking Glass Jars

A 7 Days to Die mod that adds audio feedback when drinking from glass jars causes the jar to break.

## ⬇️ [Download AudibleBreakingGlassJars-1.0.0.zip](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/raw/master/Release/AudibleBreakingGlassJars-1.0.0.zip)

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

1. Download the zip using the link above
2. Extract to `7 Days To Die/Mods/AudibleBreakingGlassJars/`
3. Ensure EAC is disabled (required for all DLL mods)

Your folder structure should be:
```
7 Days To Die/
└── Mods/
    └── AudibleBreakingGlassJars/
        ├── AudibleBreakingGlassJars.dll
        ├── ModInfo.xml
        ├── Config/
        │   └── config.xml
        └── Sounds/
            └── glass-shatter.ogg
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
| *Your own* | Drop any `.ogg` or `.wav` in `Sounds/` folder, set the name in config |

The custom sound is the default because the game's glass sound is designed for breaking glass blocks, not jars. Adding your own sound is easy - just drop a file in the Sounds folder.

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
dotnet build -c Release
```

Outputs:
- `Release/AudibleBreakingGlassJars/` - The mod folder
- `Release/AudibleBreakingGlassJars.zip` - Distribution package

## Project Structure

```
AudibleBreakingGlassJars/
├── Harmony_ItemActionEat.cs     # Main mod code
├── AudibleBreakingGlassJars.csproj
├── TECHNICAL.md                 # Implementation details
├── README.md                    # This file
├── LICENSE                      # MIT License
├── NEXUS_DESCRIPTION.txt        # Nexus Mods description
└── Release/
    └── AudibleBreakingGlassJars/
        ├── AudibleBreakingGlassJars.dll
        ├── ModInfo.xml
        ├── Config/
        │   └── config.xml
        └── Sounds/
            └── glass-shatter.ogg
```

## Changelog

### v1.0.0 - Initial Release
- Jar break detection via item count comparison
- Custom sound loading at runtime (OGG/WAV)
- Fallback to game sounds if custom sound fails
- Configurable sound name and debug mode
- Fixed: Correct item type detection (drinkJarEmpty vs resourceGlassJar)
- Fixed: Frame delay for inventory update timing

## License

MIT License - See [LICENSE](LICENSE) for details.

## Credits

- Glass breaking sound: Royalty-free audio
- Inspired by the need for better audio feedback in 7D2D

## Links

- [Nexus Mods Page](https://www.nexusmods.com/7daystodie/mods/XXXX) (coming soon)
- [GitHub Repository](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars)
- [Report Issues](https://github.com/rk-gamemods/7D2D-AudibleBreakingGlassJars/issues)
