using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OwnedEquipmentHUD;

internal static class GameApi
{
    private static readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);
    private static MethodInfo? _runIsShop;
    private static bool _runIsShopLookedUp;

    internal static Type? FindType(string shortOrFullName)
    {
        if (TypeCache.TryGetValue(shortOrFullName, out Type? cached))
        {
            return cached;
        }

        Type? result = null;
        try
        {
            result = Type.GetType(shortOrFullName, throwOnError: false);
        }
        catch
        {
            // Continue with the loaded-assembly search below.
        }

        if (result == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    result = assembly.GetType(shortOrFullName, throwOnError: false);
                    if (result == null)
                    {
                        result = assembly.GetTypes().FirstOrDefault(type =>
                            type.Name.Equals(shortOrFullName, StringComparison.Ordinal) ||
                            type.FullName?.Equals(shortOrFullName, StringComparison.Ordinal) == true);
                    }
                }
                catch (ReflectionTypeLoadException exception)
                {
                    result = exception.Types.FirstOrDefault(type =>
                        type != null &&
                        (type.Name.Equals(shortOrFullName, StringComparison.Ordinal) ||
                         type.FullName?.Equals(shortOrFullName, StringComparison.Ordinal) == true));
                }
                catch
                {
                    // A third-party assembly can fail type enumeration. Ignore it.
                }

                if (result != null)
                {
                    break;
                }
            }
        }

        TypeCache[shortOrFullName] = result;
        return result;
    }

    internal static object? GetMemberValue(object? target, string name)
    {
        if (target == null || IsUnityNull(target))
        {
            return null;
        }

        return GetMemberValue(target.GetType(), target, name, BindingFlags.Instance);
    }

    internal static object? GetStaticMemberValue(Type? type, string name)
    {
        return type == null ? null : GetMemberValue(type, null, name, BindingFlags.Static);
    }

    internal static string GetStringMember(object? target, string name)
    {
        return GetMemberValue(target, name)?.ToString() ?? string.Empty;
    }

    internal static bool GetBoolMember(object? target, string name)
    {
        object? value = GetMemberValue(target, name);
        return value is bool boolean && boolean;
    }

    internal static bool IsUnityNull(object? value)
    {
        if (value is UnityEngine.Object unityObject)
        {
            return unityObject == null;
        }

        return value == null;
    }

    internal static bool IsServiceStation()
    {
        if (!_runIsShopLookedUp)
        {
            _runIsShopLookedUp = true;
            Type? semiFunc = FindType("SemiFunc");
            _runIsShop = semiFunc?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "RunIsShop" && method.GetParameters().Length == 0);
        }

        if (_runIsShop != null)
        {
            try
            {
                return Convert.ToBoolean(_runIsShop.Invoke(null, null));
            }
            catch (Exception exception)
            {
                Plugin.Log.LogDebug($"SemiFunc.RunIsShop failed: {exception.Message}");
            }
        }

        return FallbackServiceStationCheck();
    }

    private static bool FallbackServiceStationCheck()
    {
        Type? runManagerType = FindType("RunManager");
        object? runManager = GetStaticMemberValue(runManagerType, "instance");
        object? currentLevel = GetMemberValue(runManager, "levelCurrent");
        string currentName = currentLevel?.ToString() ?? string.Empty;

        if (currentName.Length == 0)
        {
            currentName = GetStringMember(runManager, "levelCurrentName");
        }

        return currentName.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0 ||
               currentName.IndexOf("service", StringComparison.OrdinalIgnoreCase) >= 0 ||
               currentName.IndexOf("station", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static object? GetMemberValue(
        Type type,
        object? target,
        string name,
        BindingFlags scope)
    {
        const BindingFlags Common = BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            FieldInfo? field = type.GetField(name, scope | Common);
            if (field != null)
            {
                return field.GetValue(target);
            }

            PropertyInfo? property = type.GetProperty(name, scope | Common);
            if (property?.GetIndexParameters().Length == 0)
            {
                return property.GetValue(target, null);
            }

            MemberInfo? insensitiveMember = type
                .GetMembers(scope | Common)
                .FirstOrDefault(member =>
                    member.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            return insensitiveMember switch
            {
                FieldInfo insensitiveField => insensitiveField.GetValue(target),
                PropertyInfo insensitiveProperty when insensitiveProperty.GetIndexParameters().Length == 0 =>
                    insensitiveProperty.GetValue(target, null),
                _ => null
            };
        }
        catch (Exception exception)
        {
            Plugin.Log.LogDebug($"Could not read {type.Name}.{name}: {exception.Message}");
            return null;
        }
    }
}
