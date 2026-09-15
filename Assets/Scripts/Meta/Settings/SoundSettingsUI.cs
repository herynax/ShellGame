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

    private bool _subscribed;
    private bool _previewBound;
    private bool _syncedFromManager;

    private void OnEnable()
    {
        // Подстраховка: если по какой-то причине бутстрап не отработал
        // (например, в юнити-тестах), создаём сервисы на месте.
        SettingsSaveController.EnsureExists();
        SoundSettingsManager.EnsureExists();

        Subscribe();

        if (!_previewBound)
            BindPointerUpPreview();

        var mgr = SoundSettingsManager.Instance;
        if (mgr != null && mgr.IsReady)
            SyncFromManager();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (_syncedFromManager)
            return;

        var mgr = SoundSettingsManager.Instance;
        if (mgr == null || !mgr.IsReady)
            return;

        // FMOD подоспел позже открытия панели — синкаем слайдеры теперь.
        SyncFromManager();
    }

    private void Subscribe()
    {
        if (_subscribed) return;

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);

        _subscribed = false;
    }

    private void SyncFromManager()
    {
        var mgr = SoundSettingsManager.Instance;
        if (mgr == null) return;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(mgr.MasterVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(mgr.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(mgr.SfxVolume);

        _syncedFromManager = true;
    }

    private void OnMasterChanged(float v) { var mgr = SoundSettingsManager.Instance; if (mgr == null) return; mgr.SetMasterVolume(v); SettingsSaveController.Instance?.MarkDirty(); }
    private void OnMusicChanged(float v) { var mgr = SoundSettingsManager.Instance; if (mgr == null) return; mgr.SetMusicVolume(v); SettingsSaveController.Instance?.MarkDirty(); }
    private void OnSfxChanged(float v) { var mgr = SoundSettingsManager.Instance; if (mgr == null) return; mgr.SetSfxVolume(v); SettingsSaveController.Instance?.MarkDirty(); }

    private void BindPointerUpPreview()
    {
        if (sfxSlider == null) return;

        var trigger = sfxSlider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = sfxSlider.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => SoundSettingsManager.Instance?.PlaySfxPreview(sfxPreviewEvent));
        trigger.triggers.Add(entry);

        _previewBound = true;
    }
}