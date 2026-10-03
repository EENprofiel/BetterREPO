using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OwnedEquipmentHUD;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("REPO.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "lucas.repo.owned-equipment-hud";
    public const string PluginName = "Owned Equipment HUD";
    public const string PluginVersion = "1.1.0";

    internal static Plugin? Instance { get; private set; }
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> ShowEquipmentList { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowOwnedCountOnShopItems { get; private set; } = null!;
    internal static ConfigEntry<int> MaxVisibleRows { get; private set; } = null!;
    internal static ConfigEntry<HudDisplayMode> DisplayMode { get; private set; } = null!;
    internal static ConfigEntry<bool> CompactLayout { get; private set; } = null!;
    internal static ConfigEntry<bool> HideZeroCountItems { get; private set; } = null!;
    internal static ConfigEntry<bool> VerboseEquipmentLogging { get; private set; } = null!;

    private Harmony? _harmony;
    private OwnedEquipmentReader? _reader;
    private OwnedEquipmentHudRenderer? _renderer;
    private FocusedShopItem? _focusedItem;
    private bool _isServiceStation;
    private float _nextRefresh;
    private float _nextFocusProbe;

    internal OwnedEquipmentReader Reader => _reader!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        ShowEquipmentList = Config.Bind(
            "Display",
            "ShowEquipmentList",
            true,
            "Show the run-owned reusable equipment list in the Service Station.");

        ShowOwnedCountOnShopItems = Config.Bind(
            "Display",
            "ShowOwnedCountOnShopItems",
            true,
            "When looking directly at a shop item, show its owned count and the count after one purchase.");

        MaxVisibleRows = Config.Bind(
            "Display",
            "MaxVisibleRows",
            12,
            new ConfigDescription(
                "Maximum number of equipment rows visible before the list becomes scrollable.",
                new AcceptableValueRange<int>(1, 50)));

        DisplayMode = Config.Bind(
            "Display",
            "DisplayMode",
            HudDisplayMode.Text,
            "How list rows are drawn: Text (names), Icons (item icons), or Both. Items without an icon always fall back to text.");

        CompactLayout = Config.Bind(
            "Display",
            "CompactLayout",
            false,
            "Use a smaller panel with tighter rows. Useful on small screens.");

        HideZeroCountItems = Config.Bind(
            "Display",
            "HideZeroCountItems",
            true,
            "Hide registered reusable equipment that the run does not currently own.");

        VerboseEquipmentLogging = Config.Bind(
            "Debug",
            "VerboseEquipmentLogging",
            false,
            "Log the authoritative count and item metadata used for every displayed entry.");

        _reader = new OwnedEquipmentReader();
        _renderer = new OwnedEquipmentHudRenderer();

        gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);

        _harmony = new Harmony(PluginGuid);
        PatchRefreshHooks();

        Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        Log.LogInfo("Ownership source: StatsManager.GetItemPurchased(Item).");
    }

    private void Update()
    {
        bool serviceStation = GameApi.IsServiceStation();
        if (serviceStation != _isServiceStation)
        {
            _isServiceStation = serviceStation;
            _reader?.MarkDirty();
            _focusedItem = null;
            _renderer?.ResetScroll();
        }

        if (!serviceStation)
        {
            return;
        }

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 0.5f;
            _reader?.RefreshIfNeeded();
        }

        if (ShowOwnedCountOnShopItems.Value && Time.unscaledTime >= _nextFocusProbe)
        {
            _nextFocusProbe = Time.unscaledTime + 0.15f;
            _focusedItem = FindFocusedShopItem();
        }
    }

    private void OnGUI()
    {
        if (!_isServiceStation || _reader == null || _renderer == null)
        {
            return;
        }

        bool showList = ShowEquipmentList.Value;
        FocusedShopItem? focusedItem = ShowOwnedCountOnShopItems.Value ? _focusedItem : null;
        if (!showList && focusedItem == null)
        {
            return;
        }

        _renderer.Draw(
            _reader.Snapshot,
            MaxVisibleRows.Value,
            focusedItem,
            showList,
            DisplayMode.Value,
            CompactLayout.Value);
    }

    private FocusedShopItem? FindFocusedShopItem()
    {
        Camera? camera = Camera.main;
        Type? attributesType = GameApi.FindType("ItemAttributes");
        if (camera == null || attributesType == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            return null;
        }

        Component? attributes = hit.collider.GetComponentInParent(attributesType);
        object? item = GameApi.GetMemberValue(attributes, "item");
        if (item == null)
        {
            return null;
        }

        if (!Reader.IsReusableEquipmentItem(item))
        {
            return null;
        }

        if (!Reader.TryGetOwnedCount(item, out int ownedCount))
        {
            return null;
        }

        string displayName = Reader.GetDisplayName(item);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return new FocusedShopItem(displayName, ownedCount, Reader.GetPurchaseLimit(item));
    }

    private void PatchRefreshHooks()
    {
        if (_harmony == null)
        {
            return;
        }

        MethodInfo? postfixMethod = typeof(Plugin).GetMethod(
            nameof(MarkReaderDirtyPostfix),
            BindingFlags.Static | BindingFlags.NonPublic);
        if (postfixMethod == null)
        {
            return;
        }

        HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
        PatchMethods(_harmony, "StatsManager", new[]
        {
            "ItemPurchase",
            "SetItemPurchase",
            "ReceiveSyncData",
            "LoadGame",
            "LoadItemsFromFolder",
            "ResetAllStats"
        }, postfix);

        PatchMethods(_harmony, "ShopManager", new[] { "ShopInitialize" }, postfix);
        PatchMethods(_harmony, "PunManager", new[] { "TruckPopulateItemVolumes" }, postfix);
    }

    private static void PatchMethods(
        Harmony harmony,
        string typeName,
        IEnumerable<string> methodNames,
        HarmonyMethod postfix)
    {
        Type? type = GameApi.FindType(typeName);
        if (type == null)
        {
            Log.LogDebug($"Could not find optional refresh type {typeName}.");
            return;
        }

        foreach (string methodName in methodNames)
        {
            MethodInfo[] methods = type
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == methodName)
                .ToArray();

            if (methods.Length == 0)
            {
                Log.LogDebug($"Could not find optional refresh method {typeName}.{methodName}.");
                continue;
            }

            foreach (MethodInfo method in methods)
            {
                try
                {
                    harmony.Patch(method, postfix: postfix);
                }
                catch (Exception exception)
                {
                    Log.LogWarning($"Could not patch {typeName}.{methodName}: {exception.Message}");
                }
            }
        }
    }

    private static void MarkReaderDirtyPostfix()
    {
        Instance?._reader?.MarkDirty();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _renderer?.Dispose();
        Instance = null;
    }
}

internal sealed class FocusedShopItem
{
    internal FocusedShopItem(string displayName, int ownedCount, int limit)
    {
        DisplayName = displayName;
        OwnedCount = ownedCount;
        Limit = limit;
    }

    internal string DisplayName { get; }
    internal int OwnedCount { get; }
    internal int Limit { get; }
}
