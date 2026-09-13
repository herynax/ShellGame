using System.Collections;
using UnityEngine;
using FMODUnity;

public class SoundSettingsManager : MonoBehaviour, ISettingsModule
{
    public static SoundSettingsManager Instance { get; private set; }

    [SerializeField] private string masterBusPath = "bus:/";
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private FMOD.Studio.Bus masterBus, musicBus, sfxBus;
    private Vector3 _snapshot;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;
    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        while (!RuntimeManager.IsInitialized) yield return null;
        while (!RuntimeManager.HaveAllBanksLoaded) yield return null;

        masterBus = RuntimeManager.GetBus(masterBusPath);
        musicBus = RuntimeManager.GetBus(musicBusPath);
        sfxBus = RuntimeManager.GetBus(sfxBusPath);

        IsReady = true;

        LoadAndApply();
        SettingsSaveController.Instance?.RegisterModule(this);
    }

    public void LoadAndApply()
    {
        MasterVolume = PlayerPrefs.GetFloat(MASTER_KEY, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        SfxVolume = PlayerPrefs.GetFloat(SFX_KEY, 1f);
        ApplyVolumes(MasterVolume, MusicVolume, SfxVolume);
    }

    private void ApplyVolumes(float master, float music, float sfx)
    {
        if (masterBus.isValid()) masterBus.setVolume(master);
        if (musicBus.isValid()) musicBus.setVolume(music);
        if (sfxBus.isValid()) sfxBus.setVolume(sfx);
    }

    public void SetMasterVolume(float value) { MasterVolume = value; ApplyVolumes(MasterVolume, MusicVolume, SfxVolume); }
    public void SetMusicVolume(float value) { MusicVolume = value; ApplyVolumes(MasterVolume, MusicVolume, SfxVolume); }
    public void SetSfxVolume(float value) { SfxVolume = value; ApplyVolumes(MasterVolume, MusicVolume, SfxVolume); }

    public void PlaySfxPreview(string eventRef)
    {
        if (!string.IsNullOrEmpty(eventRef))
            RuntimeManager.PlayOneShot(eventRef);
    }

    // --- ISettingsModule ---
    public void CaptureSnapshot() => _snapshot = new Vector3(MasterVolume, MusicVolume, SfxVolume);

    public void Save()
    {
        PlayerPrefs.SetFloat(MASTER_KEY, MasterVolume);
        PlayerPrefs.SetFloat(MUSIC_KEY, MusicVolume);
        PlayerPrefs.SetFloat(SFX_KEY, SfxVolume);
    }

    public void Revert()
    {
        MasterVolume = _snapshot.x;
        MusicVolume = _snapshot.y;
        SfxVolume = _snapshot.z;
        ApplyVolumes(MasterVolume, MusicVolume, SfxVolume);
    }
}