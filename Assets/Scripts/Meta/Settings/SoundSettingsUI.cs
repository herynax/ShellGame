using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FMODUnity;

/// <summary>Живёт на панели "Аудио".</summary>
public class SoundSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField, EventRef] private string sfxPreviewEvent;

    private void OnEnable()
    {
        var mgr = SoundSettingsManager.Instance;
        if (mgr == null || !mgr.IsReady) return; // если ещё не успел прогрузиться FMOD — просто не синкаем в этот раз

        masterSlider.SetValueWithoutNotify(mgr.MasterVolume);
        musicSlider.SetValueWithoutNotify(mgr.MusicVolume);
        sfxSlider.SetValueWithoutNotify(mgr.SfxVolume);

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        AddPointerUpPreview(sfxSlider);
    }

    private void OnDisable()
    {
        masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    }

    private void OnMasterChanged(float v) { SoundSettingsManager.Instance.SetMasterVolume(v); SettingsSaveController.Instance?.MarkDirty(); }
    private void OnMusicChanged(float v) { SoundSettingsManager.Instance.SetMusicVolume(v); SettingsSaveController.Instance?.MarkDirty(); }
    private void OnSfxChanged(float v) { SoundSettingsManager.Instance.SetSfxVolume(v); SettingsSaveController.Instance?.MarkDirty(); }

    private void AddPointerUpPreview(Slider slider)
    {
        var trigger = slider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = slider.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear(); // на случай повторного OnEnable, чтобы не задублировать
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => SoundSettingsManager.Instance.PlaySfxPreview(sfxPreviewEvent));
        trigger.triggers.Add(entry);
    }
}