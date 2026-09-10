using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>UI на вкладке "Дисплей": VSync-тумблер + стрелки лимита FPS.</summary>
public class DisplaySettingsUI : MonoBehaviour
{
    [SerializeField] private Toggle vSyncToggleButton;
    [SerializeField] private TMP_Text vSyncValueLabel;

    [SerializeField] private Button fpsLimitPrevButton;
    [SerializeField] private Button fpsLimitNextButton;
    [SerializeField] private TMP_Text fpsLimitValueLabel;

    private void OnEnable()
    {
        if (DisplaySettingsManager.Instance == null) return;

        vSyncToggleButton.onValueChanged.AddListener(OnVSyncToggle);
        fpsLimitPrevButton.onClick.AddListener(OnFpsPrev);
        fpsLimitNextButton.onClick.AddListener(OnFpsNext);

        UpdateLabels();
    }

    private void OnDisable()
    {
        vSyncToggleButton.onValueChanged.RemoveListener(OnVSyncToggle);
        fpsLimitPrevButton.onClick.RemoveListener(OnFpsPrev);
        fpsLimitNextButton.onClick.RemoveListener(OnFpsNext);
    }

    private void OnVSyncToggle(bool isEnabled)
    {
        var mgr = DisplaySettingsManager.Instance;
        if (mgr == null) return;

        mgr.SetVSync(isEnabled);
        SettingsSaveController.Instance?.MarkDirty();
        UpdateLabels();
    }


    private void OnFpsPrev() { DisplaySettingsManager.Instance.SelectPreviousFpsOption(); SettingsSaveController.Instance?.MarkDirty(); UpdateLabels(); }
    private void OnFpsNext() { DisplaySettingsManager.Instance.SelectNextFpsOption(); SettingsSaveController.Instance?.MarkDirty(); UpdateLabels(); }

    private void UpdateLabels()
    {
        var mgr = DisplaySettingsManager.Instance;

        vSyncValueLabel.text = mgr.VSyncEnabled ? "Вкл" : "Выкл";

        int value = mgr.FpsOptions[mgr.FpsOptionIndex];
        fpsLimitValueLabel.text = value <= 0 ? "Без ограничений" : value.ToString();

        bool fpsInteractable = !mgr.VSyncEnabled;
        fpsLimitPrevButton.interactable = fpsInteractable;
        fpsLimitNextButton.interactable = fpsInteractable;
    }
}