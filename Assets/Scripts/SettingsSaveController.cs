using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Живёт на ПЕРСИСТЕНТНОМ объекте (DontDestroyOnLoad), всегда активен.
/// Управляет всеми модулями настроек, загружает сохранённые значения на старте.
/// </summary>
public class SettingsSaveController : MonoBehaviour
{
    public static SettingsSaveController Instance { get; private set; }

    private readonly List<ISettingsModule> _modules = new List<ISettingsModule>();
    public bool IsDirty { get; private set; }

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

    /// <summary>
    /// Гарантирует существование персистентного контроллера настроек.
    /// Создаёт его как DontDestroyOnLoad-объект, если его нет.
    /// </summary>
    public static SettingsSaveController EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("SettingsSaveController");
        DontDestroyOnLoad(go);
        return go.AddComponent<SettingsSaveController>();
    }

    private void Start()
    {
        LoadAndApplyAll();
    }

    public void RegisterModule(ISettingsModule module)
    {
        if (!_modules.Contains(module))
        {
            _modules.Add(module);
            // Сразу же загружаем и применяем параметры для зарегистрировавшегося модуля
            module.LoadAndApply();
        }
    }

    public void UnregisterModule(ISettingsModule module)
    {
        _modules.Remove(module);
    }

    public void LoadAndApplyAll()
    {
        foreach (var module in _modules)
            module.LoadAndApply();
    }

    public void CaptureSnapshotAll()
    {
        IsDirty = false;
        foreach (var module in _modules)
            module.CaptureSnapshot();
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void SaveAll()
    {
        foreach (var module in _modules)
            module.Save();

        PlayerPrefs.Save();
        IsDirty = false;
        Debug.Log("[Settings] Все настройки успешно сохранены в PlayerPrefs.");
    }

    public void RevertAll()
    {
        foreach (var module in _modules)
            module.Revert();

        IsDirty = false;
        Debug.Log("[Settings] Несохранённые изменения отменены.");
    }
}

/// <summary>
/// Создаёт персистентные сервисы настроек с самого старта игры.
///
/// Раньше SettingsSaveController и SoundSettingsManager нигде не были прицеплены
/// к сценам/префабам, поэтому их Instance были null и вся система "сохранить /
/// отменить изменения" молча не работала: SaveAll()/MarkDirty() были no-op, а
/// SoundSettingsUI уходил по раннему return — слайдеры звука не реагировали вовсе.
/// </summary>
public static class SettingsServicesBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCoreSettingsServices()
    {
        SettingsSaveController.EnsureExists();
        SoundSettingsManager.EnsureExists();
    }
}