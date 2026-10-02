using System.Collections.Generic;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VanillaOrModded.Maps;

namespace VanillaOrModded.UI;

internal sealed class VoteRow
{
    private static readonly Color Green = new(0.25f, 0.92f, 0.38f, 1f);
    private static readonly Color BorderGreen = new(0.25f, 0.92f, 0.38f, 0.9f);

    private readonly REPOButton _button;
    private readonly GameObject _selectionBorder;
    private readonly List<GameObject> _blocks = new();

    internal VoteRow(REPOButton button, MapCategory category)
    {
        _button = button;
        Category = category;

        _button.labelTMP.text = category.ToString().ToUpperInvariant();
        _button.labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRect = _button.labelTMP.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 0f);
        textRect.offsetMax = new Vector2(-116f, 0f);

        _selectionBorder = CreateBorder(_button.transform);
        _selectionBorder.SetActive(false);
    }

    internal MapCategory Category { get; }

    internal void SetSelected(bool selected)
    {
        _selectionBorder.SetActive(selected);
        _button.labelTMP.color = selected ? Green : Color.white;
    }

    internal void SetCount(int count)
    {
        int safeCount = Mathf.Clamp(count, 0, 16);
        while (_blocks.Count < safeCount)
        {
            _blocks.Add(CreateBlock(_button.transform, _blocks.Count));
        }

        for (int i = 0; i < _blocks.Count; i++)
        {
            _blocks[i].SetActive(i < safeCount);
        }
    }

    private static GameObject CreateBlock(Transform parent, int index)
    {
        GameObject block = new("VoteBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        block.transform.SetParent(parent, false);
        RectTransform rect = block.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(15f, 6f);
        rect.anchoredPosition = new Vector2(-14f - (index * 19f), 0f);
        Image image = block.GetComponent<Image>();
        image.color = Green;
        image.raycastTarget = false;
        return block;
    }

    private static GameObject CreateBorder(Transform parent)
    {
        GameObject border = new("SelectedVoteBorder", typeof(RectTransform));
        border.transform.SetParent(parent, false);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);

        CreateBorderEdge(border.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 0f));
        CreateBorderEdge(border.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 2f));
        CreateBorderEdge(border.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(2f, 0f));
        CreateBorderEdge(border.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), new Vector2(0f, 0f));
        return border;
    }

    private static void CreateBorderEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject edge = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edge.transform.SetParent(parent, false);
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = edge.GetComponent<Image>();
        image.color = BorderGreen;
        image.raycastTarget = false;
    }
}
