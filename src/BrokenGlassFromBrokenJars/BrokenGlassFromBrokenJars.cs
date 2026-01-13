using System;
using System.Collections;
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
                if (DebugMode)
                    Debug.Log($"{LOG_PREFIX} Added {BrokenGlassCount}x {BROKEN_GLASS_ITEM} to backpack");
                return;
            }
            
            // Try toolbelt
            if (player.inventory.CanStack(brokenGlassStack))
            {
                player.inventory.AddItem(brokenGlassStack);
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

    private static IEnumerator CheckJarAndGiveBrokenGlass(EntityPlayerLocal player, int countBefore, string itemName)
    {
        // Wait multiple frames for inventory to update (same timing as main mod)
        yield return null;
        yield return null;
        yield return null;

        if (player == null)
            yield break;

        int countAfter = CountItem(player, itemName);

        // If item count didn't increase, the jar broke - give broken glass
        if (countAfter <= countBefore)
        {
            if (BrokenGlassFromBrokenJars.DebugMode)
                Debug.Log($"[BrokenGlass-Debug] JAR BROKE! Giving broken glass...");
            BrokenGlassFromBrokenJars.GiveBrokenGlass(player);
        }
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

    private static int CountItem(EntityPlayerLocal player, string itemName)
    {
        try
        {
            var item = ItemClass.GetItem(itemName);
            return player.bag.GetItemCount(item);
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}
