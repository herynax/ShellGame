/// <summary>
/// Реализует любой контроллер параметра настроек (яркость, FOV, VSync/FPS, звук и т.д.),
/// который должен участвовать в системе "сохранить / отменить изменения".
/// </summary>
public interface ISettingsModule
{
    /// <summary>Запомнить текущее значение как "точку отката" — вызывается при открытии настроек.</summary>
    void CaptureSnapshot();

    /// <summary>Зафиксировать текущее (уже применённое превью) значение в PlayerPrefs.</summary>
    void Save();

    /// <summary>Откатить применённое превью обратно к значению из CaptureSnapshot().</summary>
    void Revert();
}