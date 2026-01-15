using HarmonyLib;
using System.Collections.Generic;

/// <summary>
/// Harmony patch for ItemActionEntryCraft.OnActivated
/// 
/// POSTFIX: After ingredients are consumed, return empty jars for any jar content ingredients.
/// 
/// Code Reference: 7D2DCodebase/Assembly-CSharp/ItemActionEntryCraft.cs
/// - Lines 167-180: Ingredients removed after AddItemToQueue succeeds
/// - Line 175: childByType.RemoveItems (workstation input)
/// - Line 179: xui.PlayerInventory.RemoveItems (player inventory)
/// </summary>
[HarmonyPatch(typeof(ItemActionEntryCraft), "OnActivated")]
public static class Patch_ReturnJarOnCraftStart
{
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

        Recipe recipe = recipeEntry.Recipe;
        
        // EXCEPTION: If the output is also a jar-based drink, the jar stays with the liquid
        // Examples: dirty water → clean water, water + goldenrod → tea
        if (JarReturnOnCraft.RecipeOutputIsJarBased(recipe))
            return;
        
        XUi xui = __instance.ItemController.xui;
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
