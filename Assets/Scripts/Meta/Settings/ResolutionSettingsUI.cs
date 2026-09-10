using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Стрелки < > для разрешения и режима экрана на вкладке "Дисплей".</summary>
public class ResolutionSettingsUI : MonoBehaviour
{
    [SerializeField] private Button resolutionPrevButton;
    [SerializeField] private Button resolutionNextButton;
    [SerializeField] private TMP_Text resolutionValueLabel;

    [SerializeField] private Button screenModePrevButton;
    [SerializeField] private Button screenModeNextButton;
    [SerializeField] private TMP_Text screenModeValueLabel;

    private void OnEnable()
    {
        if (ResolutionSettingsManager.Instance == null) return;

        resolutionPrevButton.onClick.AddListener(OnResolutionPrev);
        resolutionNextButton.onClick.AddListener(OnResolutionNext);
        screenModePrevButton.onClick.AddListener(OnScreenModePrev);
        screenModeNextButton.onClick.AddListener(OnScreenModeNext);

        UpdateLabels();
    }

    private void OnDisable()
    {
        resolutionPrevButton.onClick.RemoveListener(OnResolutionPrev);
        resolutionNextButton.onClick.RemoveListener(OnResolutionNext);
        screenModePrevButton.onClick.RemoveListener(OnScreenModePrev);
        screenModeNextButton.onClick.RemoveListener(OnScreenModeNext);
    }

    private void OnResolutionPrev() { ResolutionSettingsManager.Instance.SelectPreviousResolution(); MarkDirtyAndRefresh(); }
    private void OnResolutionNext() { ResolutionSettingsManager.Instance.SelectNextResolution(); MarkDirtyAndRefresh(); }
    private void OnScreenModePrev() { ResolutionSettingsManager.Instance.SelectPreviousScreenMode(); MarkDirtyAndRefresh(); }
    private void OnScreenModeNext() { ResolutionSettingsManager.Instance.SelectNextScreenMode(); MarkDirtyAndRefresh(); }

    private void MarkDirtyAndRefresh()
    {
        SettingsSaveController.Instance?.MarkDirty();
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        var mgr = ResolutionSettingsManager.Instance;
        var res = mgr.Resolutions[mgr.ResolutionIndex];

        resolutionValueLabel.text = $"{res.width} x {res.height}";
        screenModeValueLabel.text = mgr.GetScreenModeLabel(mgr.ScreenModeIndex);
    }
}