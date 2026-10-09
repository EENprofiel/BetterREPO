using System;

namespace OwnedEquipmentHUD;

public enum HudDisplayMode
{
    Text,
    Icons,
    Both
}

// Pure logic with no Unity or game types, so tests/ can compile it alone.
internal static class PurchaseLimit
{
    // The game's "no limit" is maxPurchase == false. Returns 0 for no limit.
    internal static int Resolve(bool? maxPurchase, int? maxPurchaseAmount)
    {
        if (maxPurchase == false)
        {
            return 0;
        }

        return maxPurchaseAmount is int amount && amount > 0 ? amount : 0;
    }

    internal static bool AtLimit(int owned, int limit)
    {
        return limit > 0 && owned >= limit;
    }

    internal static string FormatCount(int owned, int limit)
    {
        return limit > 0 ? $"x{owned}/{limit}" : $"x{owned}";
    }

    internal static string FormatOwned(int owned, int limit)
    {
        return limit > 0 ? $"{owned}/{limit}" : owned.ToString();
    }

    internal static string FormatAfterPurchase(int owned, int limit)
    {
        return AtLimit(owned, limit) ? "Limit reached" : $"After purchase: {owned + 1}";
    }
}

internal readonly struct PanelLayout
{
    internal PanelLayout(float x, float y, float width, float height, int visibleRows, bool scrollable)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        VisibleRows = visibleRows;
        Scrollable = scrollable;
    }

    internal float X { get; }
    internal float Y { get; }
    internal float Width { get; }
    internal float Height { get; }
    internal int VisibleRows { get; }
    internal bool Scrollable { get; }
}

internal static class HudLayout
{
    internal static float Scale(float screenHeight)
    {
        return Math.Max(0.75f, Math.Min(1.35f, screenHeight / 1080f));
    }

    internal static bool ShowsIcon(HudDisplayMode mode, bool hasIcon)
    {
        return mode != HudDisplayMode.Text && hasIcon;
    }

    // Text is drawn unless the row shows an icon and the mode is icons only,
    // so an item without an icon always falls back to its name.
    internal static bool ShowsName(HudDisplayMode mode, bool hasIcon)
    {
        return !ShowsIcon(mode, hasIcon) || mode == HudDisplayMode.Both;
    }

    internal static PanelLayout Compute(
        float screenWidth,
        float screenHeight,
        int entryCount,
        int maxVisibleRows,
        bool showList,
        bool hasContext,
        HudDisplayMode mode,
        bool compact)
    {
        float scale = Scale(screenHeight);
        float density = compact ? 0.8f : 1f;
        bool wantsIcons = mode != HudDisplayMode.Text;

        float margin = 8f * scale;
        float padding = 14f * scale * density;
        float titleHeight = showList ? 28f * scale * density : 0f;
        float rowHeight = (wantsIcons ? 26f : 23f) * scale * density;
        float footerHeight = 20f * scale * density;
        float contextHeight = hasContext ? 38f * scale * density : 0f;

        float baseWidth = compact ? 270f : 370f;
        if (mode == HudDisplayMode.Icons)
        {
            baseWidth = compact ? 190f : 250f;
        }

        float width = Math.Min(baseWidth * scale, Math.Max(1f, screenWidth - margin * 2f));
        float x = Math.Max(margin, screenWidth - width - 22f * scale);
        float y = Math.Max(margin, Math.Min(86f * scale, screenHeight * 0.5f));

        float fixedHeight = padding * 2f + titleHeight + contextHeight;
        float availableRows = (screenHeight - margin - y - fixedHeight - footerHeight) / rowHeight;
        int rowLimit = Math.Min(Math.Max(1, Math.Min(50, maxVisibleRows)), Math.Max(1, (int)Math.Floor(availableRows)));

        int entries = showList ? entryCount : 0;
        int visibleRows = showList ? Math.Max(1, Math.Min(rowLimit, Math.Max(entries, 1))) : 0;
        bool scrollable = showList && entries > visibleRows;
        float height = fixedHeight + visibleRows * rowHeight + (scrollable ? footerHeight : 0f);

        return new PanelLayout(x, y, width, height, visibleRows, scrollable);
    }
}
