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
        // Копия с префаба меню настроек просто отступает: работает экземпляр,
        // созданный бутстрапом SettingsServicesBootstrap. Destroy(gameObject) здесь
        // был бы фатален — на префабе меню висят ещё и кнопки/лейблы.
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildResolutionList();
    }

    /// <summary>
    /// Гарантирует существование персистентного менеджера разрешения. Раньше он
    /// жил только на неактивном префабе меню настроек, поэтому на первом запуске
    /// настройки не применялись, а UI вкладки "Дисплей" могла уйти по раннему
    /// return до его Awake.
    /// </summary>
    public static ResolutionSettingsManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("ResolutionSettingsManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<ResolutionSettingsManager>();
    }

    private void Start()
    {
        if (Instance != this) return;

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
        int defaultModeIndex = savedModeIndex >= 0 ? savedModeIndex : DefaultScreenModeIndex();
        _screenModeIndex = PlayerPrefs.GetInt(SCREEN_MODE_KEY, defaultModeIndex);
        _screenModeIndex = Mathf.Clamp(_screenModeIndex, 0, ScreenModes.Length - 1);

        Apply();
    }

    /// <summary>
    /// Режим окна по умолчанию — «без рамки», как в Player Settings проекта
    /// (fullscreenMode: 1). Раньше дефолтом был индекс 0, то есть «Полноэкранный».
    /// </summary>
    private static int DefaultScreenModeIndex()
    {
        int index = System.Array.IndexOf(ScreenModes, FullScreenMode.FullScreenWindow);
        return index >= 0 ? index : 0;
    }

    private void BuildResolutionList()
    {
        var current = Screen.currentResolution;
        var modes = Screen.resolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
            .OrderBy(r => r.width * r.height)
            .ToList();

        // Текущий (нативный) режим может отсутствовать в Screen.resolutions — например
        // при HiDPI-масштабировании. Без него дефолт уезжал на самый большой режим
        // списка, и окно меняло размер на первом же запуске.
        if (!modes.Any(r => r.width == current.width && r.height == current.height))
            modes.Add(current);

        modes.Sort((a, b) => (a.width * a.height).CompareTo(b.width * b.height));
        _resolutions = modes.ToArray();
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < _resolutions.Length; i++)
            if (_resolutions[i].width == width && _resolutions[i].height == height)
                return i;

        // Сюда попадаем только при сохранённом из прошлой конфигурации значении
        // (например, сменили монитор): берём ближайший по числу пикселей, а не
        // самый большой режим.
        int bestIndex = 0;
        int bestDelta = int.MaxValue;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            int delta = Mathf.Abs(_resolutions[i].width * _resolutions[i].height - width * height);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return bestIndex;
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

        // На старте запрошенные параметры обычно уже совпадают с текущими
        // (нативное разрешение + режим из Player Settings); лишний SetResolution
        // там приводит к ресайзу/мерцанию окна на первом кадре.
        if (res.width == Screen.width && res.height == Screen.height && mode == Screen.fullScreenMode)
            return;

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