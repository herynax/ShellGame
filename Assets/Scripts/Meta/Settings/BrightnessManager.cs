using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class BrightnessManager : MonoBehaviour, ISettingsModule
{
    public static BrightnessManager Instance { get; private set; }

    [SerializeField] private Volume globalVolume;
    [SerializeField] private float minExposure = -2f;
    [SerializeField] private float maxExposure = 1f;
    [SerializeField] private float applyTweenDuration = 0.15f;

    public float MinExposure => minExposure;
    public float MaxExposure => maxExposure;

    private const string BRIGHTNESS_KEY = "BrightnessLevel";
    private const float DEFAULT_BRIGHTNESS = 0f;

    private ColorAdjustments colorAdjustments;
    private Tween applyTween;
    private float _snapshotValue;

    public float CurrentValue { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitVolume();
    }

    private void Start()
    {
        LoadAndApply();
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    private void InitVolume()
    {
        if (globalVolume == null) { Debug.LogError("[BrightnessManager] Global Volume не назначен!"); return; }
        if (!globalVolume.profile.TryGet(out colorAdjustments)) { Debug.LogError("[BrightnessManager] Нет override'а Color Adjustments!"); return; }

        colorAdjustments.postExposure.overrideState = true;
    }

    public void LoadAndApply()
    {
        if (colorAdjustments == null) InitVolume();
        CurrentValue = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, DEFAULT_BRIGHTNESS);
        ApplyInstant(CurrentValue);
    }

    public void ApplyInstant(float value)
    {
        if (colorAdjustments == null) return;
        
        CurrentValue = Mathf.Clamp(value, minExposure, maxExposure);
        applyTween?.Kill();
        colorAdjustments.postExposure.value = CurrentValue;
    }

    public void ApplySmooth(float value)
    {
        if (colorAdjustments == null) return;

        CurrentValue = Mathf.Clamp(value, minExposure, maxExposure);
        applyTween?.Kill();
        
        float start = colorAdjustments.postExposure.value;

        applyTween = DOVirtual.Float(start, CurrentValue, applyTweenDuration,
            v => colorAdjustments.postExposure.value = v)
            .SetEase(Ease.OutSine)
            .SetUpdate(true); 
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot() => _snapshotValue = CurrentValue;
    public void Save() => PlayerPrefs.SetFloat(BRIGHTNESS_KEY, CurrentValue);
    public void Revert() => ApplyInstant(_snapshotValue);

    private void OnDestroy() => applyTween?.Kill();
}