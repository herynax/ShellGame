using UnityEngine;

public class DisplaySettingsManager : MonoBehaviour, ISettingsModule
{
    public static DisplaySettingsManager Instance { get; private set; }

    [SerializeField] private int[] fpsOptions = { 30, 60, 90, 120, 144, -1 };

    private const string VSYNC_KEY = "VSyncEnabled";
    private const string FPS_LIMIT_INDEX_KEY = "FpsLimitIndex";

    private bool _snapshotVSync;
    private int _snapshotFpsIndex;

    public bool VSyncEnabled { get; private set; }
    public int FpsOptionIndex { get; private set; }
    public int[] FpsOptions => fpsOptions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadAndApply();
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    public void LoadAndApply()
    {
        VSyncEnabled = PlayerPrefs.GetInt(VSYNC_KEY, 0) == 1;
        FpsOptionIndex = Mathf.Clamp(PlayerPrefs.GetInt(FPS_LIMIT_INDEX_KEY, DefaultFpsIndex()), 0, fpsOptions.Length - 1);
        Apply();
    }

    private int DefaultFpsIndex()
    {
        int index = System.Array.IndexOf(fpsOptions, 60);
        return index >= 0 ? index : 0;
    }

    public void SetVSync(bool enabled) { VSyncEnabled = enabled; Apply(); }
    public void SetFpsOptionIndex(int index) { FpsOptionIndex = Mathf.Clamp(index, 0, fpsOptions.Length - 1); Apply(); }

    public void SelectNextFpsOption() => SetFpsOptionIndex((FpsOptionIndex + 1) % fpsOptions.Length);
    public void SelectPreviousFpsOption() => SetFpsOptionIndex((FpsOptionIndex - 1 + fpsOptions.Length) % fpsOptions.Length);

    private void Apply()
    {
        QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = VSyncEnabled ? -1 : fpsOptions[FpsOptionIndex];
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot()
    {
        _snapshotVSync = VSyncEnabled;
        _snapshotFpsIndex = FpsOptionIndex;
    }

    public void Save()
    {
        PlayerPrefs.SetInt(VSYNC_KEY, VSyncEnabled ? 1 : 0);
        PlayerPrefs.SetInt(FPS_LIMIT_INDEX_KEY, FpsOptionIndex);
    }

    public void Revert()
    {
        VSyncEnabled = _snapshotVSync;
        FpsOptionIndex = _snapshotFpsIndex;
        Apply();
    }
}