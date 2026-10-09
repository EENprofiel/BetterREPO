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
        float scale = HudLayout.Scale(Screen.height);
        float density = compact ? 0.8f : 1f;
        EnsureStyles(scale * density);

        bool hasContext = focusedItem != null;
        bool wantsIcons = displayMode != HudDisplayMode.Text;
        int entryCount = showList ? snapshot.Entries.Count : 0;

        PanelLayout layout = HudLayout.Compute(
            Screen.width,
            Screen.height,
            entryCount,
            maxVisibleRows,
            showList,
            hasContext,
            displayMode,
            compact);

        float padding = 14f * scale * density;
        float titleHeight = showList ? 28f * scale * density : 0f;
        float iconSize = 20f * scale * density;
        float rowHeight = (wantsIcons ? 26f : 23f) * scale * density;
        float footerHeight = 20f * scale * density;
        float contextHeight = hasContext ? 38f * scale * density : 0f;
        float countWidth = 64f * scale * density;
        float x = layout.X;
        float y = layout.Y;
        float width = layout.Width;
        float height = layout.Height;
        int visibleRows = layout.VisibleRows;
        bool scrollable = layout.Scrollable;
        float contentHeight = visibleRows * rowHeight;

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
                        displayMode,
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
            string owned = PurchaseLimit.FormatOwned(focusedItem.OwnedCount, focusedItem.Limit);
            string afterPurchase = PurchaseLimit.FormatAfterPurchase(focusedItem.OwnedCount, focusedItem.Limit);
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
        HudDisplayMode displayMode,
        bool compact)
    {
        float gap = 4f;
        Rect countRect = new Rect(contentWidth - countWidth, rowY, countWidth, rowHeight);
        float left = 0f;
        bool hasIcon = HudLayout.ShowsIcon(displayMode, entry.Icon != null);

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
        if (HudLayout.ShowsName(displayMode, entry.Icon != null))
        {
            Rect nameRect = new Rect(left, rowY, countRect.x - left - gap, rowHeight);
            GUI.Label(nameRect, TrimName(entry.DisplayName, compact ? 22 : 31), _rowNameStyle!);
        }

        string count = PurchaseLimit.FormatCount(entry.Count, entry.Limit);
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
