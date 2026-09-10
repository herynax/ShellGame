using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

/// <summary>Висит на PostEffectController — активен всегда, независимо от того, открывал ли игрок настройки.</summary>
public class BrightnessManager : MonoBehaviour, ISettingsModule
{
    public static BrightnessManager Instance { get; private set; }

    [SerializeField] private Volume globalVolume;
    [SerializeField] private float minExposure = -2f;
    [SerializeField] private float maxExposure = 1f; // Лимит как у вас: 1.0 (или 1.5f если хотите)
    [SerializeField] private float applyTweenDuration = 0.15f;

    public float MinExposure => minExposure;
    public float MaxExposure => maxExposure;

    private const string BRIGHTNESS_KEY = "BrightnessLevel";
    private const float DEFAULT_BRIGHTNESS = 0f; // 0 EV — это стандартная оригинальная яркость

    private ColorAdjustments colorAdjustments;
    private Tween applyTween;
    private float _snapshotValue;

    public float CurrentValue { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (globalVolume == null) { Debug.LogError("[BrightnessManager] Global Volume не назначен!"); return; }
        if (!globalVolume.profile.TryGet(out colorAdjustments)) { Debug.LogError("[BrightnessManager] Нет override'а Color Adjustments!"); return; }

        colorAdjustments.postExposure.overrideState = true;

        CurrentValue = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, DEFAULT_BRIGHTNESS);
        ApplyInstant(CurrentValue);
    }

    private void Start()
    {
        SettingsSaveController.Instance?.RegisterModule(this);
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

        // ДОБАВЛЕНО .SetUpdate(true) — теперь анимация работает при Time.timeScale = 0!
        applyTween = DOVirtual.Float(start, CurrentValue, applyTweenDuration,
            v => colorAdjustments.postExposure.value = v)
            .SetEase(Ease.OutSine)
            .SetUpdate(true); 
    }

    public void Save(float value)
    {
        CurrentValue = Mathf.Clamp(value, minExposure, maxExposure);
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, CurrentValue);
        PlayerPrefs.Save();
    }

    public float LoadAndApply()
    {
        CurrentValue = PlayerPrefs.GetFloat(BRIGHTNESS_KEY, DEFAULT_BRIGHTNESS);
        ApplyInstant(CurrentValue);
        return CurrentValue;
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot() => _snapshotValue = CurrentValue;
    public void Save() { PlayerPrefs.SetFloat(BRIGHTNESS_KEY, CurrentValue); PlayerPrefs.Save(); }
    public void Revert() => ApplyInstant(_snapshotValue);

    private void OnDestroy() => applyTween?.Kill();
}