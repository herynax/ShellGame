using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Run;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Meta;
using ShellGame.UI;
using ShellGame.Tutorial;
using Unity.Cinemachine;
using Zenject;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public static event Action<float> ScreenGoingBlack;
    public static event Action ScreenFullyBlack;
    public static event Action LoadingScreenShown;
    public static event Action<float> ScreenRevealing;
    public static event Action SceneRevealCompleted;
    public static event Action<float> LoadProgressChanged;
    public static event Action FinalLevelCompleted;

    [Header("Настройки фейда")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Опции")]
    public float delayBeforeFadeOut = 0.3f;
    public bool blockInputDuringLoad = true;

    [Header("Смерть и Переход")]
    public bool loadNextSceneByName = true; 
    public string nextSceneOnEnemyDeath;
    public string firstSceneOnPlayerDeath = "GameScene";
    public string tutorialSceneName = "GameScene";
    public string roomLightTag = "RoomLight";
    public float roomDarkenDuration = 1.5f;
    public float enemyDeathWaitTimeout = 10f;

    [Header("Победа (Смерть Босса)")]
    public bool isFinalLevel = false;
    public string mainMenuSceneName = "MainMenu";

    [Header("Экран загрузки")]
    public float minLoadingDuration = 2.0f;
    public float delayAfterFullProgress = 0.4f;

    [Header("UI Контроллеры")]
    [SerializeField] private RunStatsScreenController runStatsScreen;
    [SerializeField] private UnlockNotificationScreenController unlockScreen;

    public string CurrentTipsGroup { get; private set; }
    public bool IsTransitioning => isLoading;

    private Canvas fadeCanvas;
    private bool isLoading = false;

    [InjectOptional] private HealthController _healthController = null;
    [InjectOptional] private IUnlockManager _unlockManager = null;
    [InjectOptional] private ShellGame.Meta.IGlobalProgressService _globalProgress = null;
    [Inject] private GameSessionProgression _sessionProgression;

    private void Awake()
    {
        if (gameObject.scene.name == "DontDestroyOnLoad")
        {
            if (Instance == null) Instance = this;
            return;
        }

        if (Instance == null)
        {
            Instance = this;
            GameObject persistentRoot = transform.root.gameObject;
            if (persistentRoot != gameObject) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance.ApplySceneSettings(this);
            Destroy(gameObject);
            return;
        }

        RunStatsTracker.EnsureExists();
        if (fadeCanvasGroup == null) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        fadeCanvas = GetComponentInChildren<Canvas>();
        if (fadeCanvas != null) fadeCanvas.sortingOrder = 9999;
    }

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable() => GameEvents.SideDied += HandleSideDied;
    private void OnDisable() => GameEvents.SideDied -= HandleSideDied;

    private void HandleSideDied(TurnSide side)
    {
        if (isLoading) return;

        if (side == TurnSide.Enemy && TutorialSceneTransitionGate.HoldEnemyDeathTransition)
            return;

        if (SceneTransitionGate.IsHeld)
            return;

        if (side == TurnSide.Player && _globalProgress is ShellGame.Meta.GlobalProgressService concreteProgress)
            concreteProgress.EnsureDeathCounted();

        StopCameraLookOnTransition();
        StartCoroutine(UnifiedDeathRoutine(side));
    }

    public void ContinueAfterTutorialEnemyDefeat()
    {
        if (isLoading) return;
        StartCoroutine(UnifiedDeathRoutine(TurnSide.Enemy));
    }

    public void ContinueAfterHeldSceneTransition(TurnSide deadSide)
    {
        if (isLoading) return;

        if (deadSide == TurnSide.Player && _globalProgress is ShellGame.Meta.GlobalProgressService concreteProgress)
            concreteProgress.EnsureDeathCounted();

        StopCameraLookOnTransition();
        StartCoroutine(UnifiedDeathRoutine(deadSide));
    }

    private void StopCameraLookOnTransition()
    {
        var lookControllers = FindObjectsByType<CinemachineStationaryLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var lookController in lookControllers)
            if (lookController != null) lookController.enabled = false;
    }

    private static void HideCursorForLoading()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator UnifiedDeathRoutine(TurnSide deadSide)
    {
        isLoading = true;
        SetPauseBlocked(true);

        if (deadSide == TurnSide.Enemy)
            yield return WaitForEnemyDeathAnimation();

        var run = RunManager.Instance;
        bool inRun = run != null && run.HasActiveRun;
        if (inRun) run.NotifyEncounterFinished(deadSide);

        bool isBossEncounter = inRun && run.CurrentEncounterKind == EncounterKind.Boss;
        bool isWin = deadSide == TurnSide.Enemy && isBossEncounter;
        bool isLoss = deadSide == TurnSide.Player;

        // --- ВАЖНО: Если побеждён обычный враг внутри забега — возвращаемся на карту БЕЗ черного экрана ---
        if (!isWin && !isLoss && inRun)
        {
            RunStatsTracker.Instance?.RegisterEnemyDefeated();
            EnsureSessionProgression().AdvanceToNextLevel();
            yield return run.ReturnToMapRoutine(); // Комната плавно улетает вверх в 3D
            isLoading = false;
            SetPauseBlocked(false);
            yield break;
        }

        // --- ИНАЧЕ (Смерть игрока / Финал босса) — полноценный переход сцены через черное затемнение ---
        HideCursorForLoading();
        if (blockInputDuringLoad && fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = true;

        Light roomLight = FindRoomLight();
        float screenFadeDuration = fadeDuration;
        if (_healthController != null && _healthController.DeathSoundDuration > 0f)
            screenFadeDuration = _healthController.DeathSoundDuration;

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Dose Counter", 0f, true);
        ScreenGoingBlack?.Invoke(screenFadeDuration);
        yield return fadeCanvasGroup.DOFade(1f, screenFadeDuration).SetUpdate(true).WaitForCompletion();

        if (roomLight != null) roomLight.DOIntensity(0f, roomDarkenDuration).SetUpdate(true);
        Time.timeScale = 0f;

        ScreenFullyBlack?.Invoke();

        if (isWin)
        {
            GameEvents.RaiseGameWon();
            FinalLevelCompleted?.Invoke();
        }

        RunStatsTracker.Instance?.EndRun();
        if (runStatsScreen != null)
            yield return runStatsScreen.ShowAndWaitForContinue(BuildStatsSnapshot());

        EnsureSessionProgression().Reset();
        ShellGame.Meta.RunCheckpointStorage.Clear();

        if (_unlockManager != null)
        {
            var newUnlocks = _unlockManager.GetUnacknowledgedUnlocks();
            if (newUnlocks.Count > 0 && unlockScreen != null)
            {
                yield return unlockScreen.ShowSequence(newUnlocks, _unlockManager);
            }
        }

        string targetScene = isWin
            ? mainMenuSceneName
            : GameManager.GetNewRunSceneName(tutorialSceneName, firstSceneOnPlayerDeath);

        run?.EndRun();
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);

        HideCursorForLoading();
        LoadingScreenShown?.Invoke();
        LoadProgressChanged?.Invoke(0f);

        yield return TrackAsyncLoading(asyncLoad);

        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);
        ScreenRevealing?.Invoke(fadeDuration);

        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
        Time.timeScale = 1f;

        SceneRevealCompleted?.Invoke();
        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
        SetPauseBlocked(false);
    }

    private IEnumerator WaitForEnemyDeathAnimation()
    {
        float elapsed = 0f;
        while (EnemyDamageFeedback.IsEnemyDeathAnimationPlaying)
        {
            if (enemyDeathWaitTimeout > 0f && elapsed >= enemyDeathWaitTimeout) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private RunStatsSnapshot BuildStatsSnapshot()
    {
        var tracker = RunStatsTracker.Instance;
        if (tracker == null) return default;

        return new RunStatsSnapshot
        {
            ElapsedSeconds = tracker.ElapsedTime,
            TotalMoves = tracker.TotalMoves,
            Mistakes = tracker.Mistakes,
            EnemiesDefeated = tracker.EnemiesDefeated,
            BestStreak = tracker.BestStreak,
            Accuracy = tracker.Accuracy,
        };
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(sceneName: sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(sceneIndex: sceneIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName = "", int sceneIndex = -1)
    {
        isLoading = true;
        SetPauseBlocked(true);
        if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = true;

        ScreenGoingBlack?.Invoke(fadeDuration);
        if (fadeCanvasGroup != null) yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

        Time.timeScale = 0f;
        ScreenFullyBlack?.Invoke();
        LoadingScreenShown?.Invoke();
        LoadProgressChanged?.Invoke(0f);

        yield return new WaitForSecondsRealtime(0.1f);

        AsyncOperation asyncLoad = !string.IsNullOrEmpty(sceneName)
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(sceneIndex);

        yield return TrackAsyncLoading(asyncLoad);

        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        ScreenRevealing?.Invoke(fadeDuration);
        if (fadeCanvasGroup != null)
        {
            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }

        Time.timeScale = 1f;
        SceneRevealCompleted?.Invoke();
        isLoading = false;
        SetPauseBlocked(false);
    }

    private void SetPauseBlocked(bool blocked) => PauseController.Instance?.SetPauseBlocked(blocked);

    private IEnumerator TrackAsyncLoading(AsyncOperation asyncLoad)
    {
        if (asyncLoad == null) yield break;

        asyncLoad.allowSceneActivation = false;
        float displayedProgress = 0f;
        float elapsedTime = 0f;

        while (displayedProgress < 1f || asyncLoad.progress < 0.9f)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float realNormalized = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            float timeNormalized = minLoadingDuration > 0f ? Mathf.Clamp01(elapsedTime / minLoadingDuration) : 1f;

            displayedProgress = Mathf.Min(realNormalized, timeNormalized);
            LoadProgressChanged?.Invoke(displayedProgress);

            if (displayedProgress >= 1f && asyncLoad.progress >= 0.9f) break;
            yield return null;
        }

        LoadProgressChanged?.Invoke(1f);
        if (delayAfterFullProgress > 0f) yield return new WaitForSecondsRealtime(delayAfterFullProgress);
        
        asyncLoad.allowSceneActivation = true;
        while (!asyncLoad.isDone) yield return null;
    }

    private Light FindRoomLight()
    {
        if (string.IsNullOrEmpty(roomLightTag)) return null;
        GameObject tagged = GameObject.FindGameObjectWithTag(roomLightTag);
        return tagged != null ? tagged.GetComponent<Light>() : null;
    }

    private GameSessionProgression EnsureSessionProgression()
    {
        if (_sessionProgression != null) return _sessionProgression;
        var progressionObject = new GameObject("GameSessionProgression");
        _sessionProgression = progressionObject.AddComponent<GameSessionProgression>();
        return _sessionProgression;
    }

    private void ApplySceneSettings(SceneLoader sceneLoader)
    {
        isFinalLevel = sceneLoader.isFinalLevel;
        loadNextSceneByName = sceneLoader.loadNextSceneByName;
        nextSceneOnEnemyDeath = sceneLoader.nextSceneOnEnemyDeath;
        firstSceneOnPlayerDeath = sceneLoader.firstSceneOnPlayerDeath;
        tutorialSceneName = sceneLoader.tutorialSceneName;
        mainMenuSceneName = sceneLoader.mainMenuSceneName;
    }
}