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
        bool showList)
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.35f);
        EnsureStyles(scale);

        int rowLimit = Mathf.Clamp(maxVisibleRows, 1, 50);
        int entryCount = showList ? snapshot.Entries.Count : 0;
        int visibleRows = showList
            ? Math.Max(1, Math.Min(rowLimit, Math.Max(entryCount, 1)))
            : 0;
        bool scrollable = showList && entryCount > visibleRows;
        bool hasContext = focusedItem != null;

        float width = 370f * scale;
        float padding = 14f * scale;
        float titleHeight = showList ? 28f * scale : 0f;
        float rowHeight = 23f * scale;
        float footerHeight = scrollable ? 20f * scale : 0f;
        float contextHeight = hasContext ? 38f * scale : 0f;
        float contentHeight = showList ? visibleRows * rowHeight : 0f;
        float height = padding + titleHeight + contentHeight + footerHeight + contextHeight + padding;

        float rightMargin = 22f * scale;
        float topMargin = 86f * scale;
        float x = Mathf.Max(8f * scale, Screen.width - width - rightMargin);
        float y = Mathf.Max(8f * scale, topMargin);
        Rect panelRect = new Rect(x, y, width, height);

        GUI.Box(panelRect, GUIContent.none, _panelStyle!);
        DrawBorder(panelRect, scale);

        if (showList)
        {
            Rect titleRect = new Rect(x + padding, y + 5f * scale, width - padding * 2f, titleHeight);
            GUI.Label(titleRect, "OWNED EQUIPMENT", _titleStyle!);

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
                GUI.Label(viewport, "No reusable equipment owned", _mutedStyle!);
            }
            else
            {
                float contentWidth = viewport.width - (scrollable ? 18f * scale : 0f);
                Rect content = new Rect(0f, 0f, contentWidth, entryCount * rowHeight);
                _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content, false, scrollable);

                for (int index = 0; index < entryCount; index++)
                {
                    OwnedEquipmentEntry entry = snapshot.Entries[index];
                    float rowY = index * rowHeight;
                    Rect nameRect = new Rect(0f, rowY, contentWidth - 46f * scale, rowHeight);
                    Rect countRect = new Rect(contentWidth - 44f * scale, rowY, 44f * scale, rowHeight);
                    GUI.Label(nameRect, TrimName(entry.DisplayName, 31), _rowNameStyle!);
                    GUI.Label(countRect, $"x{entry.Count}", _rowCountStyle!);
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
            string context =
                $"{TrimName(focusedItem.DisplayName, 29)}  |  Owned: {focusedItem.OwnedCount}\n" +
                $"After purchase: {focusedItem.OwnedCount + 1}";
            GUI.Label(contextRect, context, _contextStyle!);
        }
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
