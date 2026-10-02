using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TruckEnergyDisplay;

internal sealed class EnergyHud : IDisposable
{
    private GameObject? _root;
    private RectTransform? _panel;
    private TextMeshProUGUI? _text;
    private GameObject? _bar;
    private RectTransform? _fill;
    private string _lastText = "";
    internal void Hide() { if (_root) _root!.SetActive(false); }
    public void Dispose() { if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); } _root = null; _lastText = ""; }
    private static string Number(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Percent(double x, double max) => EnergyMath.Full(x, max) ? "100%" : Number(Math.Floor(x / max * 1000) / 10) + "%";

    internal void Render(EnergySnapshot s, bool visible)
    {
        if (!visible) { Hide(); return; }
        if (!_root) Create();
        _root!.SetActive(true);
        float scale = Plugin.UiScale.Value;
        float width = Screen.width * 1080f / Math.Max(1, Screen.height);
        _panel!.localScale = new Vector3(scale, scale, 1);
        _panel.anchoredPosition = new Vector2(-Mathf.Clamp(Plugin.OffsetX.Value, 0, Math.Max(0, width - 320 * scale)),
            -Mathf.Clamp(Plugin.OffsetY.Value, 0, Math.Max(0, 1080 - 246 * scale)));
        var text = new StringBuilder("<size=18><b>TRUCK ENERGY</b></size>\n");
        if (!s.HasEnergy) {
            text.Append("<size=22>Unavailable</size>\n<size=15>");
            text.Append(s.Status.StartsWith("Waiting for host", StringComparison.Ordinal) ? "Waiting for host\nHost needs this mod" : "Waiting for supported truck data");
            text.Append("</size>");
        } else {
            text.Append("<size=26>");
            text.Append(Plugin.ShowPercentage.Value ? Percent(s.Current, s.Maximum) : Number(s.Current) + " / " + Number(s.Maximum));
            text.Append("</size>\n");
            if (Plugin.ShowRawEnergy.Value && Plugin.ShowPercentage.Value)
                text.Append("<size=15>").Append(Number(s.Current)).Append(" / ").Append(Number(s.Maximum)).Append(" energy</size>\n");
            if (Plugin.ShowEnergyBar.Value) text.Append("\n");
            if (s.HasPrediction) {
                if (Plugin.ShowCrystalRequirement.Value) text.Append("Need: ").Append(s.Needed).Append(s.Needed == 1 ? " crystal\n" : " crystals\n");
                if (Plugin.ShowCheckoutPrediction.Value) {
                    text.Append("Buying: ").Append(s.Buying).Append(" -> ");
                    if (EnergyMath.Full(s.Projected, s.Maximum)) {
                        text.Append("FULL");
                        if (s.Extra > 0) text.Append(" (+").Append(s.Extra).Append(" extra)");
                    } else text.Append(Percent(s.Projected, s.Maximum));
                    text.Append('\n');
                    if (s.StillNeeded > 0) text.Append("Still needed: ").Append(s.StillNeeded).Append('\n');
                    if (!s.Affordable) text.Append("<size=15>Not enough money for checkout</size>\n");
                    text.Append("<size=13>After checkout and truck loading</size>");
                }
            } else if (Plugin.ShowCrystalRequirement.Value || Plugin.ShowCheckoutPrediction.Value) {
                text.Append("<size=16>Prediction unavailable</size>\n");
                if (Plugin.ShowCheckoutPrediction.Value) text.Append("In checkout: ").Append(s.Buying).Append('\n');
            }
        }
        string value = text.ToString();
        if (_lastText != value) { _lastText = value; _text!.text = value; }
        bool bar = s.HasEnergy && Plugin.ShowEnergyBar.Value;
        _bar!.SetActive(bar);
        if (bar) {
            ((RectTransform)_bar.transform).anchoredPosition = new Vector2(14, Plugin.ShowRawEnergy.Value && Plugin.ShowPercentage.Value ? -89 : -71);
            _fill!.anchorMax = new Vector2((float)Math.Max(0, Math.Min(1, s.Current / s.Maximum)), 1);
        }
    }

    private void Create()
    {
        _root = new GameObject("TruckEnergyDisplay.HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        _panel = panel.GetComponent<RectTransform>();
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(1, 1);
        _panel.sizeDelta = new Vector2(320, 246);
        var background = panel.GetComponent<Image>(); background.color = new Color(0.025f, 0.025f, 0.03f, 0.66f); background.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(14, 12); rect.offsetMax = new Vector2(-12, -12);
        _text = label.GetComponent<TextMeshProUGUI>();
        // Reuse a game UI font without duplicating or changing its shared material.
        foreach (var existing in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
            if (existing != _text && existing.font) { _text.font = existing.font; break; }
        if (!_text.font) _text.font = TMP_Settings.defaultFontAsset;
        _text.fontSize = 18; _text.color = new Color(0.94f, 0.94f, 0.87f);
        _text.alignment = TextAlignmentOptions.TopLeft; _text.raycastTarget = false;
        _text.enableWordWrapping = true; _text.overflowMode = TextOverflowModes.Truncate;
        _bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        _bar.transform.SetParent(panel.transform, false);
        var barRect = _bar.GetComponent<RectTransform>(); barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0, 1);
        barRect.sizeDelta = new Vector2(292, 5);
        _bar.GetComponent<Image>().color = new Color(0.8f, 0.77f, 0.3f, 0.15f); _bar.GetComponent<Image>().raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(_bar.transform, false);
        _fill = fill.GetComponent<RectTransform>(); _fill.anchorMin = Vector2.zero; _fill.anchorMax = Vector2.one;
        _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.91f, 0.83f, 0.23f); fill.GetComponent<Image>().raycastTarget = false;
    }
}
