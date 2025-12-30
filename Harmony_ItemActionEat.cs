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
    private const string DEFAULT_SOUND = "glassdestroy";
    private const string LOG_PREFIX = "[AudibleBreakingGlassJars]";

    public static string SoundName = DEFAULT_SOUND;
    private static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        LoadConfig();

        Debug.Log($"{LOG_PREFIX} Initialized. Using sound: {SoundName}");

        var harmony = new Harmony("com.7d2d.audiblebreakingglassjars");
        harmony.PatchAll();
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = Path.Combine(ModPath, "Config", "config.xml");
            if (!File.Exists(configPath))
                return;

            var doc = new XmlDocument();
            doc.Load(configPath);
            var soundNode = doc.SelectSingleNode("//SoundName");

            if (soundNode == null || string.IsNullOrWhiteSpace(soundNode.InnerText))
                return;

            string configuredSound = soundNode.InnerText.Trim();

            // Security: Reject any paths - only allow simple sound names
            if (configuredSound.Contains("/") || configuredSound.Contains("\\") || configuredSound.Contains(".."))
            {
                Debug.LogWarning($"{LOG_PREFIX} Invalid sound name '{configuredSound}' - paths not allowed. Using default: {DEFAULT_SOUND}");
                return;
            }

            // Check if it's a custom sound file in our Sounds folder
            string soundsFolder = Path.Combine(ModPath, "Sounds");
            string[] supportedExtensions = { ".wav", ".ogg" };

            foreach (var ext in supportedExtensions)
            {
                string customSoundPath = Path.Combine(soundsFolder, configuredSound + ext);
                if (File.Exists(customSoundPath))
                {
                    // TODO: Custom sound loading would require registering with game's audio system
                    // For now, log that we found it but can't load custom sounds yet
                    Debug.Log($"{LOG_PREFIX} Found custom sound file: {configuredSound}{ext}");
                    Debug.Log($"{LOG_PREFIX} Note: Custom sound loading not yet implemented. Using as game sound name.");
                    break;
                }
            }

            // Use the configured sound name (either built-in game sound or custom)
            SoundName = configuredSound;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LOG_PREFIX} Failed to load config: {ex.Message}. Using default: {DEFAULT_SOUND}");
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
