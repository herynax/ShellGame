using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Разрешение экрана + режим окна (Fullscreen / Borderless / Windowed).
/// Живёт на persistent-объекте (там же, где DisplaySettingsManager), применяет
/// сохранённое (или текущее нативное) значение сразу в Awake.
/// </summary>
public class ResolutionSettingsManager : MonoBehaviour, ISettingsModule
{
    public static ResolutionSettingsManager Instance { get; private set; }

    private const string RES_WIDTH_KEY = "ResolutionWidth";
    private const string RES_HEIGHT_KEY = "ResolutionHeight";
    private const string SCREEN_MODE_KEY = "ScreenMode";

    // Порядок = то, что листается стрелками < > на UI.
    private static readonly FullScreenMode[] ScreenModes =
    {
        FullScreenMode.ExclusiveFullScreen, // "Полноэкранный"
        FullScreenMode.FullScreenWindow,    // "Без рамки" (borderless)
        FullScreenMode.Windowed             // "Оконный"
    };

    private Resolution[] _resolutions; // отфильтрованные, без дублей по width x height
    private int _resolutionIndex;
    private int _screenModeIndex;

    private int _snapshotResolutionIndex;
    private int _snapshotScreenModeIndex;

    public IReadOnlyList<Resolution> Resolutions => _resolutions;
    public int ResolutionIndex => _resolutionIndex;
    public int ScreenModeIndex => _screenModeIndex;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildResolutionList();

        int savedWidth = PlayerPrefs.GetInt(RES_WIDTH_KEY, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(RES_HEIGHT_KEY, Screen.currentResolution.height);
        _resolutionIndex = FindResolutionIndex(savedWidth, savedHeight);

        int savedModeIndex = System.Array.IndexOf(ScreenModes, Screen.fullScreenMode);
        _screenModeIndex = PlayerPrefs.GetInt(SCREEN_MODE_KEY, savedModeIndex >= 0 ? savedModeIndex : 0);
        _screenModeIndex = Mathf.Clamp(_screenModeIndex, 0, ScreenModes.Length - 1);

        Apply();
    }

    private void Start()
    {
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    private void BuildResolutionList()
    {
        // Screen.resolutions на одном и том же разрешении часто даёт несколько записей
        // с разной частотой обновления — схлопываем по width x height, оставляя
        // максимальную частоту для каждого разрешения.
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

        // Сохранённого разрешения больше нет (сменили монитор и т.п.) — берём ближайшее к нативному.
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

    /// <summary>Человекочитаемое имя текущего режима — для UI-лейбла.</summary>
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
        PlayerPrefs.Save();
    }

    public void Revert()
    {
        _resolutionIndex = _snapshotResolutionIndex;
        _screenModeIndex = _snapshotScreenModeIndex;
        Apply();
    }
}