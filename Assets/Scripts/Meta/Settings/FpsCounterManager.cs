using UnityEngine;

public class FpsCounterManager : MonoBehaviour, ISettingsModule
{
    public static FpsCounterManager Instance { get; private set; }

    [SerializeField] private TMPro.TMP_Text fpsLabel;
    [SerializeField] private float updateInterval = 0.5f;

    private const string VISIBLE_KEY = "FpsCounterVisible";
    private const string LABEL_NAME = "FPS";

    private float _accumulatedTime;
    private int _accumulatedFrames;
    private bool _snapshot;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Гарантирует существование персистентного менеджера счётчика FPS. Раньше он
    /// не был прицеплен ни к одной сцене/префабу, поэтому переключатель в меню
    /// настроек работал вхолостую.
    /// </summary>
    public static FpsCounterManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("FpsCounterManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<FpsCounterManager>();
    }

    private void Start()
    {
        LoadAndApply();
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    public void LoadAndApply()
    {
        IsVisible = PlayerPrefs.GetInt(VISIBLE_KEY, 0) == 1;
        if (fpsLabel != null) fpsLabel.gameObject.SetActive(IsVisible);
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (fpsLabel != null) fpsLabel.gameObject.SetActive(visible);

        if (visible) { _accumulatedTime = 0f; _accumulatedFrames = 0; }
    }

    private void Update()
    {
        if (fpsLabel == null) TryBindLabel();
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

    /// <summary>
    /// Подхватывает текст "FPS" на канвасе, если поле не назначено в инспекторе:
    /// сам счётчик создаётся бутстрапом без сцены, а лейбл в ===FadeCanvas.prefab
    /// остался без ссылки. Поиск отложенный — префаб может появиться позже.
    /// </summary>
    private void TryBindLabel()
    {
        var labels = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var label in labels)
        {
            if (label.name != LABEL_NAME) continue;

            fpsLabel = label;
            fpsLabel.gameObject.SetActive(IsVisible);
            return;
        }
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot() => _snapshot = IsVisible;
    public void Save() => PlayerPrefs.SetInt(VISIBLE_KEY, IsVisible ? 1 : 0);
    public void Revert() => SetVisible(_snapshot);
}