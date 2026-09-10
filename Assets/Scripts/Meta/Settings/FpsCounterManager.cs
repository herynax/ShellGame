using UnityEngine;

/// <summary>Живёт на persistent-канвасе, активен всегда — показывает/скрывает оверлей FPS.</summary>
public class FpsCounterManager : MonoBehaviour, ISettingsModule
{
    public static FpsCounterManager Instance { get; private set; }

    [SerializeField] private TMPro.TMP_Text fpsLabel;
    [SerializeField] private float updateInterval = 0.5f;

    private const string VISIBLE_KEY = "FpsCounterVisible";

    private float _accumulatedTime;
    private int _accumulatedFrames;
    private bool _snapshot;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        IsVisible = PlayerPrefs.GetInt(VISIBLE_KEY, 0) == 1;
        if (fpsLabel != null) fpsLabel.gameObject.SetActive(IsVisible);
    }

    private void Start()
    {
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (fpsLabel != null) fpsLabel.gameObject.SetActive(visible);

        if (visible) { _accumulatedTime = 0f; _accumulatedFrames = 0; }
    }

    private void Update()
    {
        if (!IsVisible || fpsLabel == null) return;

        _accumulatedTime += Time.unscaledDeltaTime;
        _accumulatedFrames++;

        if (_accumulatedTime >= updateInterval)
        {
            float avg = _accumulatedFrames / _accumulatedTime;
            fpsLabel.text = $"{Mathf.RoundToInt(avg)} FPS";
            _accumulatedTime = 0f;
            _accumulatedFrames = 0;
        }
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot() => _snapshot = IsVisible;
    public void Save() { PlayerPrefs.SetInt(VISIBLE_KEY, IsVisible ? 1 : 0); PlayerPrefs.Save(); }
    public void Revert() => SetVisible(_snapshot);
}