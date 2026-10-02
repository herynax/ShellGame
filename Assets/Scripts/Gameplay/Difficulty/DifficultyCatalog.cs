using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Список пресетов сложности. Лежит в
    /// Resources/Configs/Difficulty/DifficultyCatalog.asset. Выбранный пресет
    /// хранит GameSessionProgression (и, для «Продолжить», чекпоинт).
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyCatalog", menuName = "ShellGame/Gameplay/Difficulty Catalog")]
    public sealed class DifficultyCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Configs/Difficulty/DifficultyCatalog";

        public List<DifficultyPreset> Presets = new List<DifficultyPreset>();

        [Tooltip("Кто выбирается, если игрок не задал сложность (по умолчанию — «Средний»).")]
        public string DefaultPresetId = "Medium";

        public DifficultyPreset Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return Presets.Find(p => p != null && p.Id == id);
        }

        public DifficultyPreset Default =>
            Get(DefaultPresetId) ?? (Presets.Count > 0 ? Presets[0] : null);

        public static DifficultyCatalog Load() =>
            Resources.Load<DifficultyCatalog>(ResourcesPath);
    }
}
