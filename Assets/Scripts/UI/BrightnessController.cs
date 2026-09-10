using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class BrightnessController : MonoBehaviour
{
    public static BrightnessController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Slider brightnessSlider;

    [Header("Post Processing")]
    [SerializeField] private Volume globalVolume;

    [Header("Настройки (Exposure, EV)")]
    [SerializeField] private float minExposure = -2f;  // самое тёмное (value = 0)
    [SerializeField] private float maxExposure = 1.5f; // самое светлое (value = 1)

    [SerializeField] private float applyTweenDuration = 0.15f;

    private const string BRIGHTNESS_KEY = "BrightnessLevel";
    private const float DEFAULT_BRIGHTNESS = 0.5f; // центр слайдера — нейтральная экспозиция (0 EV)

    private ColorAdjustments colorAdjustments;
    private Tween applyTween;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (globalVolume == null)
        {
            Debug.LogError("[BrightnessController] Global Volume не назначен!");
            return;
        }

        if (!globalVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("[BrightnessController] В профиле Volume нет override'а Color Adjustments!");
            return;
        }

        // Экспозиция должна быть включена (override enabled) в профиле,
        // иначе значение не будет применяться рендером.
        colorAdjustments.postExposure.overrideState = true;
    }

    public float LoadAndApply()
    {
        float value = PlayerPrefs.GetFloat(
            BRIGHTNESS_KEY,
            DEFAULT_BRIGHTNESS
        );

        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(value);

        ApplyInstant(value);

        return value;
    }

    // value 0..1, 0.5 = нейтральная экспозиция (0 EV)
    private float EvaluateExposure(float value)
    {
        value = Mathf.Clamp01(value);

        if (value < 0.5f)
        {
            float t = value / 0.5f; // 0 в крайней тьме, 1 в центре
            return Mathf.Lerp(minExposure, 0f, t);
        }
        else
        {
            float t = (value - 0.5f) / 0.5f; // 0 в центре, 1 в максимальной светлоте
            return Mathf.Lerp(0f, maxExposure, t);
        }
    }

    public void ApplyInstant(float value)
    {
        if (colorAdjustments == null)
            return;

        applyTween?.Kill();

        colorAdjustments.postExposure.value = EvaluateExposure(value);
    }

    public void ApplySmooth(float value)
    {
        if (colorAdjustments == null)
            return;

        applyTween?.Kill();

        float targetExposure = EvaluateExposure(value);
        float startExposure = colorAdjustments.postExposure.value;

        applyTween = DOVirtual.Float(
            startExposure,
            targetExposure,
            applyTweenDuration,
            v => colorAdjustments.postExposure.value = v
        ).SetEase(Ease.OutSine);
    }

    public void Save()
    {
        float value = brightnessSlider != null
            ? brightnessSlider.value
            : DEFAULT_BRIGHTNESS;

        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, value);
        PlayerPrefs.Save();
    }

    public void Save(float value)
    {
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, value);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        applyTween?.Kill();
    }
}