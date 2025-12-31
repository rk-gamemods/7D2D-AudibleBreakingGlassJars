using System;
using System.Collections;
using System.IO;
using System.Xml;
using Audio;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Audible Breaking Glass Jars - Plays a sound when drinking fluids causes the glass jar to break.
/// Works with the game's jar return percentage setting.
/// </summary>
public class AudibleBreakingGlassJars : IModApi
{
    private const string DEFAULT_CUSTOM_SOUND = "glass-shatter";
    private const string FALLBACK_GAME_SOUND = "glassdestroy";
    private const string LOG_PREFIX = "[AudibleBreakingGlassJars]";

    public static string SoundName = DEFAULT_CUSTOM_SOUND;
    public static bool DebugMode = false;
    public static bool UseCustomSound = true;
    public static AudioClip CustomAudioClip = null;
    private static string ModPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        LoadConfig();

        Debug.Log($"{LOG_PREFIX} Initialized. Sound: {SoundName}, UseCustom: {UseCustomSound}");

        // Load custom audio clip if configured
        if (UseCustomSound)
        {
            GameManager.Instance.StartCoroutine(LoadCustomAudioClip());
        }

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

            // Check for debug mode
            var debugNode = doc.SelectSingleNode("//DebugMode");
            if (debugNode != null && bool.TryParse(debugNode.InnerText.Trim(), out bool debugValue))
            {
                DebugMode = debugValue;
                if (DebugMode)
                    Debug.Log($"{LOG_PREFIX} Debug mode ENABLED");
            }

            // Security: Reject any paths - only allow simple sound names
            if (configuredSound.Contains("/") || configuredSound.Contains("\\") || configuredSound.Contains(".."))
            {
                Debug.LogWarning($"{LOG_PREFIX} Invalid sound name '{configuredSound}' - paths not allowed. Using default.");
                return;
            }

            SoundName = configuredSound;

            // Check if it's a custom sound file in our Sounds folder
            string soundsFolder = Path.Combine(ModPath, "Sounds");
            string[] supportedExtensions = { ".ogg", ".wav" };

            UseCustomSound = false;
            foreach (var ext in supportedExtensions)
            {
                string customSoundPath = Path.Combine(soundsFolder, configuredSound + ext);
                if (File.Exists(customSoundPath))
                {
                    UseCustomSound = true;
                    Debug.Log($"{LOG_PREFIX} Found custom sound file: {configuredSound}{ext}");
                    break;
                }
            }

            if (!UseCustomSound)
            {
                Debug.Log($"{LOG_PREFIX} Using game sound: {configuredSound}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LOG_PREFIX} Failed to load config: {ex.Message}. Using defaults.");
        }
    }

    private static IEnumerator LoadCustomAudioClip()
    {
        string soundsFolder = Path.Combine(ModPath, "Sounds");
        string[] supportedExtensions = { ".ogg", ".wav" };
        string soundPath = null;
        AudioType audioType = AudioType.OGGVORBIS;

        foreach (var ext in supportedExtensions)
        {
            string testPath = Path.Combine(soundsFolder, SoundName + ext);
            if (File.Exists(testPath))
            {
                soundPath = testPath;
                audioType = ext == ".wav" ? AudioType.WAV : AudioType.OGGVORBIS;
                break;
            }
        }

        if (soundPath == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} Custom sound file not found: {SoundName}. Falling back to game sound.");
            UseCustomSound = false;
            SoundName = FALLBACK_GAME_SOUND;
            yield break;
        }

        // Convert to file:// URI
        string fileUri = "file:///" + soundPath.Replace("\\", "/");

        if (DebugMode)
            Debug.Log($"{LOG_PREFIX} Loading custom audio from: {fileUri}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                CustomAudioClip = DownloadHandlerAudioClip.GetContent(www);
                if (CustomAudioClip != null)
                {
                    CustomAudioClip.name = "ABGJ_" + SoundName;
                    Debug.Log($"{LOG_PREFIX} Custom audio loaded successfully: {CustomAudioClip.name} ({CustomAudioClip.length:F2}s)");
                }
                else
                {
                    Debug.LogWarning($"{LOG_PREFIX} Failed to get audio content. Falling back to game sound.");
                    UseCustomSound = false;
                    SoundName = FALLBACK_GAME_SOUND;
                }
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Failed to load audio: {www.error}. Falling back to game sound.");
                UseCustomSound = false;
                SoundName = FALLBACK_GAME_SOUND;
            }
        }
    }

    /// <summary>
    /// Plays the configured sound. Uses custom AudioClip if loaded, otherwise falls back to game sound.
    /// </summary>
    public static void PlaySound(EntityPlayerLocal player)
    {
        if (UseCustomSound && CustomAudioClip != null)
        {
            PlayCustomSound(player);
        }
        else
        {
            PlayGameSound(player);
        }
    }

    private static void PlayCustomSound(EntityPlayerLocal player)
    {
        try
        {
            if (DebugMode)
                Debug.Log($"[ABGJ-Debug] Playing custom AudioClip: {CustomAudioClip.name}");

            // Play at player's position with no spatial blending (2D sound)
            AudioSource.PlayClipAtPoint(CustomAudioClip, player.transform.position);

            if (DebugMode)
                Debug.Log("[ABGJ-Debug] Custom sound played successfully");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LOG_PREFIX} Failed to play custom sound: {ex.Message}. Trying game sound.");
            PlayGameSound(player);
        }
    }

    private static void PlayGameSound(EntityPlayerLocal player)
    {
        try
        {
            if (DebugMode)
                Debug.Log($"[ABGJ-Debug] Playing game sound: {SoundName}");

            // Use the 5-parameter overload that doesn't require a _lp (loop) variant
            Manager.PlayInsidePlayerHead(SoundName, player.entityId, 0f, false, false);

            if (DebugMode)
                Debug.Log("[ABGJ-Debug] Game sound call completed");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LOG_PREFIX} Failed to play game sound: {ex.Message}");
        }
    }
}

/// <summary>
/// Harmony patches for ItemActionEat to detect when glass jars break.
/// Patches both consume() for animated drinking and ExecuteInstantAction() for instant consumption.
///
/// IMPORTANT: The jar return happens at the end of consume(), but the inventory update
/// is slightly delayed. We must wait one frame before checking if the jar was returned.
/// </summary>
[HarmonyPatch(typeof(ItemActionEat))]
public static class ItemActionEat_Patches
{
    // ThreadStatic to handle potential multi-threaded scenarios safely
    [ThreadStatic] private static bool _checkingJar;
    [ThreadStatic] private static int _jarCountBefore;
    [ThreadStatic] private static EntityPlayerLocal _player;
    [ThreadStatic] private static string _createItemName;

    #region consume() patches - Animated drinking path

    [HarmonyPrefix]
    [HarmonyPatch("consume")]
    private static void Consume_Prefix(ItemActionEat __instance, ItemActionData _actionData)
    {
        _checkingJar = false;

        // Always log consume events in debug mode to confirm patch is working
        if (AudibleBreakingGlassJars.DebugMode)
        {
            string itemName = _actionData?.invData?.item?.Name ?? "unknown";
            Debug.Log($"[ABGJ-Debug] ========== CONSUME EVENT ==========");
            Debug.Log($"[ABGJ-Debug] Item: {itemName}");
            Debug.Log($"[ABGJ-Debug] CreateItem: {__instance.CreateItem ?? "(null)"}");
            Debug.Log($"[ABGJ-Debug] CreateItemCount: {__instance.CreateItemCount}");
            Debug.Log($"[ABGJ-Debug] UseJarRefund: {__instance.UseJarRefund}");
        }

        // Only track if this item uses jar refund (meaning jar can break)
        if (__instance.CreateItem == null || __instance.CreateItemCount <= 0 || !__instance.UseJarRefund)
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] SKIP: Not a jar-refund item (CreateItem={__instance.CreateItem != null}, Count={__instance.CreateItemCount}, UseJarRefund={__instance.UseJarRefund})");
            return;
        }

        var entity = _actionData.invData.holdingEntity;
        if (entity is EntityPlayerLocal player)
        {
            _checkingJar = true;
            _player = player;
            _createItemName = __instance.CreateItem;
            _jarCountBefore = CountItem(player, _createItemName);
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] TRACKING: '{_createItemName}' count before consumption = {_jarCountBefore}");
        }
        else
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] SKIP: Entity is not local player (type={entity?.GetType().Name ?? "null"})");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("consume")]
    private static void Consume_Postfix()
    {
        if (!_checkingJar || _player == null)
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] consume() Postfix - Not tracking (_checkingJar={_checkingJar}, _player={_player != null})");
            return;
        }

        // Capture values for the coroutine (ThreadStatic won't work across frames)
        int jarCountBefore = _jarCountBefore;
        EntityPlayerLocal player = _player;
        string createItemName = _createItemName;

        // Reset state immediately
        _checkingJar = false;
        _player = null;
        _createItemName = null;

        if (AudibleBreakingGlassJars.DebugMode)
            Debug.Log($"[ABGJ-Debug] consume() Postfix - Scheduling delayed check for '{createItemName}' (countBefore={jarCountBefore})");

        // Wait one frame for inventory to update, then check if jar was returned
        GameManager.Instance.StartCoroutine(CheckJarAfterDelay(player, jarCountBefore, createItemName));
    }

    /// <summary>
    /// Waits for inventory to update, then checks if jar was returned.
    /// If jar count didn't increase, the jar broke and we play the sound.
    /// </summary>
    private static IEnumerator CheckJarAfterDelay(EntityPlayerLocal player, int countBefore, string itemName)
    {
        float startTime = Time.time;

        if (AudibleBreakingGlassJars.DebugMode)
            Debug.Log($"[ABGJ-Debug] CheckJarAfterDelay - START: '{itemName}' countBefore={countBefore}");

        // Wait multiple frames to ensure inventory has updated
        yield return null;  // Frame 1
        yield return null;  // Frame 2
        yield return null;  // Frame 3

        if (player == null)
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log("[ABGJ-Debug] CheckJarAfterDelay - Player became null, aborting");
            yield break;
        }

        int countAfter = CountItem(player, itemName);
        float elapsed = Time.time - startTime;

        if (AudibleBreakingGlassJars.DebugMode)
            Debug.Log($"[ABGJ-Debug] CheckJarAfterDelay - CHECK '{itemName}': before={countBefore}, after={countAfter} (elapsed={elapsed:F3}s)");

        // If item count didn't increase, the jar broke
        if (countAfter <= countBefore)
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] JAR BROKE! (after <= before) Playing sound...");
            AudibleBreakingGlassJars.PlaySound(player);
        }
        else
        {
            if (AudibleBreakingGlassJars.DebugMode)
                Debug.Log($"[ABGJ-Debug] Jar returned (after > before, no sound)");
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
            _createItemName = __instance.CreateItem;
            _jarCountBefore = CountItem(player, _createItemName);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("ExecuteInstantAction")]
    private static void ExecuteInstantAction_Postfix()
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

        // Wait one frame for inventory to update
        GameManager.Instance.StartCoroutine(CheckJarAfterDelay(player, jarCountBefore, createItemName));
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
