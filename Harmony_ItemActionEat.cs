using System;
using System.IO;
using System.Xml;
using Audio;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// Audible Breaking Glass Jars - Plays a sound when drinking fluids causes the glass jar to break.
/// Works with the game's jar return percentage setting.
/// </summary>
public class AudibleBreakingGlassJars : IModApi
{
    public static string SoundName = "glassdestroy";
    private static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        LoadConfig();

        Debug.Log($"[AudibleBreakingGlassJars] Initialized. Using sound: {SoundName}");

        var harmony = new Harmony("com.7d2d.audiblebreakingglassjars");
        harmony.PatchAll();
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = Path.Combine(ModPath, "Config", "config.xml");
            if (File.Exists(configPath))
            {
                var doc = new XmlDocument();
                doc.Load(configPath);
                var soundNode = doc.SelectSingleNode("//SoundName");
                if (soundNode != null && !string.IsNullOrWhiteSpace(soundNode.InnerText))
                {
                    SoundName = soundNode.InnerText.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudibleBreakingGlassJars] Failed to load config: {ex.Message}");
        }
    }
}

/// <summary>
/// Harmony patches for ItemActionEat to detect when glass jars break.
/// Patches both consume() for animated drinking and ExecuteInstantAction() for instant consumption.
/// </summary>
[HarmonyPatch(typeof(ItemActionEat))]
public static class ItemActionEat_Patches
{
    // ThreadStatic to handle potential multi-threaded scenarios safely
    [ThreadStatic] private static bool _checkingJar;
    [ThreadStatic] private static int _jarCountBefore;
    [ThreadStatic] private static EntityPlayerLocal _player;

    #region consume() patches - Animated drinking path

    [HarmonyPrefix]
    [HarmonyPatch("consume")]
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
            _jarCountBefore = CountGlassJars(player);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("consume")]
    private static void Consume_Postfix()
    {
        if (!_checkingJar || _player == null)
            return;

        try
        {
            int jarCountAfter = CountGlassJars(_player);

            // If jar count didn't increase, the jar broke
            if (jarCountAfter <= _jarCountBefore)
            {
                PlayGlassBreakSound(_player);
            }
        }
        finally
        {
            _checkingJar = false;
            _player = null;
        }
    }

    #endregion

    #region ExecuteInstantAction() patches - Instant consumption path

    [HarmonyPrefix]
    [HarmonyPatch("ExecuteInstantAction")]
    private static void ExecuteInstantAction_Prefix(ItemActionEat __instance, EntityAlive ent)
    {
        _checkingJar = false;

        if (__instance.CreateItem == null || __instance.CreateItemCount <= 0 || !__instance.UseJarRefund)
            return;

        if (ent is EntityPlayerLocal player)
        {
            _checkingJar = true;
            _player = player;
            _jarCountBefore = CountGlassJars(player);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("ExecuteInstantAction")]
    private static void ExecuteInstantAction_Postfix()
    {
        if (!_checkingJar || _player == null)
            return;

        try
        {
            int jarCountAfter = CountGlassJars(_player);

            if (jarCountAfter <= _jarCountBefore)
            {
                PlayGlassBreakSound(_player);
            }
        }
        finally
        {
            _checkingJar = false;
            _player = null;
        }
    }

    #endregion

    #region Helpers

    private static int CountGlassJars(EntityPlayerLocal player)
    {
        try
        {
            var jarItem = ItemClass.GetItem("resourceGlassJar");
            return player.bag.GetItemCount(jarItem);
        }
        catch
        {
            return 0;
        }
    }

    private static void PlayGlassBreakSound(EntityPlayerLocal player)
    {
        try
        {
            Manager.PlayInsidePlayerHead(AudibleBreakingGlassJars.SoundName, player.entityId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudibleBreakingGlassJars] Failed to play sound: {ex.Message}");
        }
    }

    #endregion
}
