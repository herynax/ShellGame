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
    /// активная сторона — противник. Это не нарушает принцип ГДД "FSM
    /// отвечает только за порядок состояний": порядок здесь просто совпадает
    /// с порядком стадий раунда, а не дублируется отдельным циклом.
    ///
    /// Расходуемые предметы противника (Пассатижи/Монокль/Молоток/Метка/
    /// Наркотики/Двойной урон) сюда пока не подключены — система предметов
    /// появляется со 2 уровня и требует отдельной итерации (инвентарь,
    /// CanUse/ShouldUse/IgnoreChance). Место для неё отмечено TODO в
    /// DecisionRoutine и не потребует изменений в остальном FSM.
    /// </summary>
    public sealed class EnemyAIController : MonoBehaviour
    {
        [SerializeField] private EnemyAIConfig _config;

        private readonly EnemyKnowledgeModel _knowledge = new EnemyKnowledgeModel();
        private float _currentDifficultyIndex;
        private bool _isTrackingSwaps;

        public EnemyAIState State { get; private set; } = EnemyAIState.Idle;

        public void Initialize(EnemyAIConfig config)
        {
            _config = config;
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
        /// Состояния Decision + Attack. Возвращает выбранный наперсток через
        /// onShellChosen — вызывающий код (GameManager) сам решает, что с ним
        /// делать (обычно — Shell.Select(), как и для игрока), чтобы вся
        /// дальнейшая обработка результата шла по единому пути.
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
            // обновление Knowledge, повтор цикла) — см. "Список расходуемых
            // предметов" и таблицы CanUse/ShouldUse в ГДД. Пока пропускается.

            float delay = _config.EvaluateDecisionDelay(_currentDifficultyIndex);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Этап 2: выбор цели среди отслеживаемых меток.
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

            // Этап 3: проверка вероятности ошибки — даже зная цель, ИИ может
            // отказаться от неё и выбрать случайный другой допустимый наперсток.
            float pError = _config.EvaluateDecisionErrorProbability(_currentDifficultyIndex);
            if (UnityEngine.Random.value < pError)
            {
                targetSlotIndex = shells[UnityEngine.Random.Range(0, shells.Count)].SlotIndex;
            }

            var targetShell = FindShellBySlot(shells, targetSlotIndex) ?? shells[UnityEngine.Random.Range(0, shells.Count)];

            State = EnemyAIState.Attack;
            onShellChosen?.Invoke(targetShell);
        }

        /// <summary>Состояние EndTurn — вызывается GameManager на стадии Cleanup, чисто для порядка/логирования состояния.</summary>
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

        private void OnDisable()
        {
            ExitTrackShuffle();
        }
    }
}
