using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OwnedEquipmentHUD;

internal sealed class OwnedEquipmentReader
{
    // These are the non-equipment values in R.E.P.O.'s itemType enum. The
    // comparison is made against the enum's runtime name, so no game assembly
    // or vanilla item-name catalog is bundled with this plugin.
    private static readonly HashSet<string> NonReusableItemTypes = new(StringComparer.Ordinal)
    {
        "itemupgrade",
        "playerupgrade",
        "powercrystal",
        "grenade",
        "healthpack",
        "mine"
    };

    private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.Ordinal);
    private bool _dirty = true;
    private bool _missingApiLogged;
    private Type? _statsManagerType;
    private object? _statsManagerInstance;
    private MethodInfo? _getItemPurchased;
    private MemberInfo? _itemDictionaryMember;
    private OwnedEquipmentSnapshot _snapshot = OwnedEquipmentSnapshot.Unavailable;

    internal OwnedEquipmentSnapshot Snapshot => _snapshot;

    internal void MarkDirty()
    {
        _dirty = true;
    }

    internal void RefreshIfNeeded()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        Refresh();
    }

    internal bool TryGetOwnedCount(object item, out int count)
    {
        count = 0;
        EnsureStatsApi();
        if (_statsManagerInstance == null || _getItemPurchased == null || item == null)
        {
            return false;
        }

        try
        {
            object? argument = BuildGetItemPurchasedArgument(_getItemPurchased, item);
            if (argument == null && _getItemPurchased.GetParameters()[0].ParameterType != typeof(string))
            {
                return false;
            }

            object? result = _getItemPurchased.Invoke(_statsManagerInstance, new[] { argument });
            if (result == null)
            {
                return false;
            }

            count = Convert.ToInt32(result);
            return count >= 0;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogDebug($"GetItemPurchased failed: {exception.Message}");
            return false;
        }
    }

    internal string GetDisplayName(object item)
    {
        string displayName = GameApi.GetStringMember(item, "itemName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return GameApi.GetStringMember(item, "name").Trim();
    }

    // Returns the maximum number of purchases the game allows for this item,
    // or 0 when the item has no limit (or the game does not expose one).
    internal int GetPurchaseLimit(object item)
    {
        if (item == null)
        {
            return 0;
        }

        object? maxPurchase = GameApi.GetMemberValue(item, "maxPurchase");
        object? amount = GameApi.GetMemberValue(item, "maxPurchaseAmount");
        try
        {
            return PurchaseLimit.Resolve(
                maxPurchase as bool?,
                amount == null ? null : Convert.ToInt32(amount));
        }
        catch
        {
            return 0;
        }
    }

    // R.E.P.O.'s Item has no icon field. The icon is ItemAttributes.icon on the
    // item's prefab, with ItemAttributes.hasIcon telling whether it is set.
    // Results are cached per item, including "no icon", so the prefab is not
    // searched on every refresh.
    internal Sprite? GetIcon(object item)
    {
        string name = GameApi.GetStringMember(item, "itemName");
        if (name.Length > 0 && _iconCache.TryGetValue(name, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = ReadIcon(item) ?? FindPrefabIcon(item);
        if (name.Length > 0 && sprite != null)
        {
            _iconCache[name] = sprite;
        }

        return sprite;
    }

    // Called when the player looks at a shop item: its ItemAttributes is live,
    // so the icon is read directly even if the prefab lookup failed.
    internal void LearnIcon(object item, object? attributes)
    {
        string name = GameApi.GetStringMember(item, "itemName");
        if (name.Length == 0 || _iconCache.ContainsKey(name))
        {
            return;
        }

        Sprite? sprite = ReadIcon(attributes);
        if (sprite != null)
        {
            _iconCache[name] = sprite;
            MarkDirty();
        }
    }

    private static Sprite? ReadIcon(object? holder)
    {
        if (holder == null || GameApi.IsUnityNull(holder))
        {
            return null;
        }

        if (holder.GetType().GetField("hasIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null &&
            !GameApi.GetBoolMember(holder, "hasIcon"))
        {
            return null;
        }

        object? icon = GameApi.GetMemberValue(holder, "icon");
        return icon is Sprite sprite && sprite != null && sprite.texture != null ? sprite : null;
    }

    private static Sprite? FindPrefabIcon(object item)
    {
        try
        {
            object? prefabRef = GameApi.GetMemberValue(item, "prefab");
            object? prefab = GameApi.GetMemberValue(prefabRef, "Prefab") ?? prefabRef;
            Type? attributesType = GameApi.FindType("ItemAttributes");
            if (prefab is GameObject gameObject && gameObject != null && attributesType != null)
            {
                return ReadIcon(gameObject.GetComponent(attributesType));
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogDebug($"Could not read the prefab icon: {exception.Message}");
        }

        return null;
    }

    internal bool IsReusableEquipmentItem(object item)
    {
        return item != null && IsReusableEquipment(GetCategory(item));
    }

    private void Refresh()
    {
        EnsureStatsApi();
        if (_statsManagerInstance == null || _getItemPurchased == null)
        {
            _snapshot = OwnedEquipmentSnapshot.Unavailable;
            return;
        }

        List<OwnedEquipmentEntry> entries = new();
        foreach ((string key, object item) in EnumerateItemDefinitionsWithKeys())
        {
            if (GameApi.GetBoolMember(item, "disabled"))
            {
                continue;
            }

            string category = GetCategory(item);
            string displayName = GetDisplayName(item);
            if (!IsReusableEquipment(category) || string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (!TryGetOwnedCount(item, out int count))
            {
                continue;
            }

            if (Plugin.HideZeroCountItems.Value && count <= 0)
            {
                continue;
            }

            string identity = string.IsNullOrWhiteSpace(key)
                ? GameApi.GetStringMember(item, "name")
                : key;

            int limit = GetPurchaseLimit(item);
            Sprite? icon = GetIcon(item);
            entries.Add(new OwnedEquipmentEntry(identity, displayName, category, count, limit, icon));

            if (Plugin.VerboseEquipmentLogging.Value)
            {
                int remainingSpawnEntries = GetRemainingSpawnEntries(displayName);
                Plugin.Log.LogInfo(
                    $"[OwnedEquipmentHUD] {displayName}: " +
                    $"Item key = '{identity}', category = '{category}', " +
                    $"StatsManager.GetItemPurchased = {count}, " +
                    $"purchase limit = {(limit > 0 ? limit.ToString() : "none")}, " +
                    $"icon = {(icon != null ? icon.name : "none")}, " +
                    $"ItemManager.purchasedItems remaining spawn entries = {remainingSpawnEntries}, " +
                    $"displayed total = {count}.");
            }
        }

        entries = entries
            .GroupBy(entry => entry.Identity, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Identity, StringComparer.Ordinal)
            .ToList();

        _snapshot = new OwnedEquipmentSnapshot(true, DateTime.UtcNow, entries);
    }

    private void EnsureStatsApi()
    {
        Type? statsType = GameApi.FindType("StatsManager");
        if (statsType == null)
        {
            _statsManagerInstance = null;
            return;
        }

        if (_statsManagerType != statsType)
        {
            _statsManagerType = statsType;
            _statsManagerInstance = GameApi.GetStaticMemberValue(statsType, "instance");
            _getItemPurchased = FindGetItemPurchased(statsType);
            _itemDictionaryMember = FindItemDictionaryMember(statsType);
        }
        else if (_statsManagerInstance == null)
        {
            _statsManagerInstance = GameApi.GetStaticMemberValue(statsType, "instance");
        }

        if ((_getItemPurchased == null || _itemDictionaryMember == null) && !_missingApiLogged)
        {
            _missingApiLogged = true;
            Plugin.Log.LogWarning(
                "R.E.P.O.'s StatsManager ownership API is not available yet. " +
                "The HUD will remain hidden until StatsManager.GetItemPurchased and itemDictionary are ready.");
        }
    }

    private static MethodInfo? FindGetItemPurchased(Type statsType)
    {
        return statsType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "GetItemPurchased")
            .Where(method => method.GetParameters().Length == 1)
            .OrderByDescending(method => method.GetParameters()[0].ParameterType.Name == "Item")
            .FirstOrDefault();
    }

    private static MemberInfo? FindItemDictionaryMember(Type statsType)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return statsType
            .GetMembers(Flags)
            .Where(member => member.Name.Equals("itemDictionary", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(member => member is FieldInfo || member is PropertyInfo);
    }

    private object? BuildGetItemPurchasedArgument(MethodInfo method, object item)
    {
        Type parameterType = method.GetParameters()[0].ParameterType;
        if (parameterType.IsInstanceOfType(item) || parameterType == typeof(object))
        {
            return item;
        }

        if (parameterType == typeof(string))
        {
            return GetDisplayName(item);
        }

        return null;
    }

    private IEnumerable<(string key, object item)> EnumerateItemDefinitionsWithKeys()
    {
        if (_statsManagerInstance == null || _itemDictionaryMember == null)
        {
            yield break;
        }

        object? dictionary = ReadMember(_itemDictionaryMember, _statsManagerInstance);
        if (dictionary is IDictionary nonGenericDictionary)
        {
            foreach (DictionaryEntry entry in nonGenericDictionary)
            {
                if (entry.Value != null && !GameApi.IsUnityNull(entry.Value))
                {
                    yield return (entry.Key?.ToString() ?? string.Empty, entry.Value);
                }
            }

            yield break;
        }

        if (dictionary is IEnumerable enumerable)
        {
            foreach (object? pair in enumerable)
            {
                if (pair == null)
                {
                    continue;
                }

                object? value = GameApi.GetMemberValue(pair, "Value");
                if (value != null && !GameApi.IsUnityNull(value))
                {
                    yield return (GameApi.GetStringMember(pair, "Key"), value);
                }
            }
        }
    }

    private static object? ReadMember(MemberInfo member, object target)
    {
        try
        {
            return member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.GetValue(target, null),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetCategory(object item)
    {
        return GameApi.GetStringMember(item, "itemType");
    }

    private static bool IsReusableEquipment(string category)
    {
        string normalizedCategory = Normalize(category);
        if (normalizedCategory.Length == 0 || NonReusableItemTypes.Contains(normalizedCategory))
        {
            return false;
        }

        // Unknown/new categories are intentionally allowed. A modded item that
        // enters the normal Item/shop system should be visible without a list
        // of hardcoded item names or a hardcoded mod integration.
        return true;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(character => !char.IsWhiteSpace(character) && character != '_' && character != '-')
            .ToArray())
            .ToLowerInvariant();
    }

    private static int GetRemainingSpawnEntries(string displayName)
    {
        Type? itemManagerType = GameApi.FindType("ItemManager");
        object? itemManager = GameApi.GetStaticMemberValue(itemManagerType, "instance");
        object? purchasedItems = GameApi.GetMemberValue(itemManager, "purchasedItems");
        if (purchasedItems is not IEnumerable enumerable)
        {
            return 0;
        }

        int count = 0;
        foreach (object? item in enumerable)
        {
            if (item != null && GameApi.GetStringMember(item, "itemName").Equals(displayName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}

internal sealed class OwnedEquipmentEntry
{
    internal OwnedEquipmentEntry(
        string identity,
        string displayName,
        string category,
        int count,
        int limit,
        Sprite? icon)
    {
        Identity = identity;
        DisplayName = displayName;
        Category = category;
        Count = count;
        Limit = limit;
        Icon = icon;
    }

    internal string Identity { get; }
    internal string DisplayName { get; }
    internal string Category { get; }
    internal int Count { get; }

    // 0 means the game exposes no purchase limit for this item.
    internal int Limit { get; }
    internal Sprite? Icon { get; }
}

internal sealed class OwnedEquipmentSnapshot
{
    internal static OwnedEquipmentSnapshot Unavailable { get; } =
        new OwnedEquipmentSnapshot(false, DateTime.MinValue, Array.Empty<OwnedEquipmentEntry>());

    internal OwnedEquipmentSnapshot(bool isAvailable, DateTime refreshedAtUtc, IReadOnlyList<OwnedEquipmentEntry> entries)
    {
        IsAvailable = isAvailable;
        RefreshedAtUtc = refreshedAtUtc;
        Entries = entries;
    }

    internal bool IsAvailable { get; }
    internal DateTime RefreshedAtUtc { get; }
    internal IReadOnlyList<OwnedEquipmentEntry> Entries { get; }
}
