using UnityEngine;
using UnityEngine.SceneManagement;
using ShellGame.Core;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Снапшот статистики забега для передачи в UI экрана смерти.
    /// </summary>
    public struct RunStatsSnapshot
    {
        public float ElapsedSeconds;
        public int TotalMoves;
        public int Mistakes;
        public int EnemiesDefeated;
        public int BestStreak;
        public float Accuracy; // 0..1, считается только по ходам игрока
    }

    /// <summary>
    /// Персистентный трекер статистики ТЕКУЩЕГО ЗАБЕГА.
    ///
    /// Жизненный цикл забега явный, чтобы статистика не протекала между
    /// попытками и не обнулялась посреди одной:
    ///   StartRun()          — новая попытка: всё в нули, секундомер идёт;
    ///   EnsureRunStarted()  — вызывается каждой игровой сценой: если забег уже
    ///                         идёт (переход между уровнями), ничего не трогает;
    ///   SuspendClock()      — секундомер встаёт (пауза по ESC, главное меню),
    ///                         забег при этом остаётся активным;
    ///   ResumeClock()       — секундомер продолжает с того же места;
    ///   EndRun()            — забег закончен (смерть/победа/рестарт): время
    ///                         замораживается, счётчики ждут нового StartRun().
    ///
    /// Секундомер считает только активную игру: время в паузе и в главном меню
    /// в забег не попадает.
    /// </summary>
    public sealed class RunStatsTracker : MonoBehaviour
    {
        public static RunStatsTracker Instance { get; private set; }

        public int TotalMoves { get; private set; }
        public int PlayerMoves { get; private set; }
        public int Mistakes { get; private set; }
        public int EnemiesDefeated { get; private set; }
        public int BestStreak { get; private set; }
        public int CurrentStreak { get; private set; }

        /// <summary>Идёт ли сейчас забег (между уровнями — да, после смерти/рестарта — нет).</summary>
        public bool IsRunActive => _runActive;

        /// <summary>Идёт ли секундомер: забег активен и не приостановлен паузой/меню.</summary>
        public bool IsClockRunning => _runActive && !_clockSuspended;

        public float Accuracy => PlayerMoves > 0
            ? (float)(PlayerMoves - Mistakes) / PlayerMoves
            : 0f;

        public float ElapsedTime => IsClockRunning
            ? _accumulatedSeconds + (Time.realtimeSinceStartup - _segmentStartTime)
            : _accumulatedSeconds;

        private float _accumulatedSeconds;
        private float _segmentStartTime;
        private bool _runActive;
        private bool _clockSuspended;

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

        /// <summary>Новая попытка: обнуляет всю статистику забега и запускает секундомер.</summary>
        public void StartRun()
        {
            _accumulatedSeconds = 0f;
            _segmentStartTime = Time.realtimeSinceStartup;
            _runActive = true;
            _clockSuspended = false;

            TotalMoves = 0;
            PlayerMoves = 0;
            Mistakes = 0;
            EnemiesDefeated = 0;
            BestStreak = 0;
            CurrentStreak = 0;

            Debug.Log($"[RunStatsTracker] Новый забег, отсчёт с нуля (сцена: {SceneManager.GetActiveScene().name}).");
        }

        /// <summary>
        /// Вызывается каждой игровой сценой. Новый забег (обучение или первый
        /// уровень) — стартует отсчёт с нуля; переход между уровнями внутри
        /// одного забега — оставляет накопленное как есть.
        /// </summary>
        public void EnsureRunStarted()
        {
            if (!_runActive)
            {
                StartRun();
                return;
            }

            ResumeClock();
        }

        /// <summary>Ставит секундомер на паузу, не обнуляя уже накопленное (ESC, главное меню).</summary>
        public void SuspendClock()
        {
            if (!IsClockRunning) return;

            _accumulatedSeconds += Time.realtimeSinceStartup - _segmentStartTime;
            _clockSuspended = true;
        }

        /// <summary>Продолжает секундомер с того же места. На счётчики не влияет.</summary>
        public void ResumeClock()
        {
            if (!_runActive || !_clockSuspended) return;

            _segmentStartTime = Time.realtimeSinceStartup;
            _clockSuspended = false;
        }

        /// <summary>Забег закончен: время замораживается до следующего StartRun().</summary>
        public void EndRun()
        {
            SuspendClock();
            _runActive = false;
        }

        /// <summary>
        /// Ход забега. Пустой напёрток (метки не оказалось) — это ошибка игрока,
        /// она попадает в счётчик ошибок забега; ошибки врага в зачёт не идут.
        /// </summary>
        public void RegisterMove(TurnSide side, bool wasHit)
        {
            TotalMoves++;
            if (side != TurnSide.Player) return;

            PlayerMoves++;
            if (wasHit)
            {
                CurrentStreak++;
                BestStreak = Mathf.Max(BestStreak, CurrentStreak);
            }
            else
            {
                Mistakes++;
                CurrentStreak = 0;
            }
        }

        public void RegisterEnemyDefeated() => EnemiesDefeated++;

        public static RunStatsTracker EnsureExists()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("RunStatsTracker");
            return go.AddComponent<RunStatsTracker>();
        }
    }
}
