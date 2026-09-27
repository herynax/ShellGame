using System.Collections;
using System.Collections.Generic;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using ShellGame.Tutorial;
using UnityEngine;

namespace ShellGame.Dialogue
{
    /// <summary>
    /// Показывает реплики врага по правилам из EnemyReactionConfig.
    ///
    /// Что делает:
    /// — слушает игровые события (открытие напёртка, использование предмета,
    ///   урон/смерть) и сам решает, что враг скажет: реплики срабатывают не на
    ///   каждое действие, а по правилам конфига + общему шансу и антиспаму;
    /// — на время реплик берёт GameplayGate (раунд-луп стоит, ввод игроку не
    ///   возвращается) и EnemyReactionGate (кто-то может подождать конца
    ///   реплик — например, чтобы враг не использовал второй предмет, не дав
    ///   игроку прочитать комментарий к первому);
    /// — на смерти стороны держит SceneTransitionGate, поэтому следующий
    ///   уровень не начинается, пока враг не договорит.
    ///
    /// Интеграция для нового врага: создать EnemyReactionConfig (правый клик
    /// в Project → Create → ShellGame → Dialogue → Enemy Reaction Config),
    /// положить в Resources/Configs/EnemyReactions и указать сцену в Scenes —
    /// директора поднимет EnemyReactionBootstrap. Либо повесить этот компонент
    /// вручную и назначить конфиг в инспекторе.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyReactionDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("Реплики и правила их показа. Обязателен.")]
        private EnemyReactionConfig _config;

        [SerializeField, Min(1), Tooltip("Сколько реплик может ждать в очереди, пока говорит предыдущая.")]
        private int _maxQueuedReactions = 2;

        [SerializeField, Min(0f), Tooltip("Сколько максимум ждать звука урона, прежде чем всё равно прокомментировать попадание.")]
        private float _damageSoundLeadIn = 2.5f;

        [SerializeField, Min(0f), Tooltip("Страховка: сколько ждать готовности стола (кнопки «Начать игру»), прежде чем начать вступление.")]
        private float _tableReadyTimeout = 6f;

        [SerializeField, Min(0f), Tooltip("Пауза между предложениями врага поверх отзума. 0 = следующая реплика сразу после отзума.")]
        private float _afterLinePause;

        [SerializeField, Min(0f), Tooltip("Страховка: сколько максимум ждать отзум камеры, чтобы реплики не висели навсегда. 0 = без ограничения.")]
        private float _cameraResetTimeout = 1.5f;

        private readonly List<EnemyReactionQuery> _queue = new List<EnemyReactionQuery>();
        private readonly Dictionary<int, float> _ruleShownAt = new Dictionary<int, float>();

        private bool _waitingForDamageSound;

        private GameManager _gameManager;
        private HealthController _health;
        private IDialogueService _dialogue;
        private bool _dialogueMissing;

        private bool _isShowing;
        private bool _levelEnded;
        private bool _introStarted;
        private bool _pauseBlockedByUs;

        private TurnSide _choosingSide = TurnSide.Player;
        private TurnSide _lastChooser = TurnSide.Enemy;
        private float _lastDecisionSeconds = -1f;
        private int _turnNumber;
        private int _missStreak;
        private int _hitStreak;
        private int _linesShownThisTurn;
        private float _lastLineShownAt = float.NegativeInfinity;

        private bool _playerTurnActive;
        private bool _enemyTurnActive;
        private bool _thinkingWarned;
        private float _turnStartTime;

        private ItemDefinition _pendingItem;
        private TurnSide _pendingItemUser = TurnSide.Player;

        public EnemyReactionConfig Config => _config;

        /// <summary>Конфиг ставит EnemyReactionBootstrap, ручной вариант — инспектор.</summary>
        public void SetConfig(EnemyReactionConfig config) => _config = config;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[EnemyReactionDirector] На '{name}' не назначен EnemyReactionConfig — директор выключен.", this);
                enabled = false;
                return;
            }

            // Готовим вступление сразу же: держим гейт реакций с момента появления,
            // чтобы GameManager не дал начать раунд раньше, чем враг поздоровается
            // (он поднимается на первый же кадр после загрузки сцены).
            _isShowing = true;
            EnemyReactionGate.SetBusy(this, true);
            BlockPause();
        }

        private void OnEnable()
        {
            GameEvents.ShellSelected += OnShellSelected;
            GameEvents.ShellRevealed += OnShellRevealed;
            GameEvents.DamageTaken += OnDamageTaken;
            ItemUseEvents.ItemUsed += OnItemUsed;
            ItemUseEvents.ItemSelfHit += OnItemSelfHit;
        }

        private void OnDisable()
        {
            GameEvents.ShellSelected -= OnShellSelected;
            GameEvents.ShellRevealed -= OnShellRevealed;
            GameEvents.DamageTaken -= OnDamageTaken;
            ItemUseEvents.ItemUsed -= OnItemUsed;
            ItemUseEvents.ItemSelfHit -= OnItemSelfHit;

            _queue.Clear();
            EnemyReactionGate.SetBusy(this, false);
            GameplayGate.Release(this);
            SceneTransitionGate.Release(this);
            UnblockPause();
        }

        private void Start()
        {
            if (_config == null || _introStarted)
                return;

            _introStarted = true;
            StartCoroutine(IntroRoutine());
        }

        private GameManager GameManagerRef
        {
            get
            {
                if (_gameManager == null)
                    _gameManager = FindFirstObjectByType<GameManager>();
                return _gameManager;
            }
        }

        private HealthController HealthRef
        {
            get
            {
                if (_health == null)
                    _health = FindFirstObjectByType<HealthController>();
                return _health;
            }
        }

        // ================================================================
        //  ИГРОВЫЕ СОБЫТИЯ
        // ================================================================

        private void Update()
        {
            if (_levelEnded)
                return;

            var gameManager = GameManagerRef;
            if (gameManager == null)
                return;

            bool isPlayerTurn = gameManager.State == RoundState.PlayerTurn
                                && gameManager.ActiveSide == TurnSide.Player;
            bool isEnemyTurn = gameManager.State == RoundState.PlayerTurn
                               && gameManager.ActiveSide == TurnSide.Enemy;

            if (isPlayerTurn && !_playerTurnActive)
                BeginPlayerTurn();
            else if (!isPlayerTurn && _playerTurnActive)
                _playerTurnActive = false;

            if (isEnemyTurn && !_enemyTurnActive)
                BeginEnemyTurn();
            else if (!isEnemyTurn && _enemyTurnActive)
                _enemyTurnActive = false;

            if (isPlayerTurn && !_thinkingWarned && !_isShowing)
                TryWarnAboutLongThinking();
        }

        private void BeginPlayerTurn()
        {
            _playerTurnActive = true;
            _turnStartTime = Time.unscaledTime;
            _thinkingWarned = false;
            _pendingItem = null;

            Enqueue(BuildQuery(EnemyReactionContext.PlayerTurnStarted, TurnSide.Player));
        }

        private void BeginEnemyTurn()
        {
            _enemyTurnActive = true;
            _pendingItem = null;

            Enqueue(BuildQuery(EnemyReactionContext.EnemyTurnStarted, TurnSide.Enemy));
        }

        private void TryWarnAboutLongThinking()
        {
            float warningDelay = _config != null ? _config.PlayerThinkingWarningSeconds : 0f;
            if (warningDelay <= 0f || Time.unscaledTime - _turnStartTime < warningDelay)
                return;

            _thinkingWarned = true;
            Enqueue(BuildQuery(EnemyReactionContext.PlayerThinking, TurnSide.Player));
        }

        /// <summary>
        /// Счётчик ходов игрока и время решения считаем здесь, а не по
        /// RoundState: игрок может открыть напёрток быстрее, чем Update
        /// директора заметит смену хода, и тогда «первая» реплика уровня
        /// (MinTurn/MaxTurn = 1) потерялась бы.
        /// </summary>
        private void OnShellSelected(Shell shell)
        {
            var gameManager = GameManagerRef;
            if (gameManager != null)
                _choosingSide = gameManager.ActiveSide;

            if (_choosingSide != _lastChooser)
            {
                if (_choosingSide == TurnSide.Player)
                {
                    _turnNumber++;
                    _linesShownThisTurn = 0;
                    _turnStartTime = Time.unscaledTime;
                    _thinkingWarned = false;
                }

                _lastChooser = _choosingSide;
            }

            _lastDecisionSeconds = _choosingSide == TurnSide.Player
                ? Time.unscaledTime - _turnStartTime
                : -1f;
        }

        private void OnShellRevealed(Shell shell, bool hasMarker)
        {
            if (_choosingSide == TurnSide.Player)
            {
                if (hasMarker)
                {
                    _hitStreak++;
                    _missStreak = 0;
                }
                else
                {
                    _missStreak++;
                    _hitStreak = 0;
                }
            }

            var context = _choosingSide == TurnSide.Player
                ? EnemyReactionContext.PlayerRevealed
                : EnemyReactionContext.EnemyRevealed;

            var query = BuildQuery(context, _choosingSide);
            query.HasResult = true;
            query.HasMarker = hasMarker;
            query.DecisionSeconds = _lastDecisionSeconds;

            if (hasMarker && !_levelEnded)
            {
                // Попадание: момент вскрытия — это ещё не урон. Реплика про
                // попадание должна прозвучать ПОВЕРХ звука укола/попадания, а
                // не вместо него, поэтому ждём, пока урон реально применят.
                // Если урон почему-то не придёт — страховка _damageSoundLeadIn.
                StartCoroutine(CommentAfterDamageSound(query));
                return;
            }

            Enqueue(query);
        }

        private IEnumerator CommentAfterDamageSound(EnemyReactionQuery query)
        {
            _waitingForDamageSound = true;
            float timeout = Mathf.Max(0f, _damageSoundLeadIn);

            while (_waitingForDamageSound && !_levelEnded && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            _waitingForDamageSound = false;

            // Смерть/финал уровня перехватывают очередь — молчание уместнее.
            if (_levelEnded)
                yield break;

            Enqueue(query);
        }

        private void OnItemUsed(TurnSide side, ItemDefinition item)
        {
            _pendingItem = item;
            _pendingItemUser = side;

            var query = BuildQuery(EnemyReactionContext.ItemUsed, side);
            query.Item = item;
            query.ItemUser = side;
            Enqueue(query);
        }

        /// <summary>Нож/бросок попал в того, кто бросал. Предмет — последний использованный.</summary>
        private void OnItemSelfHit(TurnSide side)
        {
            var query = BuildQuery(EnemyReactionContext.ItemSelfHit, side);
            query.Item = _pendingItem;
            query.ItemUser = side;
            Enqueue(query);
        }

        /// <summary>
        /// Смерть любой стороны: держим переход между уровнями, чтобы финальные
        /// реплики врага прозвучали до затемнения и загрузки следующей сцены.
        /// Хук именно на DamageTaken, а не на SideDied — событие стреляет раньше
        /// (см. HealthController.ApplyDamage), поэтому холд успевает встать до
        /// того, как SceneLoader отреагирует на смерть.
        /// </summary>
        private void OnDamageTaken(TurnSide side, int amount, int current, int max, bool died)
        {
            // Звук урона уже пошёл (HealthController сначала играет звуки, потом
            // шлёт это событие) — значит, реплику про попадание можно начинать
            // поверх него, не перебивая.
            _waitingForDamageSound = false;

            if (!died || _levelEnded || SceneLoader.Instance == null)
                return;

            _levelEnded = true;
            _queue.Clear();
            SceneTransitionGate.Hold(this);

            var context = side == TurnSide.Enemy
                ? EnemyReactionContext.LevelEndVictory
                : EnemyReactionContext.LevelEndDefeat;

            var query = BuildQuery(context, side);
            query.DeadSide = side;
            StartCoroutine(LevelEndRoutine(query));
        }

        /// <summary>
        /// Вступление уровня. Гейт реплик к этому моменту уже занят в Awake
        /// (раунд не начнётся, пока враг не поздоровается), здесь — ожидание
        /// готовности стола, первая пауза и сам показ.
        /// </summary>
        private IEnumerator IntroRoutine()
        {
            // Вступление идёт ПОСЛЕ того, как игрок и стол окончательно
            // загрузились: ждём, пока спавнятся предметы и на стол выезжает
            // кнопка «Начать игру» (она в этот момент некликабельная).
            yield return WaitForTableReady();

            float delay = _config != null ? _config.FirstLineDelaySeconds : 0f;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var group = SelectGroup(BuildQuery(EnemyReactionContext.GameStart, TurnSide.Enemy), true);
            if (group == null)
            {
                ReleasePresentation();
                yield break;
            }

            GameplayGate.Block(this);
            _lastLineShownAt = Time.unscaledTime;
            _linesShownThisTurn++;

            yield return PresentLines(group);

            GameplayGate.Release(this);
            ReleasePresentation();
        }

        /// <summary>
        /// Ждёт кнопку «Начать игру» — это последний шаг спавна стола, значит
        /// к этому моменту уже готовы и игрок, и все предметы. Если кнопки в
        /// сцене нет (например, обучение), ждём не дольше _tableReadyTimeout.
        /// </summary>
        private IEnumerator WaitForTableReady()
        {
            var startButton = FindFirstObjectByType<RoundStartButton>();
            if (startButton == null)
                yield break;

            float timeout = _tableReadyTimeout;
            while (!startButton.IsShown && timeout > 0f && !_levelEnded)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void ReleasePresentation()
        {
            UnblockPause();
            EnemyReactionGate.SetBusy(this, false);
            _isShowing = false;
            TryProcessQueue();
        }

        // ================================================================
        //  ОЧЕРЕДЬ И ПОКАЗ
        // ================================================================

        private void Enqueue(EnemyReactionQuery query)
        {
            if (_levelEnded || _queue.Count >= Mathf.Max(1, _maxQueuedReactions))
                return;

            _queue.Add(query);
            TryProcessQueue();
        }

        private void TryProcessQueue()
        {
            if (_isShowing || _levelEnded)
                return;

            while (_queue.Count > 0)
            {
                var query = _queue[0];
                _queue.RemoveAt(0);

                var group = SelectGroup(query, false);
                if (group == null)
                    continue;

                StartCoroutine(PresentRoutine(group));
                return;
            }
        }

        private IEnumerator PresentRoutine(EnemyReactionGroup group)
        {
            _isShowing = true;
            EnemyReactionGate.SetBusy(this, true);
            GameplayGate.Block(this);
            BlockPause();

            _lastLineShownAt = Time.unscaledTime;
            _linesShownThisTurn++;

            yield return PresentLines(group);

            GameplayGate.Release(this);
            ReleasePresentation();
        }

        private IEnumerator LevelEndRoutine(EnemyReactionQuery query)
        {
            while (_isShowing)
                yield return null;

            _isShowing = true;
            EnemyReactionGate.SetBusy(this, true);

            // HealthController шлёт DamageTaken ДО старта звука смерти, поэтому
            // кадр ждём, чтобы финальная реплика легла ПОВЕРХ звука смерти,
            // а не заглушила его.
            yield return null;

            var group = SelectGroup(query, true);
            if (group != null)
                yield return PresentLines(group);

            _isShowing = false;
            EnemyReactionGate.SetBusy(this, false);

            SceneTransitionGate.Release(this);

            var loader = SceneLoader.Instance;
            if (loader != null)
                loader.ContinueAfterHeldSceneTransition(query.DeadSide);
        }

        private IEnumerator PresentLines(EnemyReactionGroup group)
        {
            if (group == null || group.Lines == null)
                yield break;

            if (!ServiceLocator.TryGet<IDialogueService>(out _dialogue))
            {
                if (!_dialogueMissing)
                {
                    _dialogueMissing = true;
                    Debug.LogWarning("[EnemyReactionDirector] На сцене нет DialogueView (IDialogueService) — реплики показать негде.", this);
                }

                yield break;
            }

            foreach (var line in group.Lines)
            {
                if (line == null || string.IsNullOrEmpty(line.Text))
                    continue;

                var runtimeLine = DialogueLine.CreateRuntime(
                    line.Text,
                    line.TextColor,
                    line.VoiceEvent,
                    line.MinDisplayDuration,
                    line.WaitForClick,
                    _config != null ? _config.ResolveAutoSkipSeconds(line) : 0f);

                yield return _dialogue.ShowLine(runtimeLine);
                Destroy(runtimeLine);

                // Отзум между предложениями: сначала камера возвращается из
                // зума (0.3 с), и только потом показывается следующая реплика —
                // так игрок видит, что это была одна мысль врага. Работает и
                // когда реплику закрыл клик игрока: зум уже ушёл на Hide().
                yield return new WaitCameraReset(_afterLinePause, _cameraResetTimeout).Run(this);
            }
        }

        private void BlockPause()
        {
            if (_pauseBlockedByUs || PauseController.Instance == null)
                return;

            _pauseBlockedByUs = true;
            PauseController.Instance.SetPauseBlocked(true);
        }

        private void UnblockPause()
        {
            if (!_pauseBlockedByUs)
                return;

            _pauseBlockedByUs = false;
            if (PauseController.Instance != null)
                PauseController.Instance.SetPauseBlocked(false);
        }

        // ================================================================
        //  ВЫБОР РЕПЛИКИ
        // ================================================================

        /// <summary>
        /// Возвращает группу реплик для этого запроса либо null (нечего
        /// говорить). force = true игнорирует шанс и антиспам — так
        /// показываются вступление, первый обмен и финал уровня.
        /// </summary>
        private EnemyReactionGroup SelectGroup(EnemyReactionQuery query, bool force)
        {
            if (_config == null || _config.Rules == null)
                return null;

            // Вступление и финал уровня — 100% шанс: их не отсекает даже
            // антиспам, сколько бы реплик враг ни успел произнести.
            bool critical = EnemyReactionContexts.IsCritical(query.Context);
            float now = Time.unscaledTime;
            var rules = _config.Rules;
            var candidates = new List<int>();
            int bestPriority = int.MinValue;

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null)
                    continue;

                _ruleShownAt.TryGetValue(i, out float lastShownAt);
                if (!EnemyReactionMatcher.IsMatch(rule, query, now, lastShownAt))
                    continue;

                if (rule.Priority > bestPriority)
                {
                    bestPriority = rule.Priority;
                    candidates.Clear();
                    candidates.Add(i);
                }
                else if (rule.Priority == bestPriority)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
                return null;

            int pickedIndex = candidates[Random.Range(0, candidates.Count)];
            var picked = rules[pickedIndex];

            bool frequencyGatePassed = force
                                       || critical
                                       || picked.AlwaysShow
                                       || _config.IsFrequencyGateOpen(now, _lastLineShownAt, _linesShownThisTurn);

            if (!frequencyGatePassed)
                return null;

            _ruleShownAt[pickedIndex] = now;
            return EnemyReactionMatcher.PickGroup(picked.Groups);
        }

        private EnemyReactionQuery BuildQuery(EnemyReactionContext context, TurnSide actor)
        {
            return new EnemyReactionQuery
            {
                Context = context,
                Actor = actor,
                HasResult = false,
                HasMarker = false,
                Item = _pendingItem,
                ItemUser = _pendingItemUser,
                TurnNumber = _turnNumber,
                DecisionSeconds = _lastDecisionSeconds,
                ConsecutivePlayerMisses = _missStreak,
                ConsecutivePlayerHits = _hitStreak,
                PlayerHealthFraction = GetRemainingHealthFraction(TurnSide.Player),
                EnemyHealthFraction = GetRemainingHealthFraction(TurnSide.Enemy),
                DeadSide = TurnSide.Player,
            };
        }

        private float GetRemainingHealthFraction(TurnSide side)
        {
            var health = HealthRef;
            if (health == null)
                return 1f;

            int max = health.GetMaxHealth(side);
            if (max <= 0)
                return 1f;

            return Mathf.Clamp01(1f - (float)health.GetHealth(side) / max);
        }
    }
}
