using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Harmony patch for XUiC_RecipeStack.HandleOnPress (cancel crafting)
/// 
/// PREFIX: Replace default cancel logic to skip returning jar content items.
/// Since the player already received the empty jar when crafting started,
/// we should NOT return the jar content (water, etc.) on cancel.
/// 
/// EXCEPTION: If the recipe OUTPUT is also a jar-based item (like boiled water, tea),
/// use vanilla behavior - the jar stays with the liquid throughout.
/// 
/// Code Reference: 7D2DCodebase/Assembly-CSharp/XUiC_RecipeStack.cs
/// - Lines 173-232: HandleOnPress method (cancel handler)
/// - Lines 185-214: Ingredient return loop for non-forge workstations
/// - Line 188: array[j] = recipe.ingredients[j].count * recipeCount (100% refund)
/// </summary>
[HarmonyPatch(typeof(XUiC_RecipeStack), "HandleOnPress")]
public static class Patch_SkipJarContentOnCancel
{
    /// <summary>
    /// Replace cancel logic to filter out jar content items from refund.
    /// EXCEPTION: If recipe output is jar-based, use vanilla behavior.
    /// </summary>
    /// <returns>false to skip original method, true to run original</returns>
    public static bool Prefix(
        XUiC_RecipeStack __instance,
        XUiController _sender,
        int _mouseButton,
        Recipe ___recipe,
        int ___recipeCount,
        ref bool ___isCrafting,
        ref ItemValue ___originalItem,
        XUiC_CraftingQueue ___Owner)
    {
        // Let original handle null recipe case
        if (___recipe == null)
            return true;
        
        // EXCEPTION: If the output is also a jar-based drink, use vanilla cancel behavior
        // Examples: dirty water → clean water, water + goldenrod → tea
        // In these cases, the jar was never returned, so refund the water normally
        if (JarReturnOnCraft.RecipeOutputIsJarBased(___recipe))
            return true;
        
        XUiC_WorkstationMaterialInputGrid materialGrid = __instance.windowGroup.Controller
            .GetChildByType<XUiC_WorkstationMaterialInputGrid>();
        XUiC_WorkstationInputGrid inputGrid = __instance.windowGroup.Controller
            .GetChildByType<XUiC_WorkstationInputGrid>();
        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        
        // Forge-type workstations use material weight system - let original handle
        if (materialGrid != null)
            return true;
        
        // Handle repair item return (unchanged from vanilla)
        if (___originalItem != null && !___originalItem.Equals(ItemValue.None))
        {
            ItemStack itemStack = new ItemStack(___originalItem.Clone(), 1);
            if (!__instance.xui.PlayerInventory.AddItem(itemStack))
            {
                GameManager.ShowTooltip(player, Localization.Get("xuiInventoryFullDropping"));
                GameManager.Instance.ItemDropServer(
                    new ItemStack(___originalItem.Clone(), 1), 
                    player.position, Vector3.zero, player.entityId, 120f);
            }
            ___originalItem = ItemValue.None.Clone();
        }
        
        // Return ingredients - MODIFIED to skip jar contents
        int[] remaining = new int[___recipe.ingredients.Count];
        for (int i = 0; i < ___recipe.ingredients.Count; i++)
        {
            ItemStack ingredient = ___recipe.ingredients[i];
            string itemName = ingredient.itemValue.ItemClass?.GetItemName() ?? "";
            
            // CHECK: Is this a jar content item?
            if (JarReturnOnCraft.IsJarContent(itemName, out _))
            {
                // Skip returning jar contents - player already got the empty jar
                UnityEngine.Debug.Log($"[JarReturnOnCraft] Skipping refund of {ingredient.count * ___recipeCount}x {itemName} (jar already returned)");
                remaining[i] = 0;
                continue;
            }
            
            // Standard refund logic for non-jar items
            remaining[i] = ingredient.count * ___recipeCount;
            ItemStack refundStack = new ItemStack(ingredient.itemValue.Clone(), remaining[i]);
            
            bool added = (inputGrid == null) 
                ? __instance.xui.PlayerInventory.AddItem(refundStack, playCollectSound: true)
                : (inputGrid.AddToItemStackArray(refundStack) != -1);
                
            if (added)
            {
                remaining[i] = 0;
            }
            else
            {
                remaining[i] = refundStack.count;
            }
        }
        
        // Drop any overflow items at player feet
        bool hadOverflow = false;
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] > 0)
            {
                hadOverflow = true;
                GameManager.Instance.ItemDropServer(
                    new ItemStack(___recipe.ingredients[i].itemValue.Clone(), remaining[i]), 
                    player.position, Vector3.zero, player.entityId, 120f);
            }
        }
        if (hadOverflow)
        {
            GameManager.ShowTooltip(player, Localization.Get("xuiInventoryFullDropping"));
        }
        
        // Clear the recipe slot
        ___isCrafting = false;
        __instance.ClearRecipe();
        
        // Refresh queue
        ___Owner?.RefreshQueue();
        __instance.windowGroup.Controller.SetAllChildrenDirty();
        
        // Skip original method
        return false;
    }
}
