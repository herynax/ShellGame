using UnityEngine;
using UnityEngine.UI;

/// <summary>Живёт на панели "Графика", может быть неактивна большую часть времени.</summary>
public class BrightnessSliderUI : MonoBehaviour
{
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private bool startGameScreen = false;

    private void Awake()
    {
        if (brightnessSlider != null && BrightnessManager.Instance != null)
        {
            brightnessSlider.minValue = BrightnessManager.Instance.MinExposure;
            brightnessSlider.maxValue = BrightnessManager.Instance.MaxExposure;
        }
    }

    private void OnEnable()
    {
        if (BrightnessManager.Instance == null || brightnessSlider == null) return;

        // Синхронизируем границы на случай, если Awake произошел раньше BrightnessManager
        brightnessSlider.minValue = BrightnessManager.Instance.MinExposure;
        brightnessSlider.maxValue = BrightnessManager.Instance.MaxExposure;

        // 0 будет ровно посередине между -2 и 1, и соответствует CurrentValue
        brightnessSlider.SetValueWithoutNotify(BrightnessManager.Instance.CurrentValue);
        brightnessSlider.onValueChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        if (brightnessSlider != null)
            brightnessSlider.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(float value)
    {
        if (BrightnessManager.Instance == null) return;

        BrightnessManager.Instance.ApplySmooth(value);

        SettingsSaveController.Instance?.MarkDirty();
    }
}