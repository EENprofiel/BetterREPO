using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace VanillaOrModded.UI;

internal static class LabelLayout
{
    internal static void Fit(REPOLabel label, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        label.rectTransform.sizeDelta = size;
        TextMeshProUGUI text = label.labelTMP;
        if (text.rectTransform != label.rectTransform)
        {
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = label.rectTransform.pivot;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
        }

        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = 12f;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.raycastTarget = false;
    }
}
