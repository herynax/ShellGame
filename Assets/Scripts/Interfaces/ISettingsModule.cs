/// <summary>
/// Реализует любой контроллер параметра настроек (яркость, FOV, VSync/FPS, звук и т.д.),
/// который должен участвовать в системе "сохранить / отменить изменения".
/// </summary>
public interface ISettingsModule
{
    void LoadAndApply();
    void CaptureSnapshot();
    void Save();
    void Revert();
}