using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using ShellGame.Core;
using ShellGame.Gameplay;
using Zenject;

namespace ShellGame.Tutorial
{
    public class TutorialScenarioManager : MonoBehaviour
    {
        [SerializeField] private string _nextSceneName = "Level_2";

        [Header("--- CINEMACHINE КАМЕРЫ ---")]
        [SerializeField] private CinemachineCamera _mainCamera;
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

        private TurnSide _currentTurnSide = TurnSide.Player;
        private bool _isEnemyDead = false;

        private const int MainCameraPriority = 10;
        private const int SecondaryCameraPriority = 0;

        /// <summary>Сколько первых ходов озвучиваем реакцией (игрок + враг). Дальше — тишина до смерти врага.</summary>
        private const int ReactedTurnsCount = 2;

        private RoundGenerator _roundGenerator;
        private GameManager _gameManager;


        [Inject]
        private void InjectDependencies(GameManager gameManager, RoundGenerator roundGenerator)
        {
            _gameManager = gameManager;
            _roundGenerator = roundGenerator;
        }

        private IEnumerator Start()
        {
            GameEvents.ActiveSideChanged += OnSideChanged;
            GameEvents.SideDied += OnSideDied;

            // На всякий случай устанавливаем начальную камеру.
            SetMainCamera();

            yield return StartCoroutine(RunTutorialSequence());

            GameEvents.ActiveSideChanged -= OnSideChanged;
            GameEvents.SideDied -= OnSideDied;
        }

        private void OnSideChanged(TurnSide side)
        {
            _currentTurnSide = side;
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
                // После раскрытия GameManager может сразу переключить активную
                // сторону, поэтому сохраняем сторону хода заранее.
                var turnSide = _currentTurnSide;
                var waitForReveal = new WaitForShellRevealed();

                yield return waitForReveal.Run(this);

                if (_isEnemyDead)
                    break;

                if (turnSide == TurnSide.Player)
                {
                    var lines = waitForReveal.HasMarker ? _playerFoundMarkerLines : _playerFoundEmptyLines;
                    foreach (var line in lines)
                        yield return SayWithCameraReset(line);
                }
                else
                {
                    var lines = waitForReveal.HasMarker ? _enemyFoundMarkerLines : _enemyFoundEmptyLines;
                    foreach (var line in lines)
                        yield return SayWithCameraReset(line);
                }
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

            GoToNextLevel();
            OnTutorialCompleted();
        }


        // =========================================================
        // CINEMACHINE
        // =========================================================

        private void SetMainCamera()
        {
            if (_mainCamera == null || _hpCamera == null)
                return;

            _mainCamera.Priority = MainCameraPriority;
            _hpCamera.Priority = SecondaryCameraPriority;
        }

        private void SetHpCamera()
        {
            if (_mainCamera == null || _hpCamera == null)
                return;

            _mainCamera.Priority = SecondaryCameraPriority;
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