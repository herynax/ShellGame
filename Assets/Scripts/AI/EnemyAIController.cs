using System;
using System.Collections;
using System.Collections.Generic;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.AI
{
    /// <summary>
    /// Реализация поведения противника: Idle → ObserveMarkers → TrackShuffle →
    /// Decision → Attack → EndTurn.
    ///
    /// Архитектурная адаптация под уже существующий раунд-луп: раунд
    /// (Reveal/Shuffle/Turn/Cleanup) один на обе стороны дуэли и управляется
    /// GameManager. Поэтому этот класс не гоняет собственный игровой цикл —
    /// GameManager явно вызывает Enter-методы в те же моменты, когда обычный
    /// раунд показывает метки/перемешивает/ждёт выбора, но только если
    /// активная сторона — противник.
    ///
    /// Расходуемые предметы противника (кроме Монокля — см. ResyncKnowledge)
    /// сюда пока не подключены — см. TODO в DecisionRoutine.
    /// </summary>
    public sealed class EnemyAIController : MonoBehaviour
    {
        [SerializeField] private EnemyAIConfig _config;

        private readonly EnemyKnowledgeModel _knowledge = new EnemyKnowledgeModel();
        private float _currentDifficultyIndex;
        private float _currentHealthFraction = 1f;
        private bool _isTrackingSwaps;

        // --- Форсированный исход (для обучения/скриптованных сцен) ---
        private bool _forceCorrectChoice;
        private bool _forcedChoicePersistent;

        public EnemyAIState State { get; private set; } = EnemyAIState.Idle;

        public void Initialize(EnemyAIConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Заставляет противника на СЛЕДУЮЩЕМ решении выбрать реально
        /// помеченный наперсток — в обход Knowledge-модели и вероятностей
        /// Plose/Perror. Нужно для гарантированных сценариев туториала,
        /// где игрок должен увидеть попадание врага именно в этом раунде.
        ///
        /// Тайминг вызова: любое время ДО того, как GameManager вызовет
        /// MakeDecisionAndAttack для этого раунда (то есть до фактического
        /// хода противника) — например, сразу после смены активной стороны
        /// на Enemy (GameEvents.ActiveSideChanged).
        /// </summary>
        /// <param name="persistent">
        /// Если false (по умолчанию) — форс потребляется один раз и
        /// автоматически снимается после следующего решения. Если true —
        /// действует на все последующие решения, пока не будет вызван
        /// ClearForcedChoice().
        /// </param>
        public void ForceCorrectChoice(bool persistent = false)
        {
            _forceCorrectChoice = true;
            _forcedChoicePersistent = persistent;
        }

        /// <summary>Снимает форс, включённый через ForceCorrectChoice — возвращает обычное поведение ИИ.</summary>
        public void ClearForcedChoice()
        {
            _forceCorrectChoice = false;
            _forcedChoicePersistent = false;
        }

        /// <summary>
        /// Текущая доля HP противника (0..1), передаётся GameManager'ом перед
        /// каждым решением. Чем меньше HP — тем ниже точность решения (см.
        /// EnemyAIConfig.EvaluateHealthAccuracyPenalty) — аналог "поплывшего"
        /// экрана игрока от дозы, только выражен через шанс ошибки, а не
        /// визуально (у противника нет своего экрана).
        /// </summary>
        public void SetHealthFraction(float fraction01)
        {
            _currentHealthFraction = Mathf.Clamp01(fraction01);
        }

        /// <summary>Состояние ObserveMarkers — фиксируем реальную начальную раскладку меток.</summary>
        public void EnterObserveMarkers(IReadOnlyList<Shell> shells, float difficultyIndex)
        {
            State = EnemyAIState.ObserveMarkers;
            _currentDifficultyIndex = difficultyIndex;
            _knowledge.Reset();
            _knowledge.Observe(shells);
        }

        /// <summary>Состояние TrackShuffle — начинаем слушать события обмена наперстков.</summary>
        public void EnterTrackShuffle()
        {
            State = EnemyAIState.TrackShuffle;
            if (_isTrackingSwaps)
                return;

            _isTrackingSwaps = true;
            GameEvents.CupSwapPerformed += OnCupSwap;
        }

        /// <summary>Перемешивание завершено — прекращаем слушать обмены.</summary>
        public void ExitTrackShuffle()
        {
            if (!_isTrackingSwaps)
                return;

            _isTrackingSwaps = false;
            GameEvents.CupSwapPerformed -= OnCupSwap;
        }

        private void OnCupSwap(int slotA, int slotB)
        {
            _knowledge.OnCupSwap(slotA, slotB, _currentDifficultyIndex, _config);
        }

        /// <summary>
        /// Эффект предмета "Монокль" в руках противника — полностью
        /// пересобирает Knowledge из истинного состояния поля (упрощённая,
        /// но честная трактовка ГДД: "после использования состояние Knowledge
        /// обновляется"). Можно вызывать даже вне TrackShuffle.
        /// </summary>
        public void ResyncKnowledge(IReadOnlyList<Shell> shells)
        {
            _knowledge.Observe(shells);
        }

        /// <summary>
        /// Состояния Decision + Attack. Возвращает выбранный наперсток через
        /// onShellChosen — вызывающий код (GameManager) сам решает, что с ним
        /// делать (обычно — Shell.Select(), как и для игрока).
        /// </summary>
        public void MakeDecisionAndAttack(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen)
        {
            State = EnemyAIState.Decision;
            StartCoroutine(DecisionRoutine(shells, onShellChosen));
        }

        private IEnumerator DecisionRoutine(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen)
        {
            // TODO: здесь будет проход по инвентарю расходуемых предметов
            // противника (CanUse -> ShouldUse -> IgnoreChance -> Apply ->
            // обновление Knowledge, повтор цикла) — см. таблицы CanUse/ShouldUse
            // в ГДД. Монокль уже готов (ResyncKnowledge выше), остальные
            // предметы (Пассатижи/Молоток/Метка/Наркотики/Двойной урон)
            // потребуют инвентаря у противника — пока пропускается.

            float delay = _config.EvaluateDecisionDelay(_currentDifficultyIndex);
            Debug.Log($"[EnemyAI] DecisionRoutine стартовал, delay={delay}, forceCorrect={_forceCorrectChoice}, healthFraction={_currentHealthFraction:F2}");
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Shell targetShell;

            if (_forceCorrectChoice)
            {
                // Форс: игнорируем Knowledge и вероятности — берём реально
                // помеченный наперсток напрямую из состояния поля.
                targetShell = FindMarkedShell(shells) ?? shells[UnityEngine.Random.Range(0, shells.Count)];

                if (!_forcedChoicePersistent)
                    _forceCorrectChoice = false;
            }
            else
            {
                var tracked = _knowledge.GetTrackedEntries();
                int targetSlotIndex;
                if (tracked.Count > 0)
                {
                    var chosenEntry = tracked[UnityEngine.Random.Range(0, tracked.Count)];
                    targetSlotIndex = chosenEntry.CurrentSlotIndex;
                }
                else
                {
                    targetSlotIndex = shells[UnityEngine.Random.Range(0, shells.Count)].SlotIndex;
                }

                float pError = _config.EvaluateDecisionErrorProbability(_currentDifficultyIndex, _currentHealthFraction);
                if (UnityEngine.Random.value < pError)
                {
                    targetSlotIndex = shells[UnityEngine.Random.Range(0, shells.Count)].SlotIndex;
                }

                targetShell = FindShellBySlot(shells, targetSlotIndex) ?? shells[UnityEngine.Random.Range(0, shells.Count)];
            }

            Debug.Log($"[EnemyAI] Решение принято, targetShell slot={targetShell?.SlotIndex}");
            State = EnemyAIState.Attack;
            onShellChosen?.Invoke(targetShell);
        }

        public void EnterEndTurn()
        {
            State = EnemyAIState.EndTurn;
        }

        private static Shell FindShellBySlot(IReadOnlyList<Shell> shells, int slotIndex)
        {
            foreach (var shell in shells)
            {
                if (shell.SlotIndex == slotIndex)
                    return shell;
            }
            return null;
        }

        private static Shell FindMarkedShell(IReadOnlyList<Shell> shells)
        {
            foreach (var shell in shells)
            {
                if (shell.HasMarker)
                    return shell;
            }
            return null;
        }

    }
}