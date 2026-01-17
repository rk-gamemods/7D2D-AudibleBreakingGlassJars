using HarmonyLib;
using System.Collections.Generic;

/// <summary>
/// Harmony patch for ItemActionEntryCraft.OnActivated
///
/// PREFIX: Capture queue state before crafting attempt.
/// POSTFIX: After ingredients are consumed, return empty jars for any jar content ingredients.
///
/// Code Reference: 7D2DCodebase/Assembly-CSharp/ItemActionEntryCraft.cs
/// - Lines 167-180: Ingredients removed only if AddItemToQueue returns true
/// - Line 175: childByType.RemoveItems (workstation input)
/// - Line 179: xui.PlayerInventory.RemoveItems (player inventory)
/// - Line 194: warnQueueFull() called if queue is full (no ingredients consumed)
///
/// BUG FIX: We must detect when AddItemToQueue succeeds vs fails. When the queue is
/// full, vanilla does NOT consume ingredients, so we must NOT return jars.
/// We detect this by counting queue items before and after - if count increased,
/// crafting succeeded and ingredients were consumed.
/// </summary>
[HarmonyPatch(typeof(ItemActionEntryCraft), "OnActivated")]
public static class Patch_ReturnJarOnCraftStart
{
    /// <summary>
    /// Stores the queue item count before the crafting attempt.
    /// Key = crafting window group instance hash, Value = count of occupied queue slots
    /// </summary>
    [System.ThreadStatic]
    private static int _queueCountBefore;

    /// <summary>
    /// PREFIX: Count how many queue slots are occupied before crafting attempt.
    /// </summary>
    public static void Prefix(ItemActionEntryCraft __instance)
    {
        _queueCountBefore = -1;

        XUi xui = __instance.ItemController?.xui;
        if (xui == null) return;

        // Find the visible crafting window group
        var craftingWindows = xui.GetChildrenByType<XUiC_CraftingWindowGroup>();
        foreach (var window in craftingWindows)
        {
            if (window.WindowGroup != null && window.WindowGroup.isShowing)
            {
                _queueCountBefore = CountQueueItems(window);
                break;
            }
        }
    }

    /// <summary>
    /// Count how many queue slots have recipes in them.
    /// </summary>
    private static int CountQueueItems(XUiC_CraftingWindowGroup craftingWindow)
    {
        var queue = craftingWindow.GetChildByType<XUiC_CraftingQueue>();
        if (queue == null) return 0;

        var recipeStacks = queue.GetRecipesToCraft();
        if (recipeStacks == null) return 0;

        int count = 0;
        foreach (var stack in recipeStacks)
        {
            if (stack != null && stack.HasRecipe())
                count++;
        }
        return count;
    }

    /// <summary>
    /// After craft starts and ingredients consumed, return empty jars for jar contents.
    /// EXCEPTION: If the recipe OUTPUT is also a jar-based item (like boiled water, tea),
    /// don't return the jar - it stays with the liquid.
    /// </summary>
    public static void Postfix(ItemActionEntryCraft __instance)
    {
        // Get the recipe that was just queued
        XUiC_RecipeEntry recipeEntry = __instance.ItemController as XUiC_RecipeEntry;
        if (recipeEntry == null || recipeEntry.Recipe == null)
            return;

        XUi xui = __instance.ItemController.xui;

        // BUG FIX: Check if crafting actually succeeded by comparing queue counts.
        // If queue count didn't increase, the queue was full and no ingredients were consumed.
        // In that case, we must NOT return jars (would be free jar exploit).
        if (_queueCountBefore >= 0)
        {
            var craftingWindows = xui.GetChildrenByType<XUiC_CraftingWindowGroup>();
            foreach (var window in craftingWindows)
            {
                if (window.WindowGroup != null && window.WindowGroup.isShowing)
                {
                    int queueCountAfter = CountQueueItems(window);
                    if (queueCountAfter <= _queueCountBefore)
                    {
                        // Queue didn't grow - crafting failed (queue full), don't return jars
                        if (JarReturnOnCraft.DebugMode)
                            UnityEngine.Debug.Log($"[JarReturnOnCraft] Queue full - not returning jars (before={_queueCountBefore}, after={queueCountAfter})");
                        return;
                    }
                    break;
                }
            }
        }

        Recipe recipe = recipeEntry.Recipe;

        // EXCEPTION: If the output is also a jar-based drink, the jar stays with the liquid
        // Examples: dirty water → clean water, water + goldenrod → tea
        if (JarReturnOnCraft.RecipeOutputIsJarBased(recipe))
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        
        // Get craft count from the control
        XUiC_RecipeCraftCount craftCountControl = __instance.ItemController.WindowGroup.Controller
            .GetChildByType<XUiC_RecipeCraftCount>();
        int craftCount = craftCountControl?.Count ?? 1;
        
        // Check each ingredient for jar contents
        foreach (ItemStack ingredient in recipe.ingredients)
        {
            string itemName = ingredient.itemValue.ItemClass?.GetItemName();
            if (string.IsNullOrEmpty(itemName))
                continue;
                
            if (JarReturnOnCraft.IsJarContent(itemName, out string jarItemName))
            {
                // Calculate how many jars to return
                int jarCount = ingredient.count * craftCount;
                
                // Create the jar item stack
                ItemValue jarValue = ItemClass.GetItem(jarItemName);
                if (jarValue.IsEmpty())
                {
                    UnityEngine.Debug.LogWarning($"[JarReturnOnCraft] Could not find jar item: {jarItemName}");
                    continue;
                }
                
                ItemStack jarStack = new ItemStack(jarValue, jarCount);
                
                // Try to add to player inventory
                if (!xui.PlayerInventory.AddItem(jarStack, playCollectSound: true))
                {
                    // Inventory full - drop at player feet
                    GameManager.Instance.ItemDropServer(
                        jarStack, 
                        player.position, 
                        UnityEngine.Vector3.zero, 
                        player.entityId, 
                        120f);
                    GameManager.ShowTooltip(player, Localization.Get("xuiInventoryFullDropping"));
                }
                
                if (JarReturnOnCraft.DebugMode)
                    UnityEngine.Debug.Log($"[JarReturnOnCraft] Returned {jarCount}x {jarItemName} for crafting with {itemName}");
            }
        }
    }
}
