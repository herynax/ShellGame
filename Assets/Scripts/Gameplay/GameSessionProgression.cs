using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class GameSessionProgression : MonoBehaviour
    {
        public static GameSessionProgression Instance { get; private set; }

        public int CompletedRoundsInSession { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public float CurrentDifficultyIndex { get; private set; }
        public int MaxShellsPenalty { get; private set; }

        /// <summary>
        /// Сколько боёв (энциантеров) пройдено в текущем забеге. Именно эта
        /// величина, а не CurrentLevelIndex, задаёт положение на линии
        /// сложности: LevelIndex растёт по той же причине, но оставлен как
        /// «индекс уровня» для обратной совместимости с чекпоинтами и ГДД.
        /// </summary>
        public int EncountersClearedInRun { get; private set; }

        /// <summary>Сколько раундов сыграно в ТЕКУЩЕМ бою. Сбрасывается на
        /// каждом новом энкаунтере — в отличие от CompletedRoundsInSession.</summary>
        public int RoundsInCurrentEncounter { get; private set; }

        private RunDifficultyConfig _runDifficultyConfig;
        private bool _runDifficultyConfigResolved;

        /// <summary>
        /// Конфиг формы рана. Подгружается из Resources/Configs/RunDifficultyConfig
        /// лениво; если ассета нет — работают дефолты в самом классе
        /// (5 карт, 10 боёв на карту, 4 минибосса, DEnd=45).
        /// </summary>
        public RunDifficultyConfig RunDifficulty
        {
            get
            {
                if (!_runDifficultyConfigResolved)
                {
                    _runDifficultyConfig = RunDifficultyConfig.Load();
                    _runDifficultyConfigResolved = true;
                }
                return _runDifficultyConfig;
            }
            set
            {
                _runDifficultyConfig = value;
                _runDifficultyConfigResolved = true;
            }
        }

        /// <summary>
        /// Одноразовый флаг: "следующая загружаемая игровая сцена должна
        /// восстановить состояние из RunCheckpointStorage вместо обычного
        /// свежего старта". Выставляется MainMenuController перед загрузкой
        /// сцены по кнопке "Продолжить попытку", потребляется (сбрасывается)
        /// в GameManager.Start() сразу же — поэтому обычная внутриигровая
        /// смена уровня никогда его не видит и никогда не восстанавливается
        /// вместо честной генерации нового раунда.
        /// </summary>
        public bool PendingContinueFromCheckpoint { get; set; }

        private DifficultyCatalog _difficultyCatalog;
        private bool _difficultyCatalogResolved;
        private DifficultyPreset _selectedPreset;

        /// <summary>
        /// Каталог пресетов сложности. Грузится из
        /// Resources/Configs/Difficulty/DifficultyCatalog лениво.
        /// </summary>
        public DifficultyCatalog Difficulty
        {
            get
            {
                if (!_difficultyCatalogResolved)
                {
                    _difficultyCatalog = DifficultyCatalog.Load();
                    _difficultyCatalogResolved = true;
                }
                return _difficultyCatalog;
            }
            set
            {
                _difficultyCatalog = value;
                _difficultyCatalogResolved = true;
            }
        }

        /// <summary>
        /// Выбранный пресет сложности. Если игрок не задал его явно — берётся
        /// «Средний» из каталога (или первый пресет, если такого нет).
        /// </summary>
        public DifficultyPreset SelectedPreset
        {
            get
            {
                if (_selectedPreset == null)
                {
                    var catalog = Difficulty;
                    if (catalog != null)
                        _selectedPreset = catalog.Default;
                }
                return _selectedPreset;
            }
            set => _selectedPreset = value;
        }

        /// <summary>Выбор пресета по Id (кнопки меню, восстановление чекпоинта).</summary>
        public void SetSelectedPresetById(string id)
        {
            var catalog = Difficulty;
            _selectedPreset = catalog != null ? (catalog.Get(id) ?? catalog.Default) : null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        public void IncrementCompletedRounds()
        {
            CompletedRoundsInSession++;
        }

        public void SetCompletedRounds(int count)
        {
            CompletedRoundsInSession = Mathf.Max(0, count);
        }

        public void SetEncountersClearedInRun(int count)
        {
            EncountersClearedInRun = Mathf.Max(0, count);
        }

        public void SetRoundsInCurrentEncounter(int count)
        {
            RoundsInCurrentEncounter = Mathf.Max(0, count);
        }

        public void SetCurrentLevelIndex(int levelIndex)
        {
            CurrentLevelIndex = Mathf.Max(0, levelIndex);
        }

        public void AddMaxShellsPenalty(int amount = 1)
        {
            MaxShellsPenalty += amount;
        }

        public void SetMaxShellsPenalty(int amount)
        {
            MaxShellsPenalty = Mathf.Max(0, amount);
        }

        /// <summary>
        /// Следующий энкаунтер забега: сдвигаемся на шаг по линии сложности и
        /// обнуляем внутрибоевой наклон. Вызывается из SceneLoader при победе
        /// над врагом.
        /// </summary>
        public void AdvanceToNextEncounter()
        {
            EncountersClearedInRun++;
            RoundsInCurrentEncounter = 0;
            SetCurrentLevelIndex(Mathf.Max(1, CurrentLevelIndex + 1));
            MaxShellsPenalty = 0; // Сбрасываем штраф на новом бою
            RecomputeDifficultyIndex();
        }

        /// <summary>Обратная совместимость со старыми вызовами.</summary>
        public void AdvanceToNextLevel() => AdvanceToNextEncounter();

        /// <summary>Один сыгранный раунд внутри текущего боя.</summary>
        public void AdvanceDifficultyForRound()
        {
            RoundsInCurrentEncounter++;
            CompletedRoundsInSession++;
            RecomputeDifficultyIndex();
        }

        public float GetDifficultyForRound(int levelIndex, int roundIndex, int completedRoundsBeforeCurrentRound)
        {
            // Параметры levelIndex/roundIndex/completedRoundsBeforeCurrentRound
            // больше не участвуют в формуле — сложность определяется позицией в
            // ране. Оставлены ради совместимости сигнатуры с GameManager.
            return RecomputeDifficultyIndex();
        }

        /// <summary>
        /// Пересчитывает CurrentDifficultyIndex по позиции в ране.
        ///     p = EncountersClearedInRun / EncountersTotal
        ///     D = DifficultyAtRunEnd · p^Gamma + IntraRamp · (rounds / RoundsPerEncounter)
        /// </summary>
        public float RecomputeDifficultyIndex()
        {
            var config = RunDifficulty;
            var preset = SelectedPreset;

            // Пресет задаёт конец оси (D при p=1). Если пресета/каталога нет —
            // работаем по значению RunDifficultyConfig (обратная совместимость).
            float difficultyAtRunEnd = preset != null
                ? preset.DifficultyAtRunEnd
                : (config != null ? config.DifficultyAtRunEnd : 45f);

            float baseDifficulty = config != null
                ? config.EvaluateBaseDifficulty(EncountersClearedInRun, RoundsInCurrentEncounter, difficultyAtRunEnd)
                : FallbackBaseDifficulty(EncountersClearedInRun, RoundsInCurrentEncounter, difficultyAtRunEnd);

            CurrentDifficultyIndex = Mathf.Max(0f, baseDifficulty);
            return CurrentDifficultyIndex;
        }

        /// <summary>
        /// Аварийная формула на случай отсутствия RunDifficultyConfig-ассета.
        /// Держит ту же форму «ровная линия 0..DEnd на 55 энкаунтеров».
        /// </summary>
        private static float FallbackBaseDifficulty(int encountersCleared, int roundsInEncounter, float difficultyAtRunEnd)
        {
            const float fallbackIntraRamp = 3f;
            const float fallbackRounds = 6f;

            float progress = Mathf.Clamp01(encountersCleared / 55f);
            float ramp = fallbackIntraRamp * Mathf.Clamp01(roundsInEncounter / fallbackRounds);
            return difficultyAtRunEnd * progress + ramp;
        }

        public void SetDifficultyIndex(float difficultyIndex)
        {
            CurrentDifficultyIndex = Mathf.Max(0f, difficultyIndex);
        }

        public void Reset()
        {
            CompletedRoundsInSession = 0;
            CurrentLevelIndex = 0;
            CurrentDifficultyIndex = 0f;
            MaxShellsPenalty = 0; // Сбрасываем при рестарте забега
            EncountersClearedInRun = 0;
            RoundsInCurrentEncounter = 0;
            PendingContinueFromCheckpoint = false;
            _selectedPreset = null; // новая попытка — снова «Средний»
        }
    }
}