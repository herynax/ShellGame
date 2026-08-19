using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using FMODUnity;

/// <summary>
/// Стартовая сцена: тревожный театральный фейд-ин панели яркости,
/// после "Принять" — спокойный кроссфейд в панель звука, сохранение,
/// переход в следующую сцену.
///
/// При повторном запуске (SettingsInitialized == 1) весь UI пропускается —
/// настройки подтягиваются из PlayerPrefs, применяются, сразу грузится следующая сцена.
/// </summary>
public class IntroSettingsManager : MonoBehaviour
{
    [Header("Яркость")]
    [SerializeField] private BrightnessController brightnessController;
    [SerializeField] private CanvasGroup brightnessPanel;
    [SerializeField] private RectTransform brightnessPanelRect; // для шейка во время фликов
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Button acceptBrightnessButton;

    [Header("Звук")]
    [SerializeField] private CanvasGroup soundPanel;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button soundConfirmButton;

    [Header("FMOD Paths")]
    [SerializeField] private string masterBusPath = "bus:/";
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Header("Тревожный фейд-ин (яркость)")]
    [SerializeField] private float darknessPause = 0.5f;      // пауза в темноте перед фликами
    [SerializeField] private float theatricalFadeDuration = 1.8f; // финальный долгий фейд
    [SerializeField, EventRef] private string anxietyStingEvent; // опционально, FMOD one-shot

    [Header("Спокойные переходы")]
    [SerializeField] private float calmFadeDuration = 0.5f;

    [Header("Переходы между сценами")]
    [SerializeField] private string nextSceneName = "";

    // PlayerPrefs Keys
    private const string INITIALIZED_KEY = "SettingsInitialized";
    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus musicBus;
    private FMOD.Studio.Bus sfxBus;

    private IEnumerator Start()
    {
        while (!RuntimeManager.IsInitialized)
            yield return null;

        while (!RuntimeManager.HaveAllBanksLoaded)
            yield return null;

        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);

        SetPanelInstant(brightnessPanel, 0f, false);
        SetPanelInstant(soundPanel, 0f, false);

        bool isFirstLaunch = PlayerPrefs.GetInt(INITIALIZED_KEY, 0) == 0;

        if (!isFirstLaunch)
        {
            brightnessController.LoadAndApply();

            float master = PlayerPrefs.GetFloat(MASTER_KEY, 1f);
            float music = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
            float sfx = PlayerPrefs.GetFloat(SFX_KEY, 1f);
            ApplyVolumes(master, music, sfx);

            LoadNextScene();
            yield break;
        }

        yield return RunFirstLaunchFlow();
    }

    private IEnumerator RunFirstLaunchFlow()
    {
        // Значения по умолчанию
        brightnessSlider.SetValueWithoutNotify(1f);
        brightnessController.ApplyInstant(1f);

        masterSlider.SetValueWithoutNotify(1f);
        musicSlider.SetValueWithoutNotify(1f);
        sfxSlider.SetValueWithoutNotify(1f);
        ApplyVolumes(1f, 1f, 1f);

        brightnessSlider.onValueChanged.AddListener(brightnessController.ApplyInstant);

        // 1. Тревожный театральный фейд-ин панели яркости
        yield return TheatricalFadeIn(brightnessPanel, brightnessPanelRect).WaitForCompletion();

        bool brightnessConfirmed = false;
        void OnAccept() => brightnessConfirmed = true;
        acceptBrightnessButton.onClick.AddListener(OnAccept);

        yield return new WaitUntil(() => brightnessConfirmed);
        acceptBrightnessButton.onClick.RemoveListener(OnAccept);
        brightnessSlider.onValueChanged.RemoveListener(brightnessController.ApplyInstant);

        // 2. Спокойный кроссфейд: яркость -> звук
        masterSlider.onValueChanged.AddListener(v => ApplyVolumes(v, musicSlider.value, sfxSlider.value));
        musicSlider.onValueChanged.AddListener(v => ApplyVolumes(masterSlider.value, v, sfxSlider.value));
        sfxSlider.onValueChanged.AddListener(v => ApplyVolumes(masterSlider.value, musicSlider.value, v));

        yield return CalmCrossfade(brightnessPanel, soundPanel);

        bool soundConfirmed = false;
        void OnSoundConfirm() => soundConfirmed = true;
        soundConfirmButton.onClick.AddListener(OnSoundConfirm);

        yield return new WaitUntil(() => soundConfirmed);
        soundConfirmButton.onClick.RemoveListener(OnSoundConfirm);

        yield return soundPanel.DOFade(0f, calmFadeDuration).SetEase(Ease.InOutSine).SetUpdate(true).WaitForCompletion();
        soundPanel.interactable = false;
        soundPanel.blocksRaycasts = false;

        // 3. Сохранение
        brightnessController.Save(brightnessSlider.value);
        PlayerPrefs.SetFloat(MASTER_KEY, masterSlider.value);
        PlayerPrefs.SetFloat(MUSIC_KEY, musicSlider.value);
        PlayerPrefs.SetFloat(SFX_KEY, sfxSlider.value);
        PlayerPrefs.SetInt(INITIALIZED_KEY, 1);
        PlayerPrefs.Save();

        // 4. Следующая сцена
        LoadNextScene();
    }

    // --- Тревожный фейд-ин: темнота -> флики -> долгий тяжёлый фейд ---
    private Sequence TheatricalFadeIn(CanvasGroup panel, RectTransform rect)
    {
        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);

        seq.AppendInterval(darknessPause);

        if (!string.IsNullOrEmpty(anxietyStingEvent))
            seq.AppendCallback(() => RuntimeManager.PlayOneShot(anxietyStingEvent));

        // флики — рваные скачки alpha перед основным появлением
        seq.Append(panel.DOFade(0.15f, 0.06f));
        seq.Append(panel.DOFade(0.03f, 0.05f));
        seq.Append(panel.DOFade(0.4f, 0.07f));
        seq.Append(panel.DOFade(0.08f, 0.05f));

        if (rect != null)
        {
            seq.Join(rect.DOShakeAnchorPos(0.35f, strength: 6f, vibrato: 20, randomness: 90, fadeOut: true));
        }

        // основной долгий тяжёлый фейд
        seq.Append(panel.DOFade(1f, theatricalFadeDuration).SetEase(Ease.InOutSine));

        seq.OnComplete(() =>
        {
            panel.interactable = true;
            panel.blocksRaycasts = true;
        });

        return seq;
    }

    // --- Спокойный кроссфейд между панелями ---
    private IEnumerator CalmCrossfade(CanvasGroup from, CanvasGroup to)
    {
        from.interactable = false;
        from.blocksRaycasts = false;

        yield return from.DOFade(0f, calmFadeDuration).SetEase(Ease.InOutSine).SetUpdate(true).WaitForCompletion();
        from.gameObject.SetActive(false);

        to.gameObject.SetActive(true);
        to.alpha = 0f;

        yield return to.DOFade(1f, calmFadeDuration).SetEase(Ease.InOutSine).SetUpdate(true).WaitForCompletion();
        to.interactable = true;
        to.blocksRaycasts = true;
    }

    private void ApplyVolumes(float master, float music, float sfx)
    {
        if (masterBus.isValid()) masterBus.setVolume(master);
        if (musicBus.isValid()) musicBus.setVolume(music);
        if (sfxBus.isValid()) sfxBus.setVolume(sfx);
    }

    private void SetPanelInstant(CanvasGroup panel, float alpha, bool interactable)
    {
        panel.alpha = alpha;
        panel.interactable = interactable;
        panel.blocksRaycasts = interactable;
        panel.gameObject.SetActive(alpha > 0f);
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            SceneManager.LoadScene(nextIndex);
        }
    }
}