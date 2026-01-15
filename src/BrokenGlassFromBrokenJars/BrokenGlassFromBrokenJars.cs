using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// BrokenGlassFromBrokenJars - Addon mod for AudibleBreakingGlassJars
/// 
/// When a glass jar breaks (detected by the same mechanism as the sound mod),
/// adds broken glass (resourceBrokenGlass) to the player's inventory.
/// If inventory is full, drops the item on the ground.
/// 
/// This addon hooks into the same ItemActionEat patches but operates independently.
/// It does NOT require the main AudibleBreakingGlassJars mod to be installed,
/// but is designed as a companion addon.
/// </summary>
public class BrokenGlassFromBrokenJars : IModApi
{
    private const string LOG_PREFIX = "[BrokenGlassFromBrokenJars]";
    
    /// <summary>
    /// The item name for broken glass in vanilla 7D2D
    /// </summary>
    public const string BROKEN_GLASS_ITEM = "resourceBrokenGlass";
    
    /// <summary>
    /// How many broken glass pieces to give when a jar breaks
    /// </summary>
    public static int BrokenGlassCount = 1;
    
    public static bool DebugMode = false;

    public void InitMod(Mod _modInstance)
    {
        var harmony = new Harmony("com.7d2d.brokenglassfrombrokenjar");
        harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        
        Debug.Log($"{LOG_PREFIX} Initialized - will give {BrokenGlassCount}x {BROKEN_GLASS_ITEM} when jars break");
    }
    
    /// <summary>
    /// Gives broken glass to the player. Drops on ground if inventory full.
    /// </summary>
    public static void GiveBrokenGlass(EntityPlayerLocal player)
    {
        if (player == null)
            return;
            
        try
        {
            ItemValue brokenGlassValue = ItemClass.GetItem(BROKEN_GLASS_ITEM);
            if (brokenGlassValue.IsEmpty())
            {
                Debug.LogWarning($"{LOG_PREFIX} Could not find item: {BROKEN_GLASS_ITEM}");
                return;
            }
            
            ItemStack brokenGlassStack = new ItemStack(brokenGlassValue, BrokenGlassCount);
            
            // Try to add to player's bag first
            if (player.bag.CanStack(brokenGlassStack))
            {
                player.bag.AddItem(brokenGlassStack);
                player.AddUIHarvestingItem(brokenGlassStack);
                if (DebugMode)
                    Debug.Log($"{LOG_PREFIX} Added {BrokenGlassCount}x {BROKEN_GLASS_ITEM} to backpack");
                return;
            }
            
            // Try toolbelt
            if (player.inventory.CanStack(brokenGlassStack))
            {
                player.inventory.AddItem(brokenGlassStack);
                player.AddUIHarvestingItem(brokenGlassStack);
                if (DebugMode)
                    Debug.Log($"{LOG_PREFIX} Added {BrokenGlassCount}x {BROKEN_GLASS_ITEM} to toolbelt");
                return;
            }
            
            // Inventory full - drop on ground
            GameManager.Instance.ItemDropServer(
                brokenGlassStack,
                player.position,
                Vector3.zero,
                player.entityId,
                120f);
            
            // Show tooltip that item was dropped
            GameManager.ShowTooltip(player, Localization.Get("xuiInventoryFullDropping"));
                
            if (DebugMode)
                Debug.Log($"{LOG_PREFIX} Inventory full - dropped {BrokenGlassCount}x {BROKEN_GLASS_ITEM} on ground");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LOG_PREFIX} Failed to give broken glass: {ex.Message}");
        }
    }
}

/// <summary>
/// Harmony patches for ItemActionEat to detect when glass jars break.
/// Uses the same detection logic as AudibleBreakingGlassJars but gives items instead of playing sound.
/// </summary>
[HarmonyPatch(typeof(ItemActionEat))]
public static class BrokenGlass_ItemActionEat_Patches
{
    [ThreadStatic] private static bool _checkingJar;
    [ThreadStatic] private static int _jarCountBefore;
    [ThreadStatic] private static EntityPlayerLocal _player;
    [ThreadStatic] private static string _createItemName;

    #region consume() patches - Animated drinking path

    [HarmonyPrefix]
    [HarmonyPatch("consume")]
    [HarmonyPriority(Priority.Low)] // Run after main mod's prefix
    private static void Consume_Prefix(ItemActionEat __instance, ItemActionData _actionData)
    {
        _checkingJar = false;

        // Only track if this item uses jar refund (meaning jar can break)
        if (__instance.CreateItem == null || __instance.CreateItemCount <= 0 || !__instance.UseJarRefund)
            return;

        var entity = _actionData.invData.holdingEntity;
        if (entity is EntityPlayerLocal player)
        {
            _checkingJar = true;
            _player = player;
            _createItemName = __instance.CreateItem;
            _jarCountBefore = CountItem(player, _createItemName);
            
            if (BrokenGlassFromBrokenJars.DebugMode)
                Debug.Log($"[BrokenGlass-Debug] TRACKING: '{_createItemName}' count before = {_jarCountBefore}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("consume")]
    [HarmonyPriority(Priority.Low)] // Run after main mod's postfix
    private static void Consume_Postfix()
    {
        if (!_checkingJar || _player == null)
            return;

        // Capture values for the coroutine
        int jarCountBefore = _jarCountBefore;
        EntityPlayerLocal player = _player;
        string createItemName = _createItemName;

        // Reset state immediately
        _checkingJar = false;
        _player = null;
        _createItemName = null;

        // Wait for inventory to update, then check if jar was returned
        GameManager.Instance.StartCoroutine(CheckJarAndGiveBrokenGlass(player, jarCountBefore, createItemName));
    }

    /// <summary>
    /// Waits for inventory to update, then checks if jar was returned.
    /// Checks: 1) bag/toolbelt count increased, 2) jar dropped on ground nearby.
    /// Only gives broken glass if jar truly broke (not in inventory AND not on ground).
    /// </summary>
    private static IEnumerator CheckJarAndGiveBrokenGlass(EntityPlayerLocal player, int countBefore, string itemName)
    {
        // Wait multiple frames for inventory to update (same timing as main mod)
        yield return null;
        yield return null;
        yield return null;

        if (player == null)
            yield break;

        int countAfter = CountItem(player, itemName);

        // If item count increased, jar was returned to inventory/toolbelt
        if (countAfter > countBefore)
        {
            if (BrokenGlassFromBrokenJars.DebugMode)
                Debug.Log($"[BrokenGlass-Debug] Jar returned to inventory (after > before)");
            yield break;
        }

        // Count didn't increase - check if jar was dropped on ground (inventory full scenario)
        if (IsItemDroppedNearby(player, itemName))
        {
            if (BrokenGlassFromBrokenJars.DebugMode)
                Debug.Log($"[BrokenGlass-Debug] Jar dropped on ground (inventory full)");
            yield break;
        }

        // Jar not in inventory AND not on ground = jar broke - give broken glass
        if (BrokenGlassFromBrokenJars.DebugMode)
            Debug.Log($"[BrokenGlass-Debug] JAR BROKE! (not in inventory, not on ground) Giving broken glass...");
        BrokenGlassFromBrokenJars.GiveBrokenGlass(player);
    }

    #endregion

    #region ExecuteInstantAction() patches - Instant consumption path

    [HarmonyPrefix]
    [HarmonyPatch("ExecuteInstantAction")]
    [HarmonyPriority(Priority.Low)]
    private static void ExecuteInstantAction_Prefix(ItemActionEat __instance, EntityAlive ent)
    {
        _checkingJar = false;

        if (__instance.CreateItem == null || __instance.CreateItemCount <= 0 || !__instance.UseJarRefund)
            return;

        if (ent is EntityPlayerLocal player)
        {
            _checkingJar = true;
            _player = player;
            _createItemName = __instance.CreateItem;
            _jarCountBefore = CountItem(player, _createItemName);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("ExecuteInstantAction")]
    [HarmonyPriority(Priority.Low)]
    private static void ExecuteInstantAction_Postfix()
    {
        if (!_checkingJar || _player == null)
            return;

        int jarCountBefore = _jarCountBefore;
        EntityPlayerLocal player = _player;
        string createItemName = _createItemName;

        _checkingJar = false;
        _player = null;
        _createItemName = null;

        GameManager.Instance.StartCoroutine(CheckJarAndGiveBrokenGlass(player, jarCountBefore, createItemName));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Range in blocks to check for dropped items on the ground.
    /// Small radius since drops happen at player's feet.
    /// </summary>
    private const float GROUND_CHECK_RANGE = 5f;

    /// <summary>
    /// Reusable list for entity scanning to avoid allocations.
    /// </summary>
    private static readonly List<Entity> _entityScanList = new List<Entity>();

    private static int CountItem(EntityPlayerLocal player, string itemName)
    {
        try
        {
            var item = ItemClass.GetItem(itemName);
            // Count in both bag (backpack) and inventory (toolbelt)
            int bagCount = player.bag.GetItemCount(item);
            int toolbeltCount = player.inventory.GetItemCount(item);
            return bagCount + toolbeltCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if an item was dropped on the ground nearby (within GROUND_CHECK_RANGE blocks).
    /// Used when inventory is full and jar couldn't be added to bag/toolbelt.
    /// </summary>
    private static bool IsItemDroppedNearby(EntityPlayerLocal player, string itemName)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (world == null)
                return false;

            // Get the item type ID we're looking for
            ItemValue searchItem = ItemClass.GetItem(itemName);
            if (searchItem.IsEmpty())
                return false;

            int searchItemType = searchItem.type;

            // Create bounds around player position
            Vector3 playerPos = player.position;
            Bounds bounds = new Bounds(playerPos, new Vector3(GROUND_CHECK_RANGE * 2, GROUND_CHECK_RANGE * 2, GROUND_CHECK_RANGE * 2));

            // Scan for EntityItem (dropped items) in range
            _entityScanList.Clear();
            world.GetEntitiesInBounds(typeof(EntityItem), bounds, _entityScanList);

            for (int i = 0; i < _entityScanList.Count; i++)
            {
                if (_entityScanList[i] is EntityItem droppedItem)
                {
                    // Check if this dropped item matches what we're looking for
                    if (droppedItem.itemStack.itemValue.type == searchItemType)
                    {
                        if (BrokenGlassFromBrokenJars.DebugMode)
                            Debug.Log($"[BrokenGlass-Debug] Found dropped {itemName} on ground at distance {Vector3.Distance(playerPos, droppedItem.position):F1}");
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            if (BrokenGlassFromBrokenJars.DebugMode)
                Debug.Log($"[BrokenGlass-Debug] Error checking ground drops: {ex.Message}");
            return false;
        }
    }

    #endregion
}
