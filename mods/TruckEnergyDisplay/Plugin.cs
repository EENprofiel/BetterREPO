using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TruckEnergyDisplay;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.lucasdoddema.truckenergydisplay";
    public const string PluginName = "Truck Energy Display";
    public const string PluginVersion = "1.2.0";
    internal static ManualLogSource Log = null!;
    internal static ConfigEntry<bool> ShowTruckEnergy = null!, ShowCrystalRequirement = null!, ShowCheckoutPrediction = null!,
        ShowEnergyBar = null!, ShowPercentage = null!, ShowRawEnergy = null!;
    internal static ConfigEntry<float> OffsetX = null!, OffsetY = null!, UiScale = null!, RefreshInterval = null!;
    private GameEnergyReader _reader = null!;
    private EnergyNetwork _network = null!;
    private EnergyHud _hud = null!;
    private float _nextRefresh;
    private bool _wasShop;
    private string _lastError = "";
    private void Awake()
    {
        Log = Logger;
        ShowTruckEnergy = Config.Bind("Display", "ShowTruckEnergy", true, "Show the HUD only in the Service Station. Host synchronization continues if hidden.");
        ShowCrystalRequirement = Config.Bind("Display", "ShowCrystalRequirement", true, "Show crystals required for full energy after the next truck loads.");
        ShowCheckoutPrediction = Config.Bind("Display", "ShowCheckoutPrediction", true, "Predict the result of buying crystals selected in checkout.");
        ShowEnergyBar = Config.Bind("Display", "ShowEnergyBar", true, "Show a small yellow energy bar.");
        ShowPercentage = Config.Bind("Display", "ShowPercentage", true, "Show percentage of the verified station capacity.");
        ShowRawEnergy = Config.Bind("Display", "ShowRawEnergy", false, "Also show current / maximum energy units.");
        OffsetX = Config.Bind("Display", "RightMargin", 40f, new ConfigDescription("Margin from the right edge at 1080p.", new AcceptableValueRange<float>(0, 1800)));
        OffsetY = Config.Bind("Display", "TopMargin", 220f, new ConfigDescription("Margin from the top edge at 1080p.", new AcceptableValueRange<float>(0, 900)));
        UiScale = Config.Bind("Display", "Scale", 1f, new ConfigDescription("Additional HUD scale.", new AcceptableValueRange<float>(0.6f, 1.8f)));
        RefreshInterval = Config.Bind("Performance", "RefreshInterval", 0.25f, new ConfigDescription("Seconds between snapshots.", new AcceptableValueRange<float>(0.1f, 1f)));
        _reader = new GameEnergyReader(); _network = new EnergyNetwork(); _hud = new EnergyHud();
        SceneManager.sceneLoaded += SceneLoaded;
        Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Informational only; no gameplay patches.");
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _reader.Reset(); _network.Reset(); _hud.Dispose(); _nextRefresh = 0;
    }
    private void Update()
    {
        if (_reader == null) return;
        try {
            bool shop = GameEnergyReader.InShop;
            if (!shop) {
                if (_wasShop) { _reader.Reset(); _network.Reset(); _hud.Dispose(); }
                _wasShop = false; return;
            }
            _wasShop = true;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval.Value;
            _network.RefreshContext();
            EnergySnapshot snapshot;
            if (!GameManager.Multiplayer() || (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)) {
                snapshot = _reader.Read(); _network.Publish(snapshot);
            } else snapshot = _network.ClientRead();
            _hud.Render(snapshot, ShowTruckEnergy.Value && GameEnergyReader.Ready);
        } catch (Exception ex) {
            _hud.Hide();
            if (_lastError != ex.Message) { _lastError = ex.Message; Logger.LogWarning("HUD unavailable: " + ex.Message); }
        }
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded; _network?.Dispose(); _hud?.Dispose();
    }
}
