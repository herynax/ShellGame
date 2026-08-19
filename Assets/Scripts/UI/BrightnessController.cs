using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BrightnessController : MonoBehaviour
{
    public static BrightnessController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Slider brightnessSlider;

    [Header("Настройки")]
    [Range(0f, 1f)]
    [SerializeField] private float minBrightness = 0.15f;

    [SerializeField] private float applyTweenDuration = 0.15f;

    private const string BRIGHTNESS_KEY = "BrightnessLevel";
    private const float DEFAULT_BRIGHTNESS = 1f;

    private Image brightnessOverlay;
    private Tween applyTween;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[BrightnessController] SceneLoader не найден!");
            return;
        }

        brightnessOverlay = SceneLoader.Instance.BrightnessOverlay;

        if (brightnessOverlay == null)
        {
            Debug.LogError("[BrightnessController] BrightnessOverlay не назначен в SceneLoader!");
            return;
        }
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

    public void ApplyInstant(float value)
    {
        if (brightnessOverlay == null)
            return;

        applyTween?.Kill();

        value = Mathf.Clamp01(value);

        float brightness = Mathf.Lerp(
            minBrightness,
            DEFAULT_BRIGHTNESS,
            value
        );

        Color color = brightnessOverlay.color;
        color.a = 1f - brightness;
        brightnessOverlay.color = color;
    }

    public void ApplySmooth(float value)
    {
        if (brightnessOverlay == null)
            return;

        applyTween?.Kill();

        value = Mathf.Clamp01(value);

        float brightness = Mathf.Lerp(
            minBrightness,
            DEFAULT_BRIGHTNESS,
            value
        );

        float targetAlpha = 1f - brightness;

        applyTween = brightnessOverlay
            .DOFade(targetAlpha, applyTweenDuration)
            .SetEase(Ease.OutSine);
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