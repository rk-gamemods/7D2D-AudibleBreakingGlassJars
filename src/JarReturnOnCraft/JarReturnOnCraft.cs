using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEngine;

/// <summary>
/// JarReturnOnCraft - Addon mod for AudibleBreakingGlassJars
/// 
/// When crafting recipes that use jar contents (bottled water, etc.), the glass jar
/// is immediately returned. If the job is cancelled, the jar content is NOT returned
/// since you already received the empty jar.
/// 
/// EXCEPTION: If the recipe OUTPUT is also a jar-based item (water→tea), vanilla behavior applies.
/// 
/// Detection modes:
/// 1. DYNAMIC: Checks item's ItemActionEat.CreateItem property at runtime
/// 2. CONFIG: Additional items can be specified in Config/JarContents.xml
/// </summary>
public class JarReturnOnCraft : IModApi
{
    public static Mod ModInstance;
    public static Harmony HarmonyInstance;
    private static string ModPath;
    
    /// <summary>
    /// Manual overrides from config file.
    /// Key = item name, Value = jar item name (e.g., "drinkJarEmpty")
    /// </summary>
    public static Dictionary<string, string> ConfiguredJarContents { get; private set; }
    
    /// <summary>
    /// Cache for dynamic lookups to avoid repeated reflection
    /// </summary>
    private static Dictionary<string, string> DynamicJarCache { get; set; }

    /// <summary>
    /// When true, logs jar returns to console. Reduces log noise when false.
    /// </summary>
    public static bool DebugMode = false;

    public void InitMod(Mod _modInstance)
    {
        ModInstance = _modInstance;
        ModPath = _modInstance.Path;
        
        // Initialize caches
        ConfiguredJarContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DynamicJarCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Load config file (optional manual overrides)
        LoadConfig();
        
        // Apply Harmony patches
        HarmonyInstance = new Harmony("com.7d2d.jarreturnoncraft");
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        
        Debug.Log($"[JarReturnOnCraft] Initialized - Dynamic detection enabled, {ConfiguredJarContents.Count} manual overrides loaded");
    }
    
    private void LoadConfig()
    {
        try
        {
            string configPath = Path.Combine(ModPath, "Config", "JarContents.xml");
            if (!File.Exists(configPath))
            {
                Debug.Log("[JarReturnOnCraft] No Config/JarContents.xml found - using dynamic detection only");
                return;
            }
            
            var doc = new XmlDocument();
            doc.Load(configPath);
            
            var items = doc.SelectNodes("//item");
            if (items == null) return;
            
            foreach (XmlNode item in items)
            {
                string name = item.Attributes?["name"]?.Value;
                string jar = item.Attributes?["jar"]?.Value ?? "drinkJarEmpty";
                
                if (!string.IsNullOrEmpty(name))
                {
                    ConfiguredJarContents[name] = jar;
                    Debug.Log($"[JarReturnOnCraft] Config: {name} -> {jar}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[JarReturnOnCraft] Failed to load config: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Check if an item is a jar content that should return an empty jar.
    /// Uses dynamic detection first, then falls back to config.
    /// </summary>
    public static bool IsJarContent(string itemName, out string jarItemName)
    {
        jarItemName = null;
        if (string.IsNullOrEmpty(itemName))
            return false;
        
        // Check cache first
        if (DynamicJarCache.TryGetValue(itemName, out jarItemName))
            return !string.IsNullOrEmpty(jarItemName);
        
        // Check manual config overrides
        if (ConfiguredJarContents.TryGetValue(itemName, out jarItemName))
        {
            DynamicJarCache[itemName] = jarItemName;
            return true;
        }
        
        // Dynamic detection: Check if item has ItemActionEat with Create_item property
        jarItemName = GetJarFromItemAction(itemName);
        DynamicJarCache[itemName] = jarItemName; // Cache result (even if null)
        
        return !string.IsNullOrEmpty(jarItemName);
    }
    
    /// <summary>
    /// Dynamically detect if an item returns a jar when consumed.
    /// Checks ItemActionEat.CreateItem property.
    /// </summary>
    private static string GetJarFromItemAction(string itemName)
    {
        try
        {
            ItemClass itemClass = ItemClass.GetItemClass(itemName);
            if (itemClass == null || itemClass.Actions == null)
                return null;
            
            // Check first action (primary use action)
            foreach (var action in itemClass.Actions)
            {
                if (action is ItemActionEat eatAction)
                {
                    // Only consider items that use jar refund system
                    if (eatAction.UseJarRefund && !string.IsNullOrEmpty(eatAction.CreateItem))
                    {
                        return eatAction.CreateItem;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[JarReturnOnCraft] Error checking item {itemName}: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if a recipe's output is a jar-based item.
    /// If true, the jar stays with the liquid (e.g., dirty water → clean water).
    /// If false, the jar is freed (e.g., water + meat → cooked meat).
    /// </summary>
    public static bool RecipeOutputIsJarBased(Recipe recipe)
    {
        if (recipe == null)
            return false;
            
        // Get the output item name
        ItemClass outputItem = ItemClass.GetForId(recipe.itemValueType);
        if (outputItem == null)
            return false;
            
        string outputName = outputItem.GetItemName();
        
        // Check if output is a jar content item
        return IsJarContent(outputName, out _);
    }
}
