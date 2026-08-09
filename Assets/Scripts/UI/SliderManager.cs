using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using System.Collections;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("FMOD Paths")]
    [SerializeField] private string masterBusPath = "bus:/";
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    // PlayerPrefs Keys
    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus musicBus;
    private FMOD.Studio.Bus sfxBus;

    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private IEnumerator Start()
    {
        // Ждем инициализации FMOD
        while (!RuntimeManager.IsInitialized)
            yield return null;

        while (!RuntimeManager.HaveAllBanksLoaded)
            yield return null;

        // Получаем bus'ы
        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);

        // Загружаем сохраненные настройки
        LoadAndApplySettings();

        // Подписываемся на изменения slider'ов
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        isInitialized = true;
    }

    private void LoadAndApplySettings()
    {
        float master = PlayerPrefs.GetFloat(MASTER_KEY, 1f);
        float music = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        float sfx = PlayerPrefs.GetFloat(SFX_KEY, 1f);

        masterSlider.value = master;
        musicSlider.value = music;
        sfxSlider.value = sfx;

        ApplyVolumes(master, music, sfx);
    }

    private void ApplyVolumes(float master, float music, float sfx)
    {
        if (masterBus.isValid())
            masterBus.setVolume(master);

        if (musicBus.isValid())
            musicBus.setVolume(music);

        if (sfxBus.isValid())
            sfxBus.setVolume(sfx);
    }

    private void SetMasterVolume(float value)
    {
        if (!isInitialized) return;

        if (masterBus.isValid())
            masterBus.setVolume(value);

        PlayerPrefs.SetFloat(MASTER_KEY, value);
        PlayerPrefs.Save();
    }

    private void SetMusicVolume(float value)
    {
        if (!isInitialized) return;

        if (musicBus.isValid())
            musicBus.setVolume(value);

        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        PlayerPrefs.Save();
    }

    private void SetSFXVolume(float value)
    {
        if (!isInitialized) return;

        if (sfxBus.isValid())
            sfxBus.setVolume(value);

        PlayerPrefs.SetFloat(SFX_KEY, value);
        PlayerPrefs.Save();
    }
}
