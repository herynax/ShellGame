using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.UI;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public static event Action<float> ScreenGoingBlack;
    public static event Action ScreenFullyBlack;
    public static event Action LoadingScreenShown;
    public static event Action<float> ScreenRevealing;
    public static event Action<float> LoadProgressChanged; // 0..1, нормализовано

    [Header("Настройки фейда")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Опции")]
    public float delayBeforeFadeOut = 0.3f;
    public bool blockInputDuringLoad = true;

    [Header("Смерть (Переход)")]
    public bool loadNextSceneByName = true; 
    public string nextSceneOnEnemyDeath;
    public string firstSceneOnPlayerDeath = "Tutorial";
    public string roomLightTag = "RoomLight";
    public float roomDarkenDuration = 1.5f;

    [Header("Экран загрузки")]
    [Tooltip("Искусственная минимальная длительность заполнения прогресс-бара (в секундах), чтобы игрок успел прочитать подсказку.")]
    public float minLoadingDuration = 2.0f;

    [Tooltip("Пауза после того, как прогресс-бар дошел до 100%, перед активацией сцены.")]
    public float delayAfterFullProgress = 0.4f;

    [Header("Яркость")]
    [SerializeField] private Image brightnessOverlay;

    [Header("Экран статистики забега (при смерти игрока)")]
    [SerializeField] private RunStatsScreenController runStatsScreen;

    public Image BrightnessOverlay => brightnessOverlay;

    private Canvas fadeCanvas;
    private bool isLoading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Persist the whole hierarchy. Unity can only preserve a root object;
            // this also keeps the fade canvas, loading UI and stats screen together
            // if SceneLoader is moved under another object in the scene.
            GameObject persistentRoot = transform.root.gameObject;
            if (persistentRoot != gameObject)
                transform.SetParent(null, true);

            DontDestroyOnLoad(gameObject);
            Debug.Log($"[SceneLoader] Persistent UI registered: {gameObject.name}");
        }
        else
        {
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

    private void OnDestroy()
    {
        if (Instance == this)
            Debug.LogWarning("[SceneLoader] Persistent UI was destroyed.");
    }

    private void HandleSideDied(TurnSide side)
    {
        if (isLoading) return;
        StartCoroutine(UnifiedDeathRoutine(side));
    }

    /// <summary>
    /// Единая логика смерти / перехода:
    /// 1. Плавно тушит свет и делает Fade In черного экрана
    /// 2. Если умер игрок — показывает экран статистики забега после полного затемнения
    ///    и только после продолжения включает подсказку и прогресс-бар
    /// 3. Фоном асинхронно грузит сцену и плавно наполняет шкалу (minLoadingDuration)
    /// 4. Делает паузу на 100% полоски и активирует сцену
    /// 5. Делает Fade Out (ScreenRevealing)
    /// </summary>
    private IEnumerator UnifiedDeathRoutine(TurnSide deadSide)
    {
        isLoading = true;
        SetPauseBlocked(true);

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("fadeCanvasGroup не назначена в SceneLoader!");
            isLoading = false;
            SetPauseBlocked(false);
            yield break;
        }

        if (blockInputDuringLoad) fadeCanvasGroup.blocksRaycasts = true;

        // --- ШАГ 1: ВИЗУАЛЬНОЕ ЗАТЕМНЕНИЕ ---
        Light roomLight = FindRoomLight();
        float screenFadeDuration = fadeDuration;
        var healthController = FindFirstObjectByType<HealthController>();
        if (healthController != null && healthController.DeathSoundDuration > 0f)
        {
            screenFadeDuration = healthController.DeathSoundDuration;
        }

        Debug.Log($"[SceneLoader] Начинаем затемнение экрана (Смерть: {deadSide}, длительность: {screenFadeDuration:0.###}с)...");

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Dose Counter", 0f, true);
        ScreenGoingBlack?.Invoke(screenFadeDuration);
        yield return fadeCanvasGroup.DOFade(1f, screenFadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        if (roomLight != null)
            roomLight.DOIntensity(0f, roomDarkenDuration).SetUpdate(true);

        // --- ШАГ 2: ЭКРАН СТАЛ ПОЛНОСТЬЮ ЧЕРНЫМ ---
        ScreenFullyBlack?.Invoke();

        // --- ШАГ 2.5: ЭКРАН СТАТИСТИКИ ЗАБЕГА (только при смерти игрока) ---
        if (deadSide == TurnSide.Player)
        {
            RunStatsTracker.Instance?.StopClock();
            if (runStatsScreen != null)
            yield return runStatsScreen.ShowAndWaitForContinue(BuildStatsSnapshot());
        }

        // Для врага это происходит сразу после fade, для игрока — после закрытия статистики.
        LoadingScreenShown?.Invoke();
        LoadProgressChanged?.Invoke(0f);

        // --- ШАГ 3: ФОНОВАЯ ЗАГРУЗКА СЦЕНЫ ---
        AsyncOperation asyncLoad = null;

        if (deadSide == TurnSide.Player)
        {
            string targetScene = string.IsNullOrEmpty(firstSceneOnPlayerDeath)
                ? "Tutorial"
                : firstSceneOnPlayerDeath;

            Debug.Log($"[SceneLoader] Возвращаемся на стартовую сцену: {targetScene}");
            asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        }
        else
        {
            RunStatsTracker.Instance?.RegisterEnemyDefeated();
            EnsureSessionProgression().AdvanceToNextLevel();

            if (loadNextSceneByName)
            {
                Debug.Log($"[SceneLoader] Загружаем следующую сцену по имени: {nextSceneOnEnemyDeath}");
                asyncLoad = SceneManager.LoadSceneAsync(nextSceneOnEnemyDeath);
            }
            else
            {
                int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
                if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings) nextBuildIndex = 0;
                
                Debug.Log($"[SceneLoader] Загружаем следующую сцену по индексу: {nextBuildIndex}");
                asyncLoad = SceneManager.LoadSceneAsync(nextBuildIndex);
            }
        }

        // Выполняем искусственно растянутое отслеживание загрузки с докруткой бара
        yield return TrackAsyncLoading(asyncLoad);

        // --- ШАГ 4: ФЕЙД АУТ (ПРОЯВЛЕНИЕ НОВОЙ СЦЕНЫ) ---
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        Debug.Log($"[SceneLoader] Начинаем Fade Out...");
        ScreenRevealing?.Invoke(fadeDuration);
        
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

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
        if (fadeCanvasGroup != null) 
            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

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
        isLoading = false;
        SetPauseBlocked(false);
    }

    private void SetPauseBlocked(bool blocked)
    {
        PauseController.Instance?.SetPauseBlocked(blocked);
    }

    /// <summary>
    /// Контролирует плавное заполнение шкалы загрузки в течение minLoadingDuration
    /// и активирует сцену только после завершения и паузы.
    /// </summary>
    private IEnumerator TrackAsyncLoading(AsyncOperation asyncLoad)
    {
        if (asyncLoad == null) yield break;

        // Не даем сцене активироваться мгновенно
        asyncLoad.allowSceneActivation = false;

        float displayedProgress = 0f;
        float elapsedTime = 0f;

        // AsyncOperation.progress доходит максимум до 0.9 пока allowSceneActivation = false
        while (displayedProgress < 1f || asyncLoad.progress < 0.9f)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float realNormalized = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            float timeNormalized = minLoadingDuration > 0f ? Mathf.Clamp01(elapsedTime / minLoadingDuration) : 1f;

            // Прогресс растет по таймеру, но не обгоняет реальную загрузку данных
            displayedProgress = Mathf.Min(realNormalized, timeNormalized);
            LoadProgressChanged?.Invoke(displayedProgress);

            // Если время вышло и сцена в памяти готова
            if (displayedProgress >= 1f && asyncLoad.progress >= 0.9f)
                break;

            yield return null;
        }

        // Фиксируем 100%
        LoadProgressChanged?.Invoke(1f);

        // Пауза, чтобы игрок увидел полную полоску
        if (delayAfterFullProgress > 0f)
        {
            yield return new WaitForSecondsRealtime(delayAfterFullProgress);
        }

        // Разрешаем фактическое включение сцены
        asyncLoad.allowSceneActivation = true;
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private Light FindRoomLight()
    {
        if (string.IsNullOrEmpty(roomLightTag)) return null;
        GameObject tagged = GameObject.FindGameObjectWithTag(roomLightTag);
        return tagged != null ? tagged.GetComponent<Light>() : null;
    }

    private GameSessionProgression EnsureSessionProgression()
    {
        var progression = FindAnyObjectByType<GameSessionProgression>();
        if (progression != null) return progression;

        var progressionObject = new GameObject("GameSessionProgression");
        progression = progressionObject.AddComponent<GameSessionProgression>();
        return progression;
    }

    public void SetFadeAlpha(float alpha) { if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = Mathf.Clamp01(alpha); }
    public void FadeInInstant() { if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = 1f; fadeCanvasGroup.blocksRaycasts = true; } }
    public void FadeOutInstant() { if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = 0f; fadeCanvasGroup.blocksRaycasts = false; } }
}