using UnityEngine;
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
    /// Персистентный трекер статистики ТЕКУЩЕГО ЗАБЕГА (от входа в Tutorial до смерти игрока).
    /// Аналог GameSessionProgression, но не сбрасывается между уровнями —
    /// только при новом старте забега (Tutorial-сцена).
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

        public float Accuracy => PlayerMoves > 0
            ? (float)(PlayerMoves - Mistakes) / PlayerMoves
            : 0f;

        public float ElapsedTime => _isRunning
            ? Time.realtimeSinceStartup - _runStartTime
            : _cachedElapsed;

        private float _runStartTime;
        private float _cachedElapsed;
        private bool _isRunning;

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

        /// <summary>Вызывать при входе в Tutorial-сцену (старт нового забега).</summary>
        public void StartRun()
        {
            _runStartTime = Time.realtimeSinceStartup;
            _cachedElapsed = 0f;
            _isRunning = true;

            TotalMoves = 0;
            PlayerMoves = 0;
            Mistakes = 0;
            EnemiesDefeated = 0;
            BestStreak = 0;
            CurrentStreak = 0;
        }

        /// <summary>Останавливает секундомер (вызывать в момент смерти игрока).</summary>
        public void StopClock()
        {
            if (!_isRunning) return;
            _cachedElapsed = Time.realtimeSinceStartup - _runStartTime;
            _isRunning = false;
        }

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