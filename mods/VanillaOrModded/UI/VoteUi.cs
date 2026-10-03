using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using VanillaOrModded.Maps;
using VanillaOrModded.Networking;

namespace VanillaOrModded.UI;

internal static class VoteUi
{
    private static readonly Dictionary<MapCategory, VoteRow> Rows = new();

    private static REPOPopupPage? _popup;
    private static REPOLabel? _timerLabel;
    private static MapCategory? _selected;
    private static bool _isHost;
    private static bool _previousDisableInput;
    private static CursorLockMode _previousCursorLock;
    private static bool _previousCursorVisible;
    private static bool _inputCaptured;

    internal static bool IsOpen => _popup != null;

    internal static void Open(bool isHost)
    {
        Close();
        _isHost = isHost;
        _selected = null;
        Rows.Clear();

        CaptureInput();
        Vector2 position = new(-100f + Plugin.Settings.UiOffsetX.Value, Plugin.Settings.UiOffsetY.Value);
        _popup = MenuAPI.CreateREPOPopupPage(
            "CHOOSE NEXT MAP TYPE",
            shouldCachePage: false,
            pageDimmerVisibility: true,
            spacing: 8f,
            localPosition: position);
        _popup.onEscapePressed = () => false;
        _popup.transform.localScale = Vector3.one * Plugin.Settings.UiScale.Value;

        _popup.AddElementToScrollView(parent =>
        {
            _timerLabel = MenuAPI.CreateREPOLabel("5.0s", parent);
            LabelLayout.Fit(_timerLabel, new Vector2(250f, 34f), 22f, TextAlignmentOptions.Center);
            return _timerLabel.rectTransform;
        }, bottomPadding: 8f);

        AddChoice(MapCategory.Vanilla);
        AddChoice(MapCategory.Modded);
        AddChoice(MapCategory.Random);

        if (isHost)
        {
            _popup.AddElementToScrollView(parent =>
            {
                REPOButton finish = MenuAPI.CreateREPOButton("HOST: FINISH [F]", VoteCoordinator.ForceFinish, parent);
                finish.labelTMP.fontSize = 15f;
                return finish.rectTransform;
            }, topPadding: 12f);
        }

        _popup.OpenPage(true);
        _popup.menuPage.PageStateSet(MenuPage.PageState.Active);
    }

    internal static void SetCountdown(float seconds)
    {
        if (_timerLabel == null)
        {
            return;
        }

        bool show = Plugin.Settings.ShowCountdown.Value;
        _timerLabel.labelTMP.enabled = show;
        if (!show)
        {
            return;
        }

        _timerLabel.labelTMP.text = $"{Mathf.Max(0f, seconds):0.0}s";
        _timerLabel.labelTMP.color = seconds <= 1.25f
            ? new Color(1f, 0.2f, 0.2f, 1f)
            : Color.white;
    }

    internal static void SetTotals(int vanilla, int modded, int random)
    {
        if (Rows.TryGetValue(MapCategory.Vanilla, out VoteRow vanillaRow))
        {
            vanillaRow.SetCount(vanilla);
        }
        if (Rows.TryGetValue(MapCategory.Modded, out VoteRow moddedRow))
        {
            moddedRow.SetCount(modded);
        }
        if (Rows.TryGetValue(MapCategory.Random, out VoteRow randomRow))
        {
            randomRow.SetCount(random);
        }
    }

    internal static void SetSelected(MapCategory option)
    {
        _selected = option;
        foreach (VoteRow row in Rows.Values)
        {
            row.SetSelected(row.Category == option);
        }
    }

    internal static void TickInput()
    {
        if (!IsOpen || !VoteCoordinator.IsVoting)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            VoteCoordinator.SubmitLocalVote(MapCategory.Vanilla);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            VoteCoordinator.SubmitLocalVote(MapCategory.Modded);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            VoteCoordinator.SubmitLocalVote(MapCategory.Random);
        }
        else if (_isHost && Input.GetKeyDown(KeyCode.F))
        {
            VoteCoordinator.ForceFinish();
        }
    }

    internal static void Close()
    {
        if (_popup != null)
        {
            _popup.ClosePage(false);
            Object.Destroy(_popup.gameObject);
        }

        _popup = null;
        _timerLabel = null;
        _selected = null;
        _isHost = false;
        Rows.Clear();
        RestoreInput();
    }

    private static void AddChoice(MapCategory category)
    {
        _popup!.AddElementToScrollView(parent =>
        {
            REPOButton button = MenuAPI.CreateREPOButton(
                category.ToString().ToUpperInvariant(),
                () => VoteCoordinator.SubmitLocalVote(category),
                parent);
            VoteRow row = new(button, category);
            Rows[category] = row;
            row.SetSelected(_selected == category);
            return button.rectTransform;
        });
    }

    private static void CaptureInput()
    {
        if (_inputCaptured)
        {
            return;
        }

        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        if (GameDirector.instance != null)
        {
            _previousDisableInput = GameDirector.instance.DisableInput;
            GameDirector.instance.DisableInput = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _inputCaptured = true;
    }

    private static void RestoreInput()
    {
        if (!_inputCaptured)
        {
            return;
        }

        if (GameDirector.instance != null)
        {
            GameDirector.instance.DisableInput = _previousDisableInput;
        }

        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        _inputCaptured = false;
    }
}
