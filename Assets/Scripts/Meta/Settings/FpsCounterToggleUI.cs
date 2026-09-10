using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Тумблер "Показывать FPS" на вкладке "Дисплей".</summary>
public class FpsCounterToggleUI : MonoBehaviour
{
    [SerializeField] private Toggle visibilityToggle;
    [SerializeField] private TMP_Text valueLabel;

    private void OnEnable()
    {
        if (FpsCounterManager.Instance == null) return;
        visibilityToggle.SetIsOnWithoutNotify(FpsCounterManager.Instance.IsVisible);
        visibilityToggle.onValueChanged.AddListener(OnChanged);

        UpdateLabels();
    }

    private void OnDisable()
    {
        visibilityToggle.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(bool value)
    {
        FpsCounterManager.Instance.SetVisible(value);
        SettingsSaveController.Instance?.MarkDirty();

        UpdateLabels();
    }

        private void UpdateLabels()
    {
        var fcm = FpsCounterManager.Instance;

        valueLabel.text = fcm.IsVisible ? "Вкл" : "Выкл";
    }
}