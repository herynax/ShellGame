using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using ShellGame.Core;

public class SceneLoader : MonoBehaviour
{
    // Паттерн Singleton для сохранения экземпляра
    public static SceneLoader Instance { get; private set; }

    // === СОБЫТИЯ ДЛЯ СИНХРОНИЗАЦИИ МУЗЫКИ (MusicManager) ===
    // ScreenGoingBlack(duration) — экран начинает уходить в чёрное (fade-in канваса).
    //   duration = сколько это займёт секунд (0, если переход мгновенный, как при
    //   рестарте после смерти игрока) — MusicManager фейдит громкость музыки в 0
    //   за то же время.
    // ScreenFullyBlack() — экран уже полностью чёрный и новая сцена загружена,
    //   но фейд-аут канваса ещё не начался. Момент, когда можно без "мигания"
    //   для игрока сбросить состояние (например, дозу).
    // ScreenRevealing(duration) — канвас начинает фейдиться обратно (открывать
    //   сцену) — MusicManager параллельно фейдит громкость музыки обратно к базовой.
    public static event Action<float> ScreenGoingBlack;
    public static event Action ScreenFullyBlack;
    public static event Action<float> ScreenRevealing;

    [Header("Настройки фейда")]
    [Tooltip("CanvasGroup, на котором будет происходить фейдинг (должен быть на объекте SceneLoader)")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("Время появления / исчезания в секундах")]
    public float fadeDuration = 0.5f;

    [Header("Опции")]
    [Tooltip("Если True, добавляет небольшую задержку перед фейд-аутом (для визуального эффекта)")]
    public float delayBeforeFadeOut = 0.3f;
    [Tooltip("Блокировать ввод пользователя во время загрузки")]
    public bool blockInputDuringLoad = true;

    [Header("Смерть врага — затемнение сцены")]
    // НОВОЕ ПОЛЕ: Галочка для выбора способа загрузки
    [Tooltip("Если включено — загрузит сцену по указанному ниже имени. Если выключено — загрузит следующую сцену по индексу (Build Index + 1)")]
    public bool loadNextSceneByName = true; 
    
    [Tooltip("Сцена, на которую переходить при смерти врага (победа игрока)")]
    public string nextSceneOnEnemyDeath;
    [Tooltip("Тег GameObject'а с направленным светом комнаты в текущей сцене. SceneLoader персистентный (DontDestroyOnLoad), а roomLight — обычный объект сцены, поэтому прямую ссылку в инспекторе назначить нельзя (она обнулится при смене сцены) — свет ищется по тегу каждый раз заново.")]
    public string roomLightTag = "RoomLight";
    [Tooltip("Длительность затухания света до 0")]
    public float roomDarkenDuration = 1.5f;
    [Tooltip("Intensity света, при пересечении которой (сверху вниз) параллельно начинает фейдиться в чёрное канвас-группа")]
    public float canvasFadeInTriggerIntensity = 2f;

    private Canvas fadeCanvas;
    private bool isLoading = false;

    private void Awake()
    {
        // Логика Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Убеждаемся, что fadeCanvasGroup назначена
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        // Убеждаемся, что Canvas существует и остается при загрузке
        fadeCanvas = GetComponentInChildren<Canvas>();
        if (fadeCanvas != null)
        {
            fadeCanvas.sortingOrder = 9999; // Убедись что он на переднем плане
        }
    }

    private void Start()
    {
        // Инициализируем фейд кнопку: невидимая и не блокирует клики
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        GameEvents.SideDied += HandleSideDied;
    }

    private void OnDisable()
    {
        GameEvents.SideDied -= HandleSideDied;
    }

    private void HandleSideDied(TurnSide side)
    {
        if (isLoading) return;

        if (side == TurnSide.Player)
            RestartSceneOnPlayerDeath();
        else if (side == TurnSide.Enemy)
            LoadNextSceneOnEnemyDeath();
    }

    /// <summary>
    /// Загружает новую сцену по имени
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning("Уже загружается сцена! Подождите...");
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName: sceneName));
    }

    /// <summary>
    /// Загружает новую сцену по индексу (Build Index)
    /// </summary>
    public void LoadScene(int sceneIndex)
    {
        if (isLoading)
        {
            Debug.LogWarning("Уже загружается сцена! Подождите...");
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneIndex: sceneIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName = "", int sceneIndex = -1)
    {
        isLoading = true;

        // Убеждаемся что fadeCanvasGroup существует
        if (fadeCanvasGroup == null)
        {
            Debug.LogError("fadeCanvasGroup не назначена в SceneLoader!");
            isLoading = false;
            yield break;
        }

        // 1. Блокируем ввод пользователя во время загрузки
        if (blockInputDuringLoad)
        {
            fadeCanvasGroup.blocksRaycasts = true;
        }

        // 2. ФЕЙД ИН - чёрный экран закрывает всё
        Debug.Log($"[SceneLoader] Начинаем Fade In...");
        ScreenGoingBlack?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration)
            .SetUpdate(true)  // Игнорирует Time.timeScale
            .WaitForCompletion();

        Debug.Log($"[SceneLoader] Fade In завершён. Загружаем сцену...");

        // 3. Загружаем новую сцену (асинхронно)
        AsyncOperation asyncLoad;
        if (!string.IsNullOrEmpty(sceneName))
        {
            Debug.Log($"[SceneLoader] Загружаем сцену: {sceneName}");
            asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        }
        else
        {
            Debug.Log($"[SceneLoader] Загружаем сцену по индексу: {sceneIndex}");
            asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        }

        // Ждём пока сцена полностью загрузится
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log($"[SceneLoader] Сцена загружена! Ждём перед Fade Out...");
        ScreenFullyBlack?.Invoke();

        // 4. Небольшая задержка перед фейд-аутом (дает время новой сцене инициализироваться)
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        // 5. ФЕЙД АУТ - чёрный экран исчезает, показывается новая сцена
        Debug.Log($"[SceneLoader] Начинаем Fade Out...");
        ScreenRevealing?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        Debug.Log($"[SceneLoader] Fade Out завершён!");

        // 6. Разблокируем ввод
        fadeCanvasGroup.blocksRaycasts = false;

        isLoading = false;
    }

    /// <summary>
    /// Смерть игрока: мгновенное включение чёрного экрана (без анимации),
    /// рестарт текущей сцены, затем обычный плавный фейд-аут.
    /// </summary>
    public void RestartSceneOnPlayerDeath()
    {
        if (isLoading)
        {
            Debug.LogWarning("Уже загружается сцена! Подождите...");
            return;
        }
        StartCoroutine(RestartSceneInstantInRoutine());
    }

    private IEnumerator RestartSceneInstantInRoutine()
    {
        isLoading = true;

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("fadeCanvasGroup не назначена в SceneLoader!");
            isLoading = false;
            yield break;
        }

        if (blockInputDuringLoad)
            fadeCanvasGroup.blocksRaycasts = true;

        // Мгновенное включение чёрного экрана — без анимации.
        // duration = 0, поэтому MusicManager тоже мгновенно обрежет громкость в 0.
        ScreenGoingBlack?.Invoke(0f);
        FadeInInstant();

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        Debug.Log($"[SceneLoader] Рестарт сцены (смерть игрока), индекс: {currentIndex}");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(currentIndex);
        while (!asyncLoad.isDone)
            yield return null;

        ScreenFullyBlack?.Invoke();

        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        ScreenRevealing?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
    }

    /// <summary>
    /// Смерть врага: свет комнаты (roomLight) плавно тухнет до 0 за roomDarkenDuration.
    /// Когда его intensity опускается ниже canvasFadeInTriggerIntensity — параллельно
    /// (поверх ещё продолжающегося затемнения света) запускается fade-in канваса в чёрное.
    /// После того как обе анимации завершились (канвас полностью чёрный) — грузим
    /// след. сцену без повторного fade-in и делаем обычный fade-out.
    /// </summary>
    public void LoadNextSceneOnEnemyDeath()
    {
        if (isLoading)
        {
            Debug.LogWarning("Уже загружается сцена! Подождите...");
            return;
        }
        StartCoroutine(DarkenThenLoadRoutine());
    }

    private IEnumerator DarkenThenLoadRoutine()
    {
        isLoading = true;

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("fadeCanvasGroup не назначена в SceneLoader!");
            isLoading = false;
            yield break;
        }

        Light roomLight = FindRoomLight();
        if (roomLight == null)
        {
            Debug.LogError($"[SceneLoader] Не найден объект со светом комнаты (тег '{roomLightTag}') в текущей сцене!");
            isLoading = false;
            yield break;
        }

        if (blockInputDuringLoad)
            fadeCanvasGroup.blocksRaycasts = true;

        bool canvasFadeStarted = false;
        Tween canvasFadeTween = null;

        Debug.Log($"[SceneLoader] Смерть врага: начинаем затемнение света...");
        Tween lightTween = roomLight.DOIntensity(0f, roomDarkenDuration)
            .SetUpdate(true)
            .OnUpdate(() =>
            {
                if (!canvasFadeStarted && roomLight.intensity <= canvasFadeInTriggerIntensity)
                {
                    canvasFadeStarted = true;
                    Debug.Log($"[SceneLoader] Порог {canvasFadeInTriggerIntensity} пройден, запускаем fade-in канваса параллельно...");
                    // Канвас (и вместе с ним музыка) уходят в чёрное/тишину за fadeDuration
                    ScreenGoingBlack?.Invoke(fadeDuration);
                    canvasFadeTween = fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
                }
            });

        yield return lightTween.WaitForCompletion();

        // На случай если свет так и не пересёк порог
        if (!canvasFadeStarted)
        {
            ScreenGoingBlack?.Invoke(fadeDuration);
            canvasFadeTween = fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        if (canvasFadeTween != null)
            yield return canvasFadeTween.WaitForCompletion();

        ScreenFullyBlack?.Invoke();

        // ИЗМЕНЕНИЕ: Проверяем галочку перед вызовом загрузки сцены
        if (loadNextSceneByName)
        {
            yield return LoadSceneNoFadeInRoutine(sceneName: nextSceneOnEnemyDeath);
        }
        else
        {
            int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
            
            // Защита от ошибки, если следующей сцены нет в Build Settings
            if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning($"[SceneLoader] Сцены с индексом {nextBuildIndex} не существует! Загружаем меню (индекс 0).");
                nextBuildIndex = 0; 
            }
            
            yield return LoadSceneNoFadeInRoutine(sceneIndex: nextBuildIndex);
        }
    }

    /// <summary>
    /// Загрузка сцены при уже полностью чёрном канвасе (fade-in уже сделан
    /// отдельно в DarkenThenLoadRoutine) — здесь только загрузка и fade-out.
    /// </summary>
    private IEnumerator LoadSceneNoFadeInRoutine(string sceneName = "", int sceneIndex = -1)
    {
        AsyncOperation asyncLoad = null;

        // ИЗМЕНЕНИЕ: Теперь функция может грузить как по имени, так и по индексу
        if (!string.IsNullOrEmpty(sceneName))
        {
            Debug.Log($"[SceneLoader] Загружаем сцену по имени: {sceneName}");
            asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        }
        else if (sceneIndex >= 0)
        {
            Debug.Log($"[SceneLoader] Загружаем сцену по индексу: {sceneIndex}");
            asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        }
        else
        {
            Debug.LogWarning("[SceneLoader] Не задано имя сцены или некорректный индекс!");
            fadeCanvasGroup.blocksRaycasts = false;
            isLoading = false;
            yield break;
        }

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log($"[SceneLoader] Сцена загружена! Ждём перед Fade Out...");
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        Debug.Log($"[SceneLoader] Начинаем Fade Out...");
        ScreenRevealing?.Invoke(fadeDuration);
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        Debug.Log($"[SceneLoader] Fade Out завершён!");
        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
    }

    /// <summary>
    /// Ищет свет комнаты в текущей загруженной сцене по тегу roomLightTag.
    /// Вызывается заново при каждой смерти врага, а не кешируется — сам
    /// SceneLoader переживает смену сцен, а свет каждой сцены свой.
    /// </summary>
    private Light FindRoomLight()
    {
        if (string.IsNullOrEmpty(roomLightTag))
            return null;

        GameObject tagged = GameObject.FindGameObjectWithTag(roomLightTag);
        return tagged != null ? tagged.GetComponent<Light>() : null;
    }

    /// <summary>
    /// Принудительно установить альфа (для отладки)
    /// </summary>
    public void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    /// <summary>
    /// Мгновенный чёрный экран (без анимации)
    /// </summary>
    public void FadeInInstant()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Мгновенное удаление чёрного экрана (без анимации)
    /// </summary>
    public void FadeOutInstant()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }
}