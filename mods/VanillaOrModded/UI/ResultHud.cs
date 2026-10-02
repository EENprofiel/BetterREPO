using System.Collections;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace VanillaOrModded.UI;

internal static class ResultHud
{
    private static GameObject? _message;
    private static Coroutine? _destroyCoroutine;

    internal static void Show(string message, float seconds)
    {
        Clear();
        Transform? hud = GameObject.Find("Game Hud")?.transform;
        if (hud == null)
        {
            Plugin.Logger.LogMessage(message.Replace('\n', ' '));
            return;
        }

        REPOLabel label = MenuAPI.CreateREPOLabel(message, hud);
        _message = label.gameObject;
        label.rectTransform.anchorMin = new Vector2(0.2f, 0.68f);
        label.rectTransform.anchorMax = new Vector2(0.8f, 0.68f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(0f, 70f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        LabelLayout.Fit(label, new Vector2(0f, 70f), 24f, TextAlignmentOptions.Center);
        label.labelTMP.enableWordWrapping = true;
        _destroyCoroutine = Plugin.Instance.StartCoroutine(DestroyAfter(label.gameObject, seconds));
    }

    internal static void Clear()
    {
        if (_destroyCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_destroyCoroutine);
        }

        _destroyCoroutine = null;
        if (_message != null)
        {
            Object.Destroy(_message);
        }
        _message = null;
    }

    private static IEnumerator DestroyAfter(GameObject target, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (target != null)
        {
            Object.Destroy(target);
        }
        if (_message == target)
        {
            _message = null;
            _destroyCoroutine = null;
        }
    }
}
