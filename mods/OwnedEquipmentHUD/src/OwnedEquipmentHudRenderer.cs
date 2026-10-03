using System;
using UnityEngine;

namespace OwnedEquipmentHUD;

internal sealed class OwnedEquipmentHudRenderer : IDisposable
{
    private readonly Color _panelColor = new Color(0.025f, 0.022f, 0.018f, 0.88f);
    private readonly Color _borderColor = new Color(0.83f, 0.68f, 0.28f, 0.78f);
    private readonly Color _titleColor = new Color(1f, 0.86f, 0.44f, 1f);
    private readonly Color _textColor = new Color(0.94f, 0.92f, 0.86f, 1f);
    private readonly Color _mutedColor = new Color(0.68f, 0.66f, 0.61f, 1f);

    private GUIStyle? _panelStyle;
    private GUIStyle? _titleStyle;
    private GUIStyle? _rowNameStyle;
    private GUIStyle? _rowCountStyle;
    private GUIStyle? _mutedStyle;
    private GUIStyle? _contextStyle;
    private Texture2D? _panelTexture;
    private Texture2D? _borderTexture;
    private Vector2 _scrollPosition;
    private float _lastScale = -1f;

    internal void ResetScroll()
    {
        _scrollPosition = Vector2.zero;
    }

    internal void Draw(
        OwnedEquipmentSnapshot snapshot,
        int maxVisibleRows,
        FocusedShopItem? focusedItem,
        bool showList,
        HudDisplayMode displayMode,
        bool compact)
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.35f);
        float density = compact ? 0.8f : 1f;
        EnsureStyles(scale * density);

        bool hasContext = focusedItem != null;
        bool iconsOnly = displayMode == HudDisplayMode.Icons;
        bool wantsIcons = displayMode != HudDisplayMode.Text;

        float margin = 8f * scale;
        float padding = 14f * scale * density;
        float titleHeight = showList ? 28f * scale * density : 0f;
        float iconSize = 20f * scale * density;
        float rowHeight = (wantsIcons ? 26f : 23f) * scale * density;
        float footerHeight = 20f * scale * density;
        float contextHeight = hasContext ? 38f * scale * density : 0f;
        float countWidth = 64f * scale * density;

        float baseWidth = compact ? 270f : 370f;
        if (iconsOnly)
        {
            baseWidth = compact ? 190f : 250f;
        }

        // Never wider than the screen, so ultrawide and small screens both fit.
        float width = Mathf.Min(baseWidth * scale, Screen.width - margin * 2f);

        float rightMargin = 22f * scale;
        float topMargin = 86f * scale;
        float x = Mathf.Max(margin, Screen.width - width - rightMargin);
        float y = Mathf.Clamp(topMargin, margin, Mathf.Max(margin, Screen.height * 0.5f));

        // Limit the rows by config and by the space left below the panel's top edge.
        int entryCount = showList ? snapshot.Entries.Count : 0;
        int rowLimit = Mathf.Clamp(maxVisibleRows, 1, 50);
        float fixedHeight = padding * 2f + titleHeight + contextHeight;
        float availableRows = (Screen.height - margin - y - fixedHeight - footerHeight) / rowHeight;
        int screenRowLimit = Mathf.Max(1, Mathf.FloorToInt(availableRows));
        rowLimit = Math.Min(rowLimit, screenRowLimit);

        int visibleRows = showList
            ? Math.Max(1, Math.Min(rowLimit, Math.Max(entryCount, 1)))
            : 0;
        bool scrollable = showList && entryCount > visibleRows;
        float contentHeight = showList ? visibleRows * rowHeight : 0f;
        float height = fixedHeight + contentHeight + (scrollable ? footerHeight : 0f);

        Rect panelRect = new Rect(x, y, width, height);

        GUI.Box(panelRect, GUIContent.none, _panelStyle!);
        DrawBorder(panelRect, scale);

        if (showList)
        {
            Rect titleRect = new Rect(x + padding, y + 5f * scale * density, width - padding * 2f, titleHeight);
            GUI.Label(titleRect, compact ? "OWNED" : "OWNED EQUIPMENT", _titleStyle!);

            Rect viewport = new Rect(
                x + padding,
                y + padding + titleHeight,
                width - padding * 2f,
                contentHeight);

            if (!snapshot.IsAvailable)
            {
                GUI.Label(viewport, "Reading run inventory...", _mutedStyle!);
            }
            else if (entryCount == 0)
            {
                GUI.Label(viewport, compact ? "None owned" : "No reusable equipment owned", _mutedStyle!);
            }
            else
            {
                float contentWidth = viewport.width - (scrollable ? 18f * scale : 0f);
                Rect content = new Rect(0f, 0f, contentWidth, entryCount * rowHeight);
                _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content, false, scrollable);

                for (int index = 0; index < entryCount; index++)
                {
                    DrawRow(
                        snapshot.Entries[index],
                        index * rowHeight,
                        contentWidth,
                        rowHeight,
                        iconSize,
                        countWidth,
                        wantsIcons,
                        displayMode == HudDisplayMode.Both,
                        compact);
                }

                GUI.EndScrollView();
            }

            if (scrollable)
            {
                Rect footerRect = new Rect(
                    x + padding,
                    viewport.yMax + 1f * scale,
                    width - padding * 2f,
                    footerHeight);
                GUI.Label(footerRect, "Scroll for more", _mutedStyle!);
            }
        }

        if (hasContext && focusedItem != null)
        {
            Rect contextRect = new Rect(
                x + padding,
                y + height - contextHeight - padding * 0.45f,
                width - padding * 2f,
                contextHeight);
            string owned = focusedItem.Limit > 0
                ? $"{focusedItem.OwnedCount}/{focusedItem.Limit}"
                : focusedItem.OwnedCount.ToString();
            string afterPurchase = focusedItem.Limit > 0 && focusedItem.OwnedCount >= focusedItem.Limit
                ? "Limit reached"
                : $"After purchase: {focusedItem.OwnedCount + 1}";
            string context =
                $"{TrimName(focusedItem.DisplayName, compact ? 20 : 29)}  |  Owned: {owned}\n" +
                afterPurchase;
            GUI.Label(contextRect, context, _contextStyle!);
        }
    }

    private void DrawRow(
        OwnedEquipmentEntry entry,
        float rowY,
        float contentWidth,
        float rowHeight,
        float iconSize,
        float countWidth,
        bool wantsIcons,
        bool showNameWithIcon,
        bool compact)
    {
        float gap = 4f;
        Rect countRect = new Rect(contentWidth - countWidth, rowY, countWidth, rowHeight);
        float left = 0f;
        bool hasIcon = wantsIcons && entry.Icon != null;

        if (hasIcon)
        {
            Sprite sprite = entry.Icon!;
            Texture texture = sprite.texture;
            Rect source = sprite.textureRect;
            Rect uv = new Rect(
                source.x / texture.width,
                source.y / texture.height,
                source.width / texture.width,
                source.height / texture.height);
            Rect iconRect = new Rect(0f, rowY + (rowHeight - iconSize) * 0.5f, iconSize, iconSize);
            GUI.DrawTextureWithTexCoords(iconRect, texture, uv);
            left = iconSize + gap;
        }

        // Text is drawn unless the row has an icon and the mode is icons only.
        // An item with no icon therefore always falls back to its name.
        if (!hasIcon || showNameWithIcon)
        {
            Rect nameRect = new Rect(left, rowY, countRect.x - left - gap, rowHeight);
            GUI.Label(nameRect, TrimName(entry.DisplayName, compact ? 22 : 31), _rowNameStyle!);
        }

        string count = entry.Limit > 0 ? $"x{entry.Count}/{entry.Limit}" : $"x{entry.Count}";
        GUI.Label(countRect, count, _rowCountStyle!);
    }

    private void DrawBorder(Rect panelRect, float scale)
    {
        if (_borderTexture == null)
        {
            return;
        }

        float border = Mathf.Max(1f, scale);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, border), _borderTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.yMax - border, panelRect.width, border), _borderTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, border, panelRect.height), _borderTexture);
        GUI.DrawTexture(new Rect(panelRect.xMax - border, panelRect.y, border, panelRect.height), _borderTexture);
    }

    private void EnsureStyles(float scale)
    {
        if (_panelStyle != null && Math.Abs(_lastScale - scale) < 0.01f)
        {
            return;
        }

        _lastScale = scale;
        _panelTexture ??= CreateTexture(_panelColor);
        _borderTexture ??= CreateTexture(_borderColor);

        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(0, 0, 0, 0)
        };
        _panelStyle.normal.background = _panelTexture;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(16f * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false
        };
        _titleStyle.normal.textColor = _titleColor;

        _rowNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(14f * scale),
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        _rowNameStyle.normal.textColor = _textColor;

        _rowCountStyle = new GUIStyle(_rowNameStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        _rowCountStyle.normal.textColor = _titleColor;

        _mutedStyle = new GUIStyle(_rowNameStyle)
        {
            alignment = TextAnchor.MiddleLeft
        };
        _mutedStyle.normal.textColor = _mutedColor;

        _contextStyle = new GUIStyle(_mutedStyle)
        {
            fontSize = Mathf.RoundToInt(12f * scale),
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false
        };
        _contextStyle.normal.textColor = _titleColor;
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static string TrimName(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
        {
            return value;
        }

        return value.Substring(0, Math.Max(1, maxCharacters - 1)).TrimEnd() + "…";
    }

    public void Dispose()
    {
        if (_panelTexture != null)
        {
            UnityEngine.Object.Destroy(_panelTexture);
            _panelTexture = null;
        }

        if (_borderTexture != null)
        {
            UnityEngine.Object.Destroy(_borderTexture);
            _borderTexture = null;
        }
    }
}
