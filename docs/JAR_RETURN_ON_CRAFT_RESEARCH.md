# Jar Return On Craft - Research Document

**Mod Name:** JarReturnOnCraft  
**Type:** C# Harmony Addon Mod  
**Parent Mod:** AudibleBreakingGlassJars  
**Research Date:** January 11, 2026  
**Game Version:** 7 Days to Die V2.5 (b23)

---

## Feature Overview

### Core Mechanic
When a player starts crafting a recipe that uses "jar content" items (like `drinkJarBoiledWater`), the glass jar (`drinkJarEmpty`) is immediately returned to the player - simulating pouring water into the cooking pot.

If the crafting job is later cancelled, the "jar content" ingredient is NOT returned (since the jar was already given back).

### Critical Exception: Jar-to-Jar Recipes
**If the recipe OUTPUT is also a jar-based item, vanilla behavior applies.**

| Recipe Type | Example | On Start | On Cancel |
|-------------|---------|----------|-----------|
| Jar → Food | Water + Meat → Boiled Meat | Jar returned | Water NOT returned |
| Jar → Jar | Dirty Water → Clean Water | Normal | Water returned |
| Jar → Jar | Water + Goldenrod → Tea | Normal | Water + Goldenrod returned |

This ensures you don't lose jars when making drinks - the liquid transfers to the output jar.

### Gameplay Justification
- **Realism**: You pour water from a jar into a pot - the jar is now empty and available
- **Consistency**: Aligns with the glass jar ecosystem from AudibleBreakingGlassJars  
- **Balance**: Prevents exploit of getting free jars from cancel-spam

---

## Technical Research

### 1. Crafting Flow Analysis

#### When "Craft" Button is Pressed

**File:** [ItemActionEntryCraft.cs](../../7D2DCodebase/Assembly-CSharp/ItemActionEntryCraft.cs)  
**Method:** `OnActivated()` (Lines 104-194)

```csharp
// Line 167-180: Ingredients are removed AFTER recipe is added to queue
if (xUiC_CraftingWindowGroup.AddItemToQueue(recipe2))
{
    if (flag)
    {
        tempIngredientList.Clear();
    }
    if (childByType != null)
    {
        childByType.RemoveItems(recipe2.ingredients, craftCountControl.Count, tempIngredientList);  // Line 175
    }
    else
    {
        xui.PlayerInventory.RemoveItems(recipe2.ingredients, craftCountControl.Count, tempIngredientList);  // Line 179
    }
    // ...
}
```

**Key Insight:** Ingredients are consumed via `RemoveItems()` call. This is where we would POSTFIX to add the jar back.

#### When "Cancel" is Clicked

**File:** [XUiC_RecipeStack.cs](../../7D2DCodebase/Assembly-CSharp/XUiC_RecipeStack.cs)  
**Method:** `HandleOnPress()` (Lines 173-232)

```csharp
// Lines 185-214: Full ingredient return logic for non-forge workstations
int[] array = new int[recipe.ingredients.Count];
for (int j = 0; j < recipe.ingredients.Count; j++)
{
    array[j] = recipe.ingredients[j].count * recipeCount;  // Line 188 - FULL REFUND
    ItemStack itemStack2 = new ItemStack(recipe.ingredients[j].itemValue.Clone(), array[j]);
    if ((childByType2 == null) 
        ? base.xui.PlayerInventory.AddItem(itemStack2, playCollectSound: true)  // Line 191
        : (childByType2.AddToItemStackArray(itemStack2) != -1))
    {
        array[j] = 0;
    }
    else
    {
        array[j] = itemStack2.count;
    }
}
// Lines 200-210: Drop overflow items at player feet
bool flag = false;
for (int k = 0; k < array.Length; k++)
{
    if (array[k] > 0)
    {
        flag = true;
        GameManager.Instance.ItemDropServer(
            new ItemStack(recipe.ingredients[k].itemValue.Clone(), array[k]), 
            entityPlayer.position, Vector3.zero, entityPlayer.entityId, 120f);
    }
}
```

**Key Insight:** The loop at line 187 iterates ALL ingredients with 100% refund (`recipe.ingredients[j].count * recipeCount`). We need to PREFIX this method to skip specific ingredients.

---

### 2. Recipe Structure

**File:** [Recipe.cs](../../7D2DCodebase/Assembly-CSharp/Recipe.cs)

Recipes contain:
- `List<ItemStack> ingredients` - List of required items with counts
- `int itemValueType` - Output item type
- `string craftingArea` - Where it's crafted (campfire, workbench, etc.)

#### Example Recipe from XML

**File:** [recipes.xml](../../7D2DCodebase/Data/Config/recipes.xml) (Lines 1752-1754)
```xml
<recipe name="drinkJarBoiledWater" count="1" craft_area="campfire" craft_tool="toolCookingPot" tags="...">
    <ingredient name="drinkJarRiverWater" count="1"/>
</recipe>
```

---

### 3. Jar Content Items (Vanilla)

Items that use glass jars as containers (items with `Create_item="drinkJarEmpty"` property):

**File:** [items.xml](../../7D2DCodebase/Data/Config/items.xml)

| Item Name | Line | Notes |
|-----------|------|-------|
| `drinkJarRiverWater` | ~15860 | Raw water |
| `drinkJarBoiledWater` | ~15912 | Boiled water |
| `drinkJarBeer` | ~15975 | Beer |
| `drinkJarGoldenRodTea` | ~16038 | Goldenrod tea |
| `drinkJarRedTea` | ~16308 | Red tea |
| `drinkJarCoffee` | ~16639 | Coffee |
| `drinkJarGrandpasMoonshine` | ~16773 | Moonshine |
| `drinkJarGrandpasLearningElixir` | ~16807 | Learning elixir |
| `drinkJarYuccaJuice` | ~16875 | Yucca juice |
| `drinkJarMegaCrush` | ~16913 | Mega crush |
| `foodHoney` | ~17046 | Honey |
| (and more...) | | |

---

### 4. Network Safety Analysis

#### Inventory Operations Used

| Operation | Method | File | Syncs? |
|-----------|--------|------|--------|
| Add item on craft start | `XUiM_PlayerInventory.AddItem()` | [XUiM_PlayerInventory.cs#L141](../../7D2DCodebase/Assembly-CSharp/XUiM_PlayerInventory.cs#L141) | ✅ Yes (via Bag events) |
| Skip item on cancel | Don't call `AddItem()` | N/A | ✅ Yes (nothing to sync) |
| Drop overflow | `GameManager.Instance.ItemDropServer()` | [XUiC_RecipeStack.cs#L206](../../7D2DCodebase/Assembly-CSharp/XUiC_RecipeStack.cs#L206) | ✅ Yes (server RPC) |

#### Inventory Change Propagation

**File:** [XUiM_PlayerInventory.cs](../../7D2DCodebase/Assembly-CSharp/XUiM_PlayerInventory.cs) (Lines 44-75)

```csharp
public XUiM_PlayerInventory(XUi _xui, EntityPlayerLocal _player)
{
    // ...
    backpack = localPlayer.bag;
    toolbelt = localPlayer.inventory;
    backpack.OnBackpackItemsChangedInternal += onBackpackItemsChanged;  // Line 50
    toolbelt.OnToolbeltItemsChangedInternal += onToolbeltItemsChanged;  // Line 51
    // ...
}
```

The game uses event-based sync. When `AddItem()` modifies `bag` or `inventory`, internal events fire that propagate changes.

#### Workstation Sync

**File:** [TileEntityWorkstation.cs](../../7D2DCodebase/Assembly-CSharp/TileEntityWorkstation.cs)

Workstation state syncs via `NetPackageTileEntity` when player closes UI. The `bUserAccessing` flag prevents conflicts during UI interaction.

**Conclusion:** Both patch points use standard APIs that handle networking internally. No custom sync code needed.

---

### 5. Implementation Plan

#### Patch 1: Return Jar on Craft Start (POSTFIX)

**Target:** `ItemActionEntryCraft.OnActivated()`  
**Timing:** After ingredients consumed (line ~179)  
**Logic:**
1. Iterate `recipe.ingredients`
2. For each ingredient, check if it's in our "jar content" list
3. If yes, call `AddItem(new ItemStack(jarItem, ingredientCount))`

```csharp
[HarmonyPatch(typeof(ItemActionEntryCraft), "OnActivated")]
public class Patch_ReturnJarOnCraft
{
    public static void Postfix(ItemActionEntryCraft __instance)
    {
        // Check recipe ingredients for jar items
        // Add empty jars back to player
    }
}
```

#### Patch 2: Skip Jar Content on Cancel (PREFIX)

**Target:** `XUiC_RecipeStack.HandleOnPress()`  
**Timing:** Before original method  
**Logic:**
1. Copy original method logic
2. In ingredient loop, skip items that are in our "jar content" list
3. Return `false` to prevent original execution

```csharp
[HarmonyPatch(typeof(XUiC_RecipeStack), "HandleOnPress")]
public class Patch_SkipJarOnCancel
{
    public static bool Prefix(XUiC_RecipeStack __instance, 
        Recipe ___recipe, int ___recipeCount, ...)
    {
        // Custom cancel logic that skips jar contents
        return false; // Skip original
    }
}
```

---

### 6. Configuration Design - IMPLEMENTED

The mod uses **dynamic detection** plus optional **XML config overrides**.

#### Dynamic Detection (Automatic)

At runtime, the mod checks if an item has:
- `ItemActionEat` action with `UseJarRefund = true`
- `CreateItem` property set (e.g., `"drinkJarEmpty"`)

**Code Reference:** [ItemActionEat.cs](../../7D2DCodebase/Assembly-CSharp/ItemActionEat.cs) (Lines 53-65)

```csharp
if (_props.Values.ContainsKey("Create_item"))
{
    CreateItem = _props.Values["Create_item"];
    // ...
    if (_props.Values.ContainsKey("Use_jar_refund"))
    {
        UseJarRefund = StringParsers.ParseBool(_props.Values["Use_jar_refund"]);
    }
}
```

This automatically works with:
- All vanilla jar items
- Any modded items that follow the same XML pattern

#### XML Config Override (Manual)

For items that don't follow the standard pattern, add to `Config/JarContents.xml`:

```xml
<JarContents>
    <!-- Modded items that don't use UseJarRefund -->
    <item name="moddedDrinkInJar" jar="drinkJarEmpty" />
    
    <!-- Custom jar types from other mods -->
    <item name="moddedPotionInBottle" jar="moddedEmptyBottle" />
</JarContents>
```

#### Detection Priority
1. Check XML config overrides first
2. Fall back to dynamic detection via `ItemActionEat`
3. Cache results for performance

---

### 7. Edge Cases

| Scenario | Expected Behavior | Implementation Notes |
|----------|-------------------|---------------------|
| Multiple jar items in recipe | Return jar for each | Iterate all ingredients |
| Quantity > 1 | Return matching jar count | `jarCount = ingredientCount` |
| Inventory full on craft | Drop jar at feet | Use `ItemDropServer()` fallback |
| Forge/material workstation | Skip - different code path | Check for `materialGrid != null` |
| Recipe with jar + other items | Return jar, cancel returns others | Only filter jar-specific items |
| Queued multiple items | Jar returned per queue item | `recipeCount` multiplier applies |
| **Output is jar-based** | **Vanilla behavior** | Check `RecipeOutputIsJarBased()` |
| Modded jar items | Auto-detect if follows pattern | Falls back to XML config |

---

### 8. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Multiplayer desync | Low | Using standard inventory APIs |
| Mod conflicts | Low | Narrow patch targets |
| Future game updates | Medium | Simple patches, easy to update |
| Performance impact | Negligible | Only runs on craft/cancel events |

---

## Conclusion

This feature is **feasible and network-safe** using two Harmony patches:

1. **POSTFIX** on `ItemActionEntryCraft.OnActivated()` - return jar when craft starts
2. **PREFIX** on `XUiC_RecipeStack.HandleOnPress()` - skip jar item on cancel

Estimated implementation: ~150-200 lines of C# code plus XML config.

---

## Code References

| File | Full Path | Purpose |
|------|-----------|---------|
| XUiC_RecipeStack.cs | `7D2DCodebase/Assembly-CSharp/XUiC_RecipeStack.cs` | Cancel handler (HandleOnPress) |
| ItemActionEntryCraft.cs | `7D2DCodebase/Assembly-CSharp/ItemActionEntryCraft.cs` | Craft button handler (OnActivated) |
| XUiC_CraftingQueue.cs | `7D2DCodebase/Assembly-CSharp/XUiC_CraftingQueue.cs` | Queue management |
| XUiM_PlayerInventory.cs | `7D2DCodebase/Assembly-CSharp/XUiM_PlayerInventory.cs` | Inventory add/remove |
| recipes.xml | `7D2DCodebase/Data/Config/recipes.xml` | Recipe definitions |
| items.xml | `7D2DCodebase/Data/Config/items.xml` | Item definitions with Create_item |
