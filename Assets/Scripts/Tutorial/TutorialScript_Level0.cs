using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Run;
using Zenject;

namespace ShellGame.Tutorial
{
    public class TutorialScenarioManager : MonoBehaviour
    {
        [SerializeField] private string _nextSceneName = "Level_1";

        [Header("--- CINEMACHINE КАМЕРЫ ---")]
        [SerializeField] private CinemachineCamera _hpCamera;

        [Header("--- 1. ВСТУПЛЕНИЕ ---")]
        [SerializeField] private DialogueLine _helloLine;
        [SerializeField] private DialogueLine _intro1Line;
        [SerializeField] private DialogueLine _intro2Line;
        [SerializeField] private DialogueLine _watchCarefullyLine;

        [Header("--- 2. ПРАВИЛА (ДЕМОНСТРАЦИЯ) ---")]
        [SerializeField] private DialogueLine _hpDemoLine;
        [SerializeField] private DialogueLine _rulesSimpleLine;
        [SerializeField] private DialogueLine _markerRevealLine;
        [SerializeField] private DialogueLine _damageRuleLine;
        [SerializeField] private DialogueLine _emptyRevealLine;
        [SerializeField] private DialogueLine _enemyRuleLine;
        [SerializeField] private DialogueLine _startLine;

        [Header("--- 3. ИГРОВЫЕ РЕАКЦИИ (ВЫБОР ИГРОКА) ---")]
        [SerializeField] private DialogueLine[] _playerFoundMarkerLines;
        [SerializeField] private DialogueLine[] _playerFoundEmptyLines;

        [Header("--- 4. ИГРОВЫЕ РЕАКЦИИ (ВЫБОР ВРАГА) ---")]
        [SerializeField] private DialogueLine[] _enemyFoundMarkerLines;
        [SerializeField] private DialogueLine[] _enemyFoundEmptyLines;

        [Header("--- 5. ФИНАЛ ---")]
        [SerializeField] private DialogueLine[] _finalLines;

        private bool _isEnemyDead = false;

        private const int MainCameraPriority = 10;
        private const int SecondaryCameraPriority = 0;

        /// <summary>Сколько первых ходов озвучиваем реакцией (игрок + враг). Дальше — тишина до смерти врага.</summary>
        private const int ReactedTurnsCount = 2;

        private RoundGenerator _roundGenerator;
        private GameManager _gameManager;


        [Inject]
        private void InjectDependencies(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        private void Awake()
        {
            var rig = GetComponentInParent<EncounterRig>();

            if (rig != null)
            {
                _roundGenerator = rig.RoundGenerator;
            }
            else
            {
                Debug.LogError(
                    "[TutorialScenarioManager] Не удалось найти EncounterRig в родителях.");
            }
        }

        private IEnumerator Start()
        {
            GameEvents.SideDied += OnSideDied;

            // Переход после смерти врага в обучении запускаем МЫ (в конце
            // сценария, после предсмертной речи), а не SceneLoader по событию
            // смерти. Иначе переход стартует в тот же кадр и реплики врага не
            // успевают договорить.
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = true;

            // На всякий случай устанавливаем начальную камеру.
            SetMainCamera();

            yield return StartCoroutine(RunTutorialSequence());

            GameEvents.SideDied -= OnSideDied;
        }

        private void OnDestroy()
        {
            // Статик переживает смену сцены — обязательно снимаем холд,
            // иначе на обычных уровнях переход после смерти врага не случится.
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = false;
        }

        private void OnSideDied(TurnSide side)
        {
            if (side != TurnSide.Player)
                _isEnemyDead = true;
        }

        private IEnumerator RunTutorialSequence()
        {
            // ==========================================
            // ЭТАП 1: ВСТУПЛЕНИЕ
            // ==========================================

            yield return SayWithCameraReset(_helloLine);
            yield return SayWithCameraReset(_intro1Line);
            yield return SayWithCameraReset(_intro2Line);
            yield return SayWithCameraReset(_watchCarefullyLine);


            // ==========================================
            // ЭТАП 2: ОБЪЯСНЕНИЕ ПРАВИЛ
            // ==========================================

            yield return new Parallel(
                new Say(_hpDemoLine),
                new DoAction(() =>
                {
                    SetHpCamera();
                })
            ).Run(this);

            // Возвращаем обычную камеру.
            SetMainCamera();

            yield return new WaitCameraReset().Run(this);


            yield return new Parallel(
                new Say(_rulesSimpleLine),
                new DoAction(() =>
                {
                    SpawnCupsOnTable();
                })
            ).Run(this);

            yield return new WaitCameraReset().Run(this);


            yield return new Parallel(
                new Say(_markerRevealLine),
                new DoAction(() =>
                {
                    RevealCupWithMarker();
                })
            ).Run(this);

            yield return new WaitCameraReset().Run(this);


            yield return new Parallel(
                new Say(_damageRuleLine),
                new DoAction(() =>
                {
                    CloseCup();
                })
            ).Run(this);

            yield return new WaitCameraReset().Run(this);


            yield return new Parallel(
                new Say(_emptyRevealLine),
                new DoAction(() =>
                {
                    RevealEmptyCup();
                })
            ).Run(this);

            yield return new WaitCameraReset().Run(this);


            yield return new Parallel(
                new Say(_enemyRuleLine),
                new DoAction(() =>
                {
                    CloseCup();
                })
            ).Run(this);

            yield return new WaitCameraReset().Run(this);


            yield return SayWithCameraReset(_startLine);


            // ==========================================
            // ЗАПУСК ИГРЫ
            // ==========================================

            if (_gameManager != null)
            {
                _gameManager.ContinueTutorialShuffle();
                _gameManager.UnlockTutorialPlayerChoice();
            }


            // ==========================================
            // ЭТАП 3: ПЕРВЫЙ ХОД ИГРОКА И ПЕРВЫЙ ХОД ВРАГА
            // (озвучиваем только эти два хода — по одному разу на сторону)
            // ==========================================

        for (int turnNumber = 0; turnNumber < ReactedTurnsCount && !_isEnemyDead; turnNumber++)
        {
            var result = new WaitForShellResult();

            // Ждём конкретную пару:
            // выбор напёрстка -> раскрытие именно выбранного напёрстка.
            yield return result.Run(this);

            if (_isEnemyDead)
                break;

            if (_gameManager != null)
                _gameManager.PauseTutorialGameplay();

            if (result.SelectedBy == TurnSide.Player)
            {
                var lines = result.HasMarker
                    ? _playerFoundMarkerLines
                    : _playerFoundEmptyLines;

                foreach (var line in lines)
                    yield return SayWithCameraReset(line);
            }
            else
            {
                var lines = result.HasMarker
                    ? _enemyFoundMarkerLines
                    : _enemyFoundEmptyLines;

                foreach (var line in lines)
                    yield return SayWithCameraReset(line);
            }

            if (_gameManager != null)
                _gameManager.ResumeTutorialGameplay();
        }

            // ==========================================
            // ЭТАП 3.5: ТИШИНА — ИГРА ИДЁТ САМА ДО СМЕРТИ ВРАГА
            // ==========================================

            while (!_isEnemyDead)
                yield return null;


            // ==========================================
            // ЭТАП 4: ФИНАЛ
            // ==========================================

            yield return new WaitSeconds(0.5f).Run(this);

            foreach (var line in _finalLines)
            {
                yield return SayWithCameraReset(line);
            }

            // Флаг прохождения ставим ДО перехода: карта после возврата
            // и RunManager уже должны считать обучение пройденным.
            OnTutorialCompleted();
            GoToNextLevel();
        }


        // =========================================================
        // CINEMACHINE
        // =========================================================

        private void SetMainCamera()
        {
            if (_hpCamera == null)
                return;
            _hpCamera.Priority = SecondaryCameraPriority;
        }

        private void SetHpCamera()
        {
            if (_hpCamera == null)
                return;
            _hpCamera.Priority = MainCameraPriority;
        }


        // =========================================================
        // DIALOGUE
        // =========================================================

        private IEnumerator SayWithCameraReset(
            DialogueLine line,
            float pauseAfterReset = 0.05f)
        {
            if (line == null)
                yield break;

            yield return new Say(line).Run(this);

            yield return new WaitCameraReset(pauseAfterReset).Run(this);
        }


        // =========================================================
        // GAMEPLAY DEMONSTRATION
        // =========================================================

        private void SpawnCupsOnTable()
        {
            GameEvents.RaiseRoundStartConfirmed();
        }

        private void RevealCupWithMarker()
        {
            if (_roundGenerator != null)
                _roundGenerator.RevealOnlyMarkedShell(2.5f);
        }

        private void RevealEmptyCup()
        {
            if (_roundGenerator != null)
                _roundGenerator.RevealOnlyEmptyShell(2.5f);
        }

        private void CloseCup()
        {
            // ShellAnimator сам опустит напёрсток.
        }


        private void GoToNextLevel()
        {
            // Реплики предсмертной речи уже произнесены — холд нам больше не
            // нужен, а снимаем его до перехода, чтобы статик не уехал дальше.
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = false;

            // Обучение — обычный энкаунтер в сцене Game: возвращаемся на карту
            // под чёрным экраном. Сцену не грузим.
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun && SceneLoader.Instance != null)
            {
                if (!SceneLoader.Instance.RunTransition(run.ReturnToMapRoutine()))
                    Debug.LogWarning("[TutorialScenarioManager] Не удалось запустить возврат на карту: переход уже идёт.");
                return;
            }

            // Забега нет (обучение запущено отдельной сценой) — старое поведение.
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(_nextSceneName);
        }

        private void OnTutorialCompleted()
        {
            PlayerPrefs.SetInt(GameManager.TutorialCompletedPrefKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[TutorialScript_Level0] Обучение завершено и сохранено в PlayerPrefs.");
        }
    }
}