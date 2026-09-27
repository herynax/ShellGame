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

        [Tooltip("Множитель задержки принятия решения врагом. <1 = решение принимается быстрее (сейчас: враг в целом чуть расторопнее игрока).")]
        [SerializeField, Range(0.1f, 1.5f)] private float _decisionSpeedMultiplier = 0.9f;

        [Tooltip("Дополнительный множитель, когда враг НЕ использовал предметы в этом решении. 0.75 = на 25% быстрее, чем если бы предмет использовался.")]
        [SerializeField, Range(0.1f, 1.5f)] private float _noItemDecisionSpeedMultiplier = 0.75f;

        private readonly EnemyKnowledgeModel _knowledge = new EnemyKnowledgeModel();
        private float _currentDifficultyIndex;
        private bool _isFirstLevel;
        private float _currentHealthFraction = 1f;

        private float _trackingLossReductionMultiplier = 1f;
        private bool _isTrackingSwaps;

        // true, если с последнего решения враг уже потратил предмет. Снимает
        // бонус скорости "решил без предметов" (см. DecisionRoutine).
        private bool _itemUsedThisDecision;

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
            Debug.Log($"[EnemyAI] ForceCorrectChoice ENABLED persistent={persistent} state={State} difficulty={_currentDifficultyIndex:F2}");
        }


        /// <summary>Снимает форс, включённый через ForceCorrectChoice — возвращает обычное поведение ИИ.</summary>
        public void ClearForcedChoice()
        {
            _forceCorrectChoice = false;
            _forcedChoicePersistent = false;
            Debug.Log($"[EnemyAI] ForceCorrectChoice CLEARED state={State}");
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
        public void EnterObserveMarkers(IReadOnlyList<Shell> shells, float difficultyIndex, bool isFirstLevel = false)
        {
            State = EnemyAIState.ObserveMarkers;
            _currentDifficultyIndex = difficultyIndex;
            _isFirstLevel = isFirstLevel;
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
            _trackingLossReductionMultiplier = 1f; // одноразовый эффект — сгорает после этого перемешивания, использовалось оно или нет
        }

        private void OnCupSwap(int slotA, int slotB)
        {
            _knowledge.OnCupSwap(slotA, slotB, _currentDifficultyIndex, _config, _trackingLossReductionMultiplier);
        }

        /// <summary>Предмет "Наркотики" в руках противника: снижает шанс потерять отслеживание метки на СЛЕДУЮЩЕМ перемешивании (multiplier — во сколько раз, 0.3 = "-70% шанса потерять"). Сгорает после одного перемешивания независимо от того, потребовалось оно или нет.</summary>
        public void ReduceTrackingLossNextShuffle(float multiplier)
        {
            _trackingLossReductionMultiplier = Mathf.Clamp01(multiplier);
        }

        public bool CanReduceTrackingLossNextShuffle() => Mathf.Approximately(_trackingLossReductionMultiplier, 1f);

        public void ResetDrugEffects()
        {
            if (_isTrackingSwaps)
            {
                _isTrackingSwaps = false;
                GameEvents.CupSwapPerformed -= OnCupSwap;
            }

            _trackingLossReductionMultiplier = 1f;
        }

        public float GetTrackedKnowledgeFraction() => _knowledge.GetTrackedFraction();


        public float GetItemUseDesireThreshold(float difficultyIndex) =>
            _config != null ? _config.EvaluateItemUseDesireThreshold(difficultyIndex) : 0.5f;

        public float GetItemUseThinkingDuration() =>
            _config != null ? Mathf.Max(0f, _config.ItemUseThinkingDuration) : 0f;

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
        public void MakeDecisionAndAttack(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen, bool afterItemUse = false)
        {
            if (afterItemUse)
                _itemUsedThisDecision = true;

            State = EnemyAIState.Decision;
            StartCoroutine(DecisionRoutine(shells, onShellChosen));
        }

        /// <summary>
        /// Решение для предмета, которому нужен ПУСТОЙ наперсток (молоток).
        /// В отличие от MakeDecisionAndAttack враг ищет не метку, а наоборот —
        /// слот, который по его Knowledge скорее всего пуст (см.
        /// EnemyKnowledgeModel.GetMarkerBelief). Точность оценивается той же
        /// кривой, что и у обычного выбора, поэтому враг и здесь может
        /// ошибиться и влепить себе молотком в лицо.
        ///
        /// Решение намеренно асинхронное, с той же задержкой "раздумья", что и
        /// обычный выбор: предмет, который бьёт по напертку, должен выбрать его
        /// ДО того, как GameManager запустит следующий обычный ход врага
        /// (см. EnemyStrikeSettleSeconds у HammerItemDefinition).
        /// </summary>
        public void MakeDecisionAndPickEmpty(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen)
        {
            _itemUsedThisDecision = true;
            State = EnemyAIState.Decision;
            StartCoroutine(PickEmptyShellRoutine(shells, onShellChosen));
        }

        /// <summary>
        /// Задержка перед ближайшим решением ИИ с поправкой на множители
        /// скорости. Предметы, которые сами выбирают наперсток, берут её отсюда,
        /// чтобы GameManager мог отложить обычный ход врага ровно на столько.
        /// </summary>
        public float GetDecisionDelay()
        {
            if (_config == null)
                return 0f;

            return _config.EvaluateDecisionDelay(_currentDifficultyIndex)
                * _decisionSpeedMultiplier
                * (_itemUsedThisDecision ? 1f : _noItemDecisionSpeedMultiplier);
        }

        private IEnumerator PickEmptyShellRoutine(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen)
        {
            float delay = GetDecisionDelay();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            State = EnemyAIState.Attack;
            onShellChosen?.Invoke(PickEmptyShell(shells));
        }

        /// <summary>Слот, который противник считает самым вероятным пустым, с той же долей ошибки, что и у обычного выбора.</summary>
        private Shell PickEmptyShell(IReadOnlyList<Shell> shells)
        {
            if (shells == null || shells.Count == 0)
                return null;

            var bestCandidates = new List<Shell>();
            float bestBelief = float.PositiveInfinity;

            foreach (var shell in shells)
            {
                if (shell == null)
                    continue;

                float belief = _knowledge.GetMarkerBelief(shell.SlotIndex);
                if (belief < bestBelief - 0.001f)
                {
                    bestBelief = belief;
                    bestCandidates.Clear();
                    bestCandidates.Add(shell);
                }
                else if (Mathf.Approximately(belief, bestBelief))
                {
                    bestCandidates.Add(shell);
                }
            }

            if (bestCandidates.Count == 0)
                return shells[0];

            float errorProbability = _config != null
                ? _config.EvaluateDecisionErrorProbability(_currentDifficultyIndex, _currentHealthFraction, _isFirstLevel)
                : 0f;

            if (UnityEngine.Random.value < errorProbability)
                return shells[UnityEngine.Random.Range(0, shells.Count)];

            return bestCandidates[UnityEngine.Random.Range(0, bestCandidates.Count)];
        }

        private IEnumerator DecisionRoutine(IReadOnlyList<Shell> shells, Action<Shell> onShellChosen)
        {
            bool usedItemThisDecision = _itemUsedThisDecision;
            _itemUsedThisDecision = false;

            float baseDelay = _config != null ? _config.EvaluateDecisionDelay(_currentDifficultyIndex) : 0f;
            float delay = baseDelay * _decisionSpeedMultiplier
                * (usedItemThisDecision ? 1f : _noItemDecisionSpeedMultiplier);

            int markedSlotIndex = FindMarkedShell(shells)?.SlotIndex ?? -1;
            Debug.Log($"[EnemyAI] Decision START delay={delay:F2} baseDelay={baseDelay:F2} usedItem={usedItemThisDecision} forceCorrect={_forceCorrectChoice} persistent={_forcedChoicePersistent} healthFraction={_currentHealthFraction:F2} difficulty={_currentDifficultyIndex:F2} markedSlot={markedSlotIndex}");
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            Shell targetShell;
            string decisionMode;
            float errorProbability = -1f;
            float errorRoll = -1f;

            if (_forceCorrectChoice)
            {
                // Форс: игнорируем Knowledge и вероятности — берём реально
                // помеченный наперсток напрямую из состояния поля.
                targetShell = FindMarkedShell(shells) ?? shells[UnityEngine.Random.Range(0, shells.Count)];
                decisionMode = "FORCED_CORRECT";

                if (!_forcedChoicePersistent)
                {
                    _forceCorrectChoice = false;
                    Debug.Log("[EnemyAI] ForceCorrectChoice CONSUMED");
                }
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

                errorProbability = _config != null
                    ? _config.EvaluateDecisionErrorProbability(
                        _currentDifficultyIndex, _currentHealthFraction, _isFirstLevel)
                    : 0f;
                errorRoll = UnityEngine.Random.value;
                bool madeError = errorRoll < errorProbability;
                if (madeError)
                {
                    targetSlotIndex = shells[UnityEngine.Random.Range(0, shells.Count)].SlotIndex;
                }

                targetShell = FindShellBySlot(shells, targetSlotIndex) ?? shells[UnityEngine.Random.Range(0, shells.Count)];
                decisionMode = madeError ? "ERROR_REROLL" : "TRACKED_CHOICE";
            }

            Debug.Log($"[EnemyAI] Decision RESULT mode={decisionMode} targetSlot={targetShell?.SlotIndex} markedSlot={markedSlotIndex} pError={errorProbability:F3} roll={errorRoll:F3} forceCorrectAfter={_forceCorrectChoice}");
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
