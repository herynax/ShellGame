// START OF FILE SceneLoader.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Feedback;
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
    [Tooltip("Сцена, с которой начинается новая попытка после поражения, если обучение уже пройдено.")]
    public string firstSceneOnPlayerDeath = "Level_1";
    [Tooltip("Сцена, с которой начинается новая попытка после поражения, пока обучение не пройдено.")]
    public string tutorialSceneName = "Tutorial";
    public string roomLightTag = "RoomLight";
    public float roomDarkenDuration = 1.5f;
    [Tooltip("Не начинать затемнение/загрузку, пока не завершится анимация смерти врага. Таймаут — защита от зависания (0 = без таймаута).")]
    public float enemyDeathWaitTimeout = 10f;

    [Header("Победа (Смерть Босса)")]
    [Tooltip("Если включено, смерть врага на этом уровне считается победой в игре.")]
    public bool isFinalLevel = false;
    [Tooltip("Имя сцены, в которой смерть босса завершает забег.")]
    public string finalLevelSceneName = "Final";
    public string mainMenuSceneName = "MainMenu"; // Куда кидать после победы/смерти

    [Header("Экран загрузки")]
    public float minLoadingDuration = 2.0f;
    public float delayAfterFullProgress = 0.4f;

    [Header("UI Контроллеры")]
    [SerializeField] private RunStatsScreenController runStatsScreen;
    [SerializeField] private UnlockNotificationScreenController unlockScreen; // НОВЫЙ ЭКРАН АНЛОКОВ

    /// <summary>
    /// Группа лоадинг-типов для текущего RunTransition (EncounterDefinition.TipsGroup).
    /// Выставляется ДО LoadingScreenShown, так что экран загрузки может прочитать её
    /// в обработчике этого события. Пусто — группа не задана.
    /// </summary>
    public string CurrentTipsGroup { get; private set; }

    /// <summary>
    /// true, пока идёт любой переход: затемнение, загрузка, работа под чёрным
    /// экраном и открытие. Становится false только когда экран полностью открыт.
    /// Так враг может начать вступление не в темноте, а после появления картинки.
    /// </summary>
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

        // The tutorial owns the final narration after the first opponent is
        // defeated. It will explicitly resume this same transition once the
        // narration has ended.
        if (side == TurnSide.Enemy && TutorialSceneTransitionGate.HoldEnemyDeathTransition)
            return;

        // Враг ещё не договорил (смерть = переход на следующий уровень).
        // EnemyReactionDirector освободит холд сам и вызовет Continue.
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

    /// <summary>
    /// Продолжает отложенный переход: вызывается тем, кто держал
    /// SceneTransitionGate (реакции врага дерут его, чтобы финальные реплики
    /// прозвучали до затемнения).
    /// </summary>
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

    /// <summary>
    /// Курсор в игре заперт и скрыт — в том числе на всей загрузке уровня.
    /// Показывать его могут только экраны, которым он нужен для клика
    /// (статистика забега и анлоки): они включают его сами на время показа.
    /// </summary>
    private static void HideCursorForLoading()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator UnifiedDeathRoutine(TurnSide deadSide)
    {
        isLoading = true;
        SetPauseBlocked(true);
        HideCursorForLoading();

        if (fadeCanvasGroup == null) yield break;

        // Экран загрузки (затемнение) начинается только после того, как анимация
        // смерти врага полностью закончилась — судороги, падение и растворение.
        if (deadSide == TurnSide.Enemy)
            yield return WaitForEnemyDeathAnimation();

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
            RunStatsTracker.Instance?.EndRun();
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

            // После поражения начинаем новую попытку: обучение пройдено — с
            // первого игрового уровня, не пройдено — с обучения (иначе игрок
            // потеряет его по пути и попадёт в игру необученным).
            string targetScene = isWin
                ? mainMenuSceneName
                : GameManager.GetNewRunSceneName(tutorialSceneName, firstSceneOnPlayerDeath);

            Debug.Log($"[SceneLoader] Переход после {(isWin ? "победы" : "поражения")} на сцену '{targetScene}' " +
                      $"(обучение пройдено: {GameManager.IsTutorialCompleted()}).");

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

        // Экраны статистики/анлоков могли оставить курсор на себя — на
        // загрузке уровня он уже не нужен.
        HideCursorForLoading();

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

    /// <summary>
    /// Переход без смены сцены (карта -> энкаунтер): затемнение, экран загрузки,
    /// выполнение <paramref name="routine"/> под чёрным экраном, открытие.
    /// Пока routine работает, Time.timeScale = 0, поэтому внутри неё нельзя
    /// использовать WaitForSeconds — только yield return null / Realtime.
    /// Возвращает false, если переход не запущен (уже идёт другой или routine == null).
    /// </summary>
    public bool RunTransition(IEnumerator routine, string tipsGroup = null)
    {
        if (isLoading || routine == null || fadeCanvasGroup == null) return false;

        CurrentTipsGroup = tipsGroup;
        StartCoroutine(RunTransitionRoutine(routine));
        return true;
    }

    private IEnumerator RunTransitionRoutine(IEnumerator routine)
    {
        isLoading = true;
        SetPauseBlocked(true);
        HideCursorForLoading();

        if (blockInputDuringLoad) fadeCanvasGroup.blocksRaycasts = true;

        ScreenGoingBlack?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

        // ЗАМОРАЖИВАЕМ ВРЕМЯ НА ВРЕМЯ ПЕРЕХОДА
        Time.timeScale = 0f;

        ScreenFullyBlack?.Invoke();
        LoadingScreenShown?.Invoke();
        LoadProgressChanged?.Invoke(0f);

        float startTime = Time.unscaledTime;

        // Основная работа под чёрным экраном (вход в энкаунтер / возврат на карту).
        yield return routine;

        // Держим экран загрузки минимум minLoadingDuration, чтобы он не мигал.
        while (minLoadingDuration > 0f)
        {
            float elapsed = Time.unscaledTime - startTime;
            LoadProgressChanged?.Invoke(Mathf.Clamp01(elapsed / minLoadingDuration));
            if (elapsed >= minLoadingDuration) break;
            yield return null;
        }

        LoadProgressChanged?.Invoke(1f);
        if (delayAfterFullProgress > 0f) yield return new WaitForSecondsRealtime(delayAfterFullProgress);

        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);
        ScreenRevealing?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();

        // ВОЗВРАЩАЕМ ВРЕМЯ В НОРМУ ТОЛЬКО ПОЛНОСТЬЮ ЗАВЕРШИВ ПЕРЕХОД
        Time.timeScale = 1f;

        SceneRevealCompleted?.Invoke();
        fadeCanvasGroup.blocksRaycasts = false;
        CurrentTipsGroup = null;
        isLoading = false;
        SetPauseBlocked(false);
    }

    private IEnumerator LoadSceneRoutine(string sceneName = "", int sceneIndex = -1)
    {
        isLoading = true;
        SetPauseBlocked(true);
        fadeCanvasGroup.blocksRaycasts = true;
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
        tutorialSceneName = sceneLoader.tutorialSceneName;
        mainMenuSceneName = sceneLoader.mainMenuSceneName;
    }
}
// END OF FILE