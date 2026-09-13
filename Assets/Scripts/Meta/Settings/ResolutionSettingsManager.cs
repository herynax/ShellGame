using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResolutionSettingsManager : MonoBehaviour, ISettingsModule
{
    public static ResolutionSettingsManager Instance { get; private set; }

    private const string RES_WIDTH_KEY = "ResolutionWidth";
    private const string RES_HEIGHT_KEY = "ResolutionHeight";
    private const string SCREEN_MODE_KEY = "ScreenMode";

    private static readonly FullScreenMode[] ScreenModes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    private Resolution[] _resolutions;
    private int _resolutionIndex;
    private int _screenModeIndex;

    private int _snapshotResolutionIndex;
    private int _snapshotScreenModeIndex;

    public IReadOnlyList<Resolution> Resolutions => _resolutions;
    public int ResolutionIndex => _resolutionIndex;
    public int ScreenModeIndex => _screenModeIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildResolutionList();
    }

    private void Start()
    {
        LoadAndApply();
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    public void LoadAndApply()
    {
        if (_resolutions == null || _resolutions.Length == 0)
            BuildResolutionList();

        int savedWidth = PlayerPrefs.GetInt(RES_WIDTH_KEY, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(RES_HEIGHT_KEY, Screen.currentResolution.height);
        _resolutionIndex = FindResolutionIndex(savedWidth, savedHeight);

        int savedModeIndex = System.Array.IndexOf(ScreenModes, Screen.fullScreenMode);
        _screenModeIndex = PlayerPrefs.GetInt(SCREEN_MODE_KEY, savedModeIndex >= 0 ? savedModeIndex : 0);
        _screenModeIndex = Mathf.Clamp(_screenModeIndex, 0, ScreenModes.Length - 1);

        Apply();
    }

    private void BuildResolutionList()
    {
        _resolutions = Screen.resolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
            .OrderBy(r => r.width * r.height)
            .ToArray();

        if (_resolutions.Length == 0)
            _resolutions = new[] { Screen.currentResolution };
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < _resolutions.Length; i++)
            if (_resolutions[i].width == width && _resolutions[i].height == height)
                return i;

        return _resolutions.Length - 1;
    }

    public void SelectNextResolution() => SetResolutionIndex((_resolutionIndex + 1) % _resolutions.Length);
    public void SelectPreviousResolution() => SetResolutionIndex((_resolutionIndex - 1 + _resolutions.Length) % _resolutions.Length);

    public void SetResolutionIndex(int index)
    {
        _resolutionIndex = Mathf.Clamp(index, 0, _resolutions.Length - 1);
        Apply();
    }

    public void SelectNextScreenMode() => SetScreenModeIndex((_screenModeIndex + 1) % ScreenModes.Length);
    public void SelectPreviousScreenMode() => SetScreenModeIndex((_screenModeIndex - 1 + ScreenModes.Length) % ScreenModes.Length);

    public void SetScreenModeIndex(int index)
    {
        _screenModeIndex = Mathf.Clamp(index, 0, ScreenModes.Length - 1);
        Apply();
    }

    public string GetScreenModeLabel(int index)
    {
        switch (ScreenModes[index])
        {
            case FullScreenMode.ExclusiveFullScreen: return "Полноэкранный";
            case FullScreenMode.FullScreenWindow: return "Без рамки";
            case FullScreenMode.Windowed: return "Оконный";
            default: return ScreenModes[index].ToString();
        }
    }

    private void Apply()
    {
        var res = _resolutions[_resolutionIndex];
        var mode = ScreenModes[_screenModeIndex];
        Screen.SetResolution(res.width, res.height, mode);
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot()
    {
        _snapshotResolutionIndex = _resolutionIndex;
        _snapshotScreenModeIndex = _screenModeIndex;
    }

    public void Save()
    {
        var res = _resolutions[_resolutionIndex];
        PlayerPrefs.SetInt(RES_WIDTH_KEY, res.width);
        PlayerPrefs.SetInt(RES_HEIGHT_KEY, res.height);
        PlayerPrefs.SetInt(SCREEN_MODE_KEY, _screenModeIndex);
    }

    public void Revert()
    {
        _resolutionIndex = _snapshotResolutionIndex;
        _screenModeIndex = _snapshotScreenModeIndex;
        Apply();
    }
}