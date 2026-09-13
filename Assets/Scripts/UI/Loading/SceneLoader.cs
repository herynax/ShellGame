// START OF FILE SceneLoader.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Meta; // Добавлено для анлоков
using ShellGame.UI;
using ShellGame.Tutorial;
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
    public string firstSceneOnPlayerDeath = "Tutorial";
    public string roomLightTag = "RoomLight";
    public float roomDarkenDuration = 1.5f;

    [Header("Победа (Смерть Босса)")]
    [Tooltip("Если включено, смерть врага на этом уровне считается победой в игре.")]
    public bool isFinalLevel = false;
    [Tooltip("Имя сцены, в которой смерть босса завершает забег.")]
    public string finalLevelSceneName = "Final";
    public string mainMenuSceneName = "MainMenu"; // Куда кидать после победы/смерти

    [Header("Экран загрузки")]
    public float minLoadingDuration = 2.0f;
    public float delayAfterFullProgress = 0.4f;

    [Header("Яркость")]
    [SerializeField] private Image brightnessOverlay;

    [Header("UI Контроллеры")]
    [SerializeField] private RunStatsScreenController runStatsScreen;
    [SerializeField] private UnlockNotificationScreenController unlockScreen; // НОВЫЙ ЭКРАН АНЛОКОВ

    public Image BrightnessOverlay => brightnessOverlay;

    private Canvas fadeCanvas;
    private bool isLoading = false;

    [InjectOptional] private HealthController _healthController;
    [InjectOptional] private IUnlockManager _unlockManager;
    [InjectOptional] private ShellGame.Meta.IGlobalProgressService _globalProgress;
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

        // The tutorial owns the final narration after the first opponent is
        // defeated. It will explicitly resume this same transition once the
        // narration has ended.
        if (side == TurnSide.Enemy && TutorialSceneTransitionGate.HoldEnemyDeathTransition)
            return;

        if (side == TurnSide.Player && _globalProgress is ShellGame.Meta.GlobalProgressService concreteProgress)
            concreteProgress.EnsureDeathCounted();

        ReleaseCursorAfterDeath();
        StartCoroutine(UnifiedDeathRoutine(side));
    }

    public void ContinueAfterTutorialEnemyDefeat()
    {
        if (isLoading) return;
        StartCoroutine(UnifiedDeathRoutine(TurnSide.Enemy));
    }

    private void ReleaseCursorAfterDeath()
    {
        var lookControllers = FindObjectsOfType<CinemachineStationaryLook>(true);
        foreach (var lookController in lookControllers)
            if (lookController != null) lookController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator UnifiedDeathRoutine(TurnSide deadSide)
    {
        isLoading = true;
        SetPauseBlocked(true);

        if (fadeCanvasGroup == null) yield break;
        if (blockInputDuringLoad) fadeCanvasGroup.blocksRaycasts = true;

        // Определяем исход
        bool isWin = deadSide == TurnSide.Enemy && IsFinalLevel();
        bool isLoss = (deadSide == TurnSide.Player);

        // --- ШАГ 1: ВИЗУАЛЬНОЕ ЗАТЕМНЕНИЕ ---
        Light roomLight = FindRoomLight();
        float screenFadeDuration = fadeDuration;
        if (_healthController != null && _healthController.DeathSoundDuration > 0f)
            screenFadeDuration = _healthController.DeathSoundDuration;

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Dose Counter", 0f, true);
        ScreenGoingBlack?.Invoke(screenFadeDuration);
        yield return fadeCanvasGroup.DOFade(1f, screenFadeDuration).SetUpdate(true).WaitForCompletion();

        if (roomLight != null) roomLight.DOIntensity(0f, roomDarkenDuration).SetUpdate(true);

        // ЗАМОРАЖИВАЕМ ВРЕМЯ НА ВРЕМЯ ЗАГРУЗКИ
        Time.timeScale = 0f;

        ScreenFullyBlack?.Invoke();

        // --- ШАГ 2: СОБЫТИЕ ПОБЕДЫ ---
        if (isWin)
        {
            GameEvents.RaiseGameWon();
            FinalLevelCompleted?.Invoke();
        }

        // --- ШАГ 3: ЭКРАН СТАТИСТИКИ И АНЛОКОВ ---
        AsyncOperation asyncLoad = null;

        if (isWin || isLoss)
        {
            // 3.1 Статистика
            RunStatsTracker.Instance?.StopClock();
            if (runStatsScreen != null)
                yield return runStatsScreen.ShowAndWaitForContinue(BuildStatsSnapshot());

            EnsureSessionProgression().Reset();
            ShellGame.Meta.RunCheckpointStorage.Clear();

            // 3.2 Анлоки
            if (_unlockManager != null)
            {
                var newUnlocks = _unlockManager.GetUnacknowledgedUnlocks();
                if (newUnlocks.Count > 0 && unlockScreen != null)
                {
                    yield return unlockScreen.ShowSequence(newUnlocks, _unlockManager);
                }
            }

            string targetScene = isWin ? mainMenuSceneName : firstSceneOnPlayerDeath;
            asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        }
        else
        {
            // --- ОБЫЧНЫЙ ПЕРЕХОД НА СЛЕДУЮЩИЙ УРОВЕНЬ ---
            RunStatsTracker.Instance?.RegisterEnemyDefeated();
            EnsureSessionProgression().AdvanceToNextLevel();

            if (loadNextSceneByName)
                asyncLoad = SceneManager.LoadSceneAsync(nextSceneOnEnemyDeath);
            else
            {
                int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
                asyncLoad = SceneManager.LoadSceneAsync(nextIndex >= SceneManager.sceneCountInBuildSettings ? 0 : nextIndex);
            }
        }

        LoadingScreenShown?.Invoke();
        LoadProgressChanged?.Invoke(0f);

        // --- ШАГ 4: ФОНОВАЯ ЗАГРУЗКА ---
        yield return TrackAsyncLoading(asyncLoad);

        // --- ШАГ 5: ФЕЙД АУТ ---
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);
        ScreenRevealing?.Invoke(fadeDuration);
        
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();

        // ВОЗВРАЩАЕМ ВРЕМЯ В НОРМУ ТОЛЬКО ПОЛНОСТЬЮ ЗАВЕРШИВ ПЕРЕХОД
        Time.timeScale = 1f;

        SceneRevealCompleted?.Invoke();
        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
        SetPauseBlocked(false);
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
        if (blockInputDuringLoad && fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = true;

        ScreenGoingBlack?.Invoke(fadeDuration);
        if (fadeCanvasGroup != null) yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

        // ЗАМОРАЖИВАЕМ ВРЕМЯ НА ВРЕМЯ ЗАГРУЗКИ
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

        // ВОЗВРАЩАЕМ ВРЕМЯ В НОРМУ ТОЛЬКО ПОЛНОСТЬЮ ЗАВЕРШИВ ПЕРЕХОД
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

    private bool IsFinalLevel()
    {
        return isFinalLevel ||
               (!string.IsNullOrEmpty(finalLevelSceneName) &&
                SceneManager.GetActiveScene().name == finalLevelSceneName);
    }

    private void ApplySceneSettings(SceneLoader sceneLoader)
    {
        isFinalLevel = sceneLoader.isFinalLevel;
        finalLevelSceneName = sceneLoader.finalLevelSceneName;
        loadNextSceneByName = sceneLoader.loadNextSceneByName;
        nextSceneOnEnemyDeath = sceneLoader.nextSceneOnEnemyDeath;
        firstSceneOnPlayerDeath = sceneLoader.firstSceneOnPlayerDeath;
        mainMenuSceneName = sceneLoader.mainMenuSceneName;
    }
}
// END OF FILE
