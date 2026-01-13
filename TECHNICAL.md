# Audible Breaking Glass Jars - Technical Documentation

## Overview

This mod plays an audio cue when drinking a jarred beverage results in the glass jar breaking (not being returned to inventory). The game has a configurable "Jar Return Percentage" setting (0-100%) but provides no audio feedback when jars break vs. are returned.

**Target Version:** 7 Days to Die V1.1 (b14)

## Architecture

### Multi-Project Design
The project now contains three separate builds:

| Project File | Output DLL | Purpose |
|--------------|------------|---------|
| `AudibleBreakingGlassJars.csproj` | `AudibleBreakingGlassJars.dll` | Main mod - glass breaking sound |
| `BrokenGlassFromBrokenJars.csproj` | `BrokenGlassFromBrokenJars.dll` | Addon - gives broken glass |
| `JarReturnOnCraft.csproj` | `JarReturnOnCraft.dll` | Addon - fixes crafting exploit |

Each addon is fully independent and can be used without the main mod.

### Dependencies
- **0Harmony.dll** - Harmony 2.x patching library (provided by TFP_Harmony mod)
- **Assembly-CSharp.dll** - Game's main assembly
- **UnityEngine.CoreModule.dll** - Unity engine core

## Game Mechanics: Jar Return System

### How Jar Returns Work

When consuming a jarred drink (water, tea, etc.), the game decides whether to return an empty jar:

```csharp
// From ItemActionEat.cs (game code)
if (CreateItem != null && CreateItemCount > 0 &&
    (!UseJarRefund || (float)GameStats.GetInt(EnumGameStats.JarRefund) * 0.01f > rand.RandomRange(1f)))
{
    // Jar returned to inventory (or dropped if inventory full)
}
// Else: jar "broke" - nothing happens, no jar returned
```

**Key Properties on ItemActionEat:**
| Property | Type | Purpose |
|----------|------|---------|
| `CreateItem` | string | Item to create after consumption (e.g., "resourceGlassJar") |
| `CreateItemCount` | int | How many to create (usually 1) |
| `UseJarRefund` | bool | Whether this item respects the jar refund percentage |

### Two Consumption Paths

The game has two code paths for consuming items:

1. **`consume()`** (lines ~300-350 in game code)
   - Called when drinking animation completes
   - This is the normal gameplay path
   - Sound plays in sync with animation end

2. **`ExecuteInstantAction()`** (lines ~230-260 in game code)
   - Called for instant/UI-based consumption
   - Rare in normal gameplay
   - We patch both for completeness

## Detection Strategy

### Why We Use Jar Count Comparison

We cannot directly observe the random roll that determines jar fate. Instead:

1. **Prefix**: Before consumption, count player's glass jars
2. **Postfix**: After consumption, count again
3. **Logic**: If count didn't increase, the jar broke

```
Before: 5 jars → Drink water → After: 6 jars = Jar returned (no sound)
Before: 5 jars → Drink water → After: 5 jars = Jar broke (play sound)
```

### ThreadStatic Variables

We use `[ThreadStatic]` to safely pass state from Prefix to Postfix:

```csharp
[ThreadStatic] private static bool _checkingJar;
[ThreadStatic] private static int _jarCountBefore;
[ThreadStatic] private static EntityPlayerLocal _player;
```

This handles potential multi-threaded scenarios, though in practice consumption is single-threaded.

### CRITICAL: Delayed Inventory Check

The jar return logic runs at the end of `consume()`, but **the inventory update is delayed by one frame**. If we check the jar count immediately in the Postfix, we'll see the old count and incorrectly think the jar broke.

**Solution:** Use a coroutine to wait one frame before checking:

```csharp
// In Postfix - DON'T check immediately!
GameManager.Instance.StartCoroutine(CheckJarAfterDelay(player, jarCountBefore));

private static IEnumerator CheckJarAfterDelay(EntityPlayerLocal player, int jarCountBefore)
{
    yield return null;  // Wait one frame

    int jarCountAfter = CountGlassJars(player);
    if (jarCountAfter <= jarCountBefore)
    {
        PlayGlassBreakSound(player);
    }
}
```

**Important:** Because we use a coroutine, ThreadStatic variables won't work across frames. We must capture the values as local variables before starting the coroutine.

### Edge Cases

| Scenario | Behavior | Notes |
|----------|----------|-------|
| Jar refund = 0% | Sound always plays | Every jar breaks |
| Jar refund = 100% | Sound never plays | Every jar returns |
| Inventory full | No sound | Jar is dropped, not broken |
| Race condition | May miss sound | Another source adds jar simultaneously (extremely rare) |

## Audio System Nuances

### CRITICAL: PlayInsidePlayerHead Overloads

The `Audio.Manager` class has multiple `PlayInsidePlayerHead` overloads with **very different behavior**:

#### 2-Parameter Version (DO NOT USE)
```csharp
Manager.PlayInsidePlayerHead(string soundGroupNameBegin, int entityID)
```
**This version expects TWO sounds to exist:**
1. `soundName` - The base sound
2. `soundName_lp` - A loop variant

If either is missing, **the call silently fails** with no error or exception.

#### 5-Parameter Version (USE THIS)
```csharp
Manager.PlayInsidePlayerHead(string soundGroupName, int entityID, float delay, bool isLooping, bool isUnique)
```
**This version works with a single sound definition.** No `_lp` variant required.

### Our Implementation
```csharp
// CORRECT - Uses 5-parameter overload
Manager.PlayInsidePlayerHead(SoundName, player.entityId, 0f, false, false);

// WRONG - Silently fails if soundName_lp doesn't exist
// Manager.PlayInsidePlayerHead(SoundName, player.entityId);
```

### Sound Requirements

For a sound to work with our mod, it must:
1. Exist in the game's `sounds.xml` as a `<SoundDataNode>`
2. Have valid `<AudioClip>` references to actual audio files
3. NOT require positional audio (we play "inside player head")

**Verified working sounds:**
- `glassdestroy` - Glass block destruction (default)
- `itembreak` - Generic item breaking
- `brokenglass_place` - UI glass sound (subtle)

**Sounds that may not work well:**
- Sounds using `AudioSource_Impact.prefab` designed for 3D world positions
- Sounds requiring the `_lp` loop variant

## Configuration

### Config File Location
`Config/config.xml` inside the mod folder.

### Config Schema
```xml
<?xml version="1.0" encoding="UTF-8"?>
<config>
    <SoundName>abgj_glass_shatter</SoundName>
    <DebugMode>false</DebugMode>
</config>
```

### Sound Options
| Sound Name | Description |
|------------|-------------|
| `abgj_glass_shatter` | Custom sound included with mod (default) |
| `glassdestroy` | Game's glass block destruction sound (loud, dramatic) |
| `itembreak` | Game's generic item breaking sound |

### Custom Sound Registration
The mod's `Config/sounds.xml` registers the custom sound with the game:
```xml
<configs>
    <append xpath="/Sounds">
        <SoundDataNode name="abgj_glass_shatter">
            <AudioSource name="@:Sounds/Prefabs/AudioSource_Interact.prefab"/>
            <AudioClip ClipName="#@modfolder:Sounds/glass-shatter.ogg"/>
            ...
        </SoundDataNode>
    </append>
</configs>
```

Key elements:
- `#@modfolder:` prefix resolves to this mod's folder
- Uses `AudioSource_Interact.prefab` for UI-type sounds (works with `PlayInsidePlayerHead`)
- Sound file in `Sounds/glass-shatter.ogg`

### Security Validation
Sound names are validated to prevent path traversal:
- Rejected: Contains `/`, `\`, or `..`
- Only simple sound names are accepted

## Debug Mode

When `<DebugMode>true</DebugMode>` is set, the mod logs detailed information:

```
[ABGJ-Debug] ========== CONSUME EVENT ==========
[ABGJ-Debug] Item: drinkJarPureMineralWater
[ABGJ-Debug] CreateItem: resourceGlassJar
[ABGJ-Debug] CreateItemCount: 1
[ABGJ-Debug] UseJarRefund: True
[ABGJ-Debug] TRACKING: Jar count before consumption = 5
[ABGJ-Debug] consume() Postfix - Jar count: before=5, after=5
[ABGJ-Debug] consume() Postfix - JAR BROKE! Playing sound...
[ABGJ-Debug] PlayGlassBreakSound - Calling Manager.PlayInsidePlayerHead(...)
[ABGJ-Debug] PlayGlassBreakSound - Sound call completed (no exception)
```

**Log prefixes:**
- `[AudibleBreakingGlassJars]` - Normal operation messages
- `[ABGJ-Debug]` - Debug mode messages (verbose)

## Maintenance Guide

### After Game Updates

1. **Check if `ItemActionEat` changed:**
   - Look for changes to `consume()` and `ExecuteInstantAction()` methods
   - Verify `CreateItem`, `CreateItemCount`, `UseJarRefund` properties still exist
   - Check if jar return logic moved or changed

2. **Check if audio system changed:**
   - Verify `Audio.Manager.PlayInsidePlayerHead` signature
   - Check if `glassdestroy` sound still exists in `sounds.xml`

3. **Test with debug mode:**
   - Enable `<DebugMode>true</DebugMode>`
   - Drink water and check console for `[ABGJ-Debug]` messages
   - Verify patches are firing and jar counting works

### Common Issues

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| No debug output at all | Harmony patch not applying | Check if method signatures changed |
| "SKIP: Not a jar-refund item" | Item properties changed | Verify `UseJarRefund` property exists |
| Sound call succeeds but no audio | Wrong `PlayInsidePlayerHead` overload | Use 5-parameter version |
| Sound name not found | Sound removed from game | Check `sounds.xml`, use fallback |

### Adding New Features

**To support custom sound files:**
1. Sounds must be registered with the game's audio system
2. Create a `sounds.xml` in the mod's `Config/` folder
3. Reference the audio file path relative to mod folder
4. This requires additional implementation (currently TODO)

**To add volume control:**
1. The `PlayInsidePlayerHead` method doesn't support volume
2. Would need to use `Manager.Play()` with position instead
3. Or modify the sound definition in a custom `sounds.xml`

## File Structure

```
AudibleBreakingGlassJars/
├── AudibleBreakingGlassJars.csproj        # Main mod
├── BrokenGlassFromBrokenJars.csproj       # Addon
├── JarReturnOnCraft.csproj                # Addon
├── src/
│   ├── AudibleBreakingGlassJars/
│   │   └── Harmony_ItemActionEat.cs       # Sound on jar break
│   ├── BrokenGlassFromBrokenJars/
│   │   └── BrokenGlassFromBrokenJars.cs   # Give broken glass
│   └── JarReturnOnCraft/
│       ├── JarReturnOnCraft.cs            # Detection logic + config
│       ├── Patch_ReturnJarOnCraftStart.cs # Postfix on craft start
│       └── Patch_SkipJarContentOnCancel.cs # Prefix replaces cancel
├── TECHNICAL.md                           # This document
├── docs/
│   └── JAR_RETURN_ON_CRAFT_RESEARCH.md    # Original research notes
├── .gitignore
└── Release/
    ├── AudibleBreakingGlassJars/
    │   ├── AudibleBreakingGlassJars.dll
    │   ├── ModInfo.xml
    │   ├── Config/config.xml
    │   └── Sounds/glass-shatter.ogg
    ├── AudibleBreakingGlassJars_Addon_BrokenGlass/
    │   ├── BrokenGlassFromBrokenJars.dll
    │   └── ModInfo.xml
    └── AudibleBreakingGlassJars_Addon_JarReturn/
        ├── JarReturnOnCraft.dll
        ├── ModInfo.xml
        └── Config/JarContents.xml         # Optional overrides
```

## Compatibility

### ProxiCraft Integration
This mod is registered as compatible in ProxiCraft's `ModCompatibility.cs`:
```csharp
{ "AudibleBreakingGlassJars", "Glass jar break sound - compatible (patches ItemActionEat, only reads inventory)" }
```

We only READ inventory counts; we don't modify items. This makes us compatible with inventory-modifying mods.

### Potential Conflicts
- Other mods patching `ItemActionEat.consume()` - unlikely to conflict (we're observing, not modifying)
- Mods that change jar return mechanics - our detection should still work
- Audio overhaul mods - may affect which sounds are available

## Build Instructions

```bash
cd AudibleBreakingGlassJars
dotnet build -c Release
```

Output: `Release/AudibleBreakingGlassJars/AudibleBreakingGlassJars.dll`

The build also creates `Release/AudibleBreakingGlassJars.zip` for distribution.

## Version History

- **1.0.0** - AudibleBreakingGlassJars
  - Jar break detection via count comparison
  - Configurable sound name
  - Debug logging mode
  - Custom `glass-shatter.ogg` sound included (royalty-free)
  - Fixed: Use 5-parameter `PlayInsidePlayerHead` overload (2-param silently fails)
  - Fixed: Delay jar count check by one frame (inventory update is async)

- **0.1.0 Beta** - BrokenGlassFromBrokenJars
  - Initial release
  - Gives broken glass (`resourceBrokenGlass`) when jars break
  - Independent operation

- **0.1.0 Beta** - JarReturnOnCraft
  - Initial release
  - Returns empty jar when crafting starts
  - Skips jar content refund on cancel
  - Dynamic jar detection + XML config
  - Jar-to-jar recipe exception handling

---

# Addon: JarReturnOnCraft - Technical Details

## Problem Statement

Vanilla 7D2D has a crafting exploit with jar contents:

1. Player has 1 jar of water, no empty jars
2. Start crafting tea (uses water)
3. Immediately cancel the crafting job
4. Player gets the water back (standard cancel refund)
5. But player ALSO keeps the jar from the water → **free jar duplication**

This happens because:
- The `UseJarRefund` system is designed for *consumption*, not crafting
- Crafting cancel logic refunds ALL ingredients at 100%
- The jar "inside" the water was never supposed to be a separate item

## Solution Architecture

### Two-Patch Design

**Patch 1: ReturnJarOnCraftStart** (Postfix on `ItemActionEntryCraft.OnActivated`)
- Triggers when a craft job is added to the queue
- Detects jar content items in recipe ingredients
- Immediately returns empty jar to player inventory
- Exception: If recipe OUTPUT is also jar-based, skip (vanilla behavior)

**Patch 2: SkipJarContentOnCancel** (Prefix on `XUiC_RecipeStack.HandleOnPress`)
- Completely replaces cancel logic
- Iterates through recipe ingredients
- Skips refunding any jar content items
- Still refunds all non-jar ingredients normally

### Detection Logic

```csharp
public static bool IsJarContent(string itemName, out string jarItem)
{
    // 1. Check XML config overrides first
    if (_configItems.TryGetValue(itemName, out jarItem))
        return true;
    
    // 2. Dynamic detection via ItemActionEat properties
    ItemClass itemClass = ItemClass.GetItemClass(itemName);
    foreach (ItemAction action in itemClass.Actions)
    {
        if (action is ItemActionEat eatAction &&
            eatAction.UseJarRefund &&           // Has jar refund flag
            !string.IsNullOrEmpty(eatAction.CreateItem))  // Creates a jar item
        {
            jarItem = eatAction.CreateItem;
            return true;
        }
    }
    return false;
}
```

**Key detection criteria:**
- `ItemActionEat.UseJarRefund == true` - Item respects jar refund percentage
- `ItemActionEat.CreateItem != null` - Item creates something when consumed (the jar)

### Jar-to-Jar Recipe Exception

Some recipes convert one jar drink to another:
- `drinkJarBoiledWater` → `drinkJarGoldenrodTea`
- `drinkJarRiverWater` → `drinkJarBoiledWater`

In these cases, the jar stays with the liquid throughout - no jar should be returned at craft start, and vanilla cancel behavior is correct.

```csharp
public static bool RecipeOutputIsJarBased(Recipe recipe)
{
    string outputName = recipe.GetOutputItemClass()?.GetItemName();
    if (string.IsNullOrEmpty(outputName))
        return false;
    
    // Use same detection logic on the output
    return IsJarContent(outputName, out _);
}
```

### Configuration File

Optional XML config at `Config/JarContents.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<JarContents>
    <!-- Manual overrides for modded items -->
    <Item name="myModdedWater" jarItem="drinkJarEmpty" />
    <Item name="customBeverage" jarItem="modJarItem" />
</JarContents>
```

Config items are loaded first and take precedence over dynamic detection.

### VanillaJarFix Compatibility

[VanillaJarFix](https://www.nexusmods.com/7daystodie/mods/9353) removes `UseJarRefund="true"` from vanilla items via XML patches. This makes our dynamic detection automatically skip those items:

- VanillaJarFix removes `UseJarRefund` → `eatAction.UseJarRefund` is false
- Our `IsJarContent()` returns false → vanilla behavior used
- No conflict, both mods work together correctly

---
