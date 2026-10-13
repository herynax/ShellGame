using System.Collections;
using DG.Tweening;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using ShellGame.Tutorial;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace ShellGame.Run
{
    public sealed class EncounterHost : MonoBehaviour
    {
        [SerializeField] private Transform _rigParent;
        [SerializeField] private HealthSoundProvider _healthSoundProvider;
        [SerializeField] private GameObject _playerPos;

        [Header("Анимация перехода между энкаунтерами")]
        [SerializeField] private float _flyUpDuration = 0.55f;
        [SerializeField] private float _flyDownDuration = 0.65f;
        [SerializeField] private float _flyHeightOffset = 30f;
        [SerializeField] private Ease _flyUpEase = Ease.InCubic;
        [SerializeField] private Ease _flyDownEase = Ease.OutCubic;

        [Header("Cinemachine Impulse (Опционально)")]
        [Tooltip("Источник импульса Cinemachine при ударе комнаты о стол. Если пусто — ищется на префабе рига.")]
        [SerializeField] private CinemachineImpulseSource _landingImpulseSource;

        [Inject] private DiContainer _container = null;
        [Inject] private GameManager _gameManager = null;
        [Inject] private GameSessionProgression _sessionProgression = null;
        [Inject] private PlayerDamageFeedback _playerFeedback = null;
        [Inject] private TurnSpotlightController _spotlight = null;

        private HealthController _health;
        [SerializeField] private HealthController _playerHealth;
        private EnemyAIController _enemyAI;
        private PlayerItemSpawner _playerItemSpawner;
        private EnemyItemSpawner _enemyItemSpawner;
        private RoundGenerator _roundGenerator;

        public ItemUseMessageView _itemUseMessageView;
        public Camera _interactCamera;

        public HealthController PlayerHealth => _playerHealth;
        public HealthSoundProvider HealthSoundProvider => _healthSoundProvider;
        public CoinPileController CoinPileController => CoinPileController.Instance;
        public GameObject PlayerPos => _playerPos;

        private EncounterRig _rig;
        private ShopEncounterRig _shopRig;
        private EnemyReactionDirector _director;
        private FirstEncounterItemSelector _firstEncounterSelector;
        private ShopEncounterController _shopController;

        public event System.Action OnExitRequested;

        private void Awake()
        {
            if (_interactCamera == null)
                _interactCamera = Camera.main;

            RunManager.EnsureExists();
            RunManager.Instance.RegisterHost(this);
        }

        private void SetLookControllersEnabled(bool enabled)
        {
            var looks = FindObjectsByType<CinemachineStationaryLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var look in looks)
            {
                if (look != null) look.enabled = enabled;
            }
        }

        private void TriggerLandingImpulse()
        {
            if (_landingImpulseSource != null)
            {
                _landingImpulseSource.GenerateImpulse();
                return;
            }

            var rigImpulse = _rig != null 
                ? _rig.GetComponentInChildren<CinemachineImpulseSource>() 
                : (_shopRig != null ? _shopRig.GetComponentInChildren<CinemachineImpulseSource>() : null);

            rigImpulse?.GenerateImpulse();
        }

        public IEnumerator EnterRoutine(EncounterDefinition def)
        {
            // Очищаем статические гейты
            ShellPeekGate.Cancel();
            ShellKnifeGate.Cancel();
            ShellHammerGate.Cancel();
            GameplayGate.Clear();
            SceneTransitionGate.Clear();
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = false;

            SetLookControllersEnabled(false);

            Transform oldMoving = _rig != null ? _rig.transform : (_shopRig != null ? _shopRig.transform : null);
            Volume oldVol = _rig != null ? _rig.PostProcessVolume : (_shopRig != null ? _shopRig.GetComponentInChildren<Volume>(true) : null);

            if (_director != null)
            {
                Destroy(_director.gameObject);
                _director = null;
            }

            if (_shopController != null)
            {
                _shopController.OnShopExited -= RequestExit;
                _shopController = null;
            }

            // --- ШАГ 1: Старая комната улетает ВВЕРХ ---
            if (oldMoving != null)
            {
                DG.Tweening.Sequence exitSeq = DOTween.Sequence().SetUpdate(true);
                exitSeq.Join(oldMoving.DOMoveY(oldMoving.position.y + _flyHeightOffset, _flyUpDuration).SetEase(_flyUpEase));

                if (oldVol != null)
                    exitSeq.Join(DOTween.To(() => oldVol.weight, x => oldVol.weight = x, 0f, _flyUpDuration));

                yield return exitSeq.WaitForCompletion();
                yield return CleanupOldRigRoutine();
            }

            // --- ШАГ 2: Спавн новой комнаты ВВЕРХУ ---
            Vector3 targetPos = _rigParent != null ? _rigParent.position : Vector3.zero;
            Vector3 spawnPos = targetPos + Vector3.up * _flyHeightOffset;

            Transform newMoving = null;
            Volume newVol = null;

            if (def.Kind == EncounterKind.Shop)
            {
                if (def.RigPrefab == null)
                {
                    Debug.LogError($"[EncounterHost] У магазина '{def.Id}' не задан RigPrefab.");
                    SetLookControllersEnabled(true);
                    yield break;
                }

                _shopRig = _container.InstantiatePrefabForComponent<ShopEncounterRig>(def.RigPrefab, spawnPos, Quaternion.identity, _rigParent);
                if (_shopRig == null)
                {
                    Debug.LogError($"[EncounterHost] На префабе магазина '{def.RigPrefab.name}' нет ShopEncounterRig.");
                    SetLookControllersEnabled(true);
                    yield break;
                }

                yield return null;
                newMoving = _shopRig.transform;
                newVol = _shopRig.GetComponentInChildren<Volume>(true);
            }
            else
            {
                _rig = _container.InstantiatePrefabForComponent<EncounterRig>(def.RigPrefab, spawnPos, Quaternion.identity, _rigParent);
                yield return null;
                newMoving = _rig.transform;
                newVol = _rig.PostProcessVolume;
            }

            if (newVol != null)
            {
                newVol.weight = 0f;
                newVol.priority = 10;
            }

            // --- ШАГ 3: Новая комната летит СВЕРХУ ВНИЗ ---
            if (newMoving != null)
            {
                DG.Tweening.Sequence enterSeq = DOTween.Sequence().SetUpdate(true);
                enterSeq.Join(newMoving.DOMove(targetPos, _flyDownDuration).SetEase(_flyDownEase));

                if (newVol != null)
                    enterSeq.Join(DOTween.To(() => newVol.weight, x => newVol.weight = x, 1f, _flyDownDuration));

                yield return enterSeq.WaitForCompletion();
                newMoving.position = targetPos;
            }

            // --- ШАГ 4: ИМПАКТ ПРИЗЕМЛЕНИЯ (Только через Cinemachine Impulse) ---
            TriggerLandingImpulse();

            if (_rig != null && _rig.LandingParticles != null)
                _rig.LandingParticles.Play();
            else if (_shopRig != null)
            {
                var particles = _shopRig.GetComponentInChildren<ParticleSystem>(true);
                particles?.Play();
            }

            yield return new WaitForSeconds(0.08f);
            SetLookControllersEnabled(true);

            // --- ШАГ 5: Запуск логики энкаунтера ---
            if (def.Kind == EncounterKind.Shop)
            {
                SetupShopEncounter(def);
            }
            else
            {
                SetupBattleEncounter(def);

                bool isFirstEncounter = IsFirstEncounter(def);
                if (isFirstEncounter && def.FirstEncounterConfig != null)
                {
                    _firstEncounterSelector = _rig.GetComponentInChildren<FirstEncounterItemSelector>(true);
                    if (_firstEncounterSelector != null)
                    {
                        _firstEncounterSelector.OnSelectionComplete += () => StartEncounter(def);
                        _firstEncounterSelector.StartSelection();
                        yield break;
                    }
                }
            }

            StartEncounter(def);
        }

        private void SetupBattleEncounter(EncounterDefinition def)
        {
            _enemyAI = _rig.EnemyAI;
            _health = _rig.Health;
            _playerItemSpawner = _rig.PlayerItemSpawner;
            _enemyItemSpawner = _rig.EnemyItemSpawner;
            _roundGenerator = _rig.RoundGenerator;

            _health.BindEnemyFeedback(_rig.EnemyFeedback);
            _playerFeedback.BindRig(_rig);
            _playerItemSpawner.ResetForNewEncounter(_rig.EnemyLook);
            _enemyItemSpawner.ResetForNewEncounter(_rig.EnemyLook);
            _enemyAI.ResetForNewEncounter(
                def.EnemyAIConfig,
                def.Kind,
                _sessionProgression != null ? _sessionProgression.SelectedPreset : null);

            _gameManager.BindRig(_rig);
            _spotlight.SetTurnIndicator(_rig.TurnIndicator);

            if (def.ReactionConfig != null)
                _director = EnemyReactionBootstrap.CreateDirector(def.ReactionConfig);

            _shopController = _rig.GetComponentInChildren<ShopEncounterController>(true);
            if (_shopController != null)
                _shopController.OnShopExited += RequestExit;
        }

        private void SetupShopEncounter(EncounterDefinition def)
        {
            _shopController = _shopRig.ShopController;
            if (_shopController == null)
                _shopController = _shopRig.GetComponentInChildren<ShopEncounterController>(true);

            if (_shopController != null)
                _shopController.OnShopExited += RequestExit;
        }

        private bool IsFirstEncounter(EncounterDefinition def)
        {
            if (def.Kind != EncounterKind.Enemy) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null) return false;

            return runManager.CurrentRun.MapState.CompletedNodeIds.Count == 0;
        }

        private void StartEncounter(EncounterDefinition def)
        {
            _gameManager.BeginEncounter(def.Id, def.Kind == EncounterKind.Tutorial);
        }

        public void RequestExit() => OnExitRequested?.Invoke();

        public IEnumerator ExitRoutine()
        {
            SetLookControllersEnabled(false);

            Transform oldMoving = _rig != null ? _rig.transform : (_shopRig != null ? _shopRig.transform : null);
            Volume oldVol = _rig != null ? _rig.PostProcessVolume : (_shopRig != null ? _shopRig.GetComponentInChildren<Volume>(true) : null);

            if (_director != null)
            {
                Destroy(_director.gameObject);
                _director = null;
            }

            if (_shopController != null)
            {
                _shopController.OnShopExited -= RequestExit;
                _shopController = null;
            }

            if (oldMoving != null)
            {
                DG.Tweening.Sequence exitSeq = DOTween.Sequence().SetUpdate(true);
                exitSeq.Join(oldMoving.DOMoveY(oldMoving.position.y + _flyHeightOffset, _flyUpDuration).SetEase(_flyUpEase));

                if (oldVol != null)
                    exitSeq.Join(DOTween.To(() => oldVol.weight, x => oldVol.weight = x, 0f, _flyUpDuration));

                yield return exitSeq.WaitForCompletion();
            }

            yield return CleanupOldRigRoutine();
            SetLookControllersEnabled(true);
        }

        private IEnumerator CleanupOldRigRoutine()
        {
            if (_shopRig != null)
            {
                if (CoinPileController.Instance != null)
                {
                    var combatZone = CoinPileController.Instance.GetComponent<BoxCollider>();
                    if (combatZone != null)
                        CoinPileController.Instance.CoinZone = combatZone;
                }

                Destroy(_shopRig.gameObject);
                _shopRig = null;
            }

            if (_rig != null)
            {
                _gameManager.StopEncounter();
                _roundGenerator?.ClearRound();
                _playerItemSpawner?.ResetForNewEncounter(null);
                _enemyItemSpawner?.ResetForNewEncounter(null);
                _gameManager.BindRig(null);
                _spotlight.SetTurnIndicator(null);

                Destroy(_rig.gameObject);
                _rig = null;
                _enemyAI = null;
                _health = null;
                _playerItemSpawner = null;
                _enemyItemSpawner = null;
                _roundGenerator = null;
            }

            yield return null;
        }
    }
}