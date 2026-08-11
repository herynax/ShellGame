using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Новая система ввода
using Unity.Cinemachine;
using DG.Tweening;
using ShellGame.Audio; // Для фейда музыки при выходе

/// <summary>
/// Контроллер паузы.
/// ESC ставит игру на паузу: переключает Cinemachine-камеру на "камеру паузы",
/// прячет игровой прицел (фейд-аут) и показывает системный курсор, ставит Time.timeScale = 0.
/// Повторный ESC (или кнопка "назад" в UI) снимает паузу и возвращает всё обратно.
/// Паузу можно временно заблокировать (например, во время загрузки уровня в SceneLoader).
/// </summary>
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    public static event Action OnPaused;
    public static event Action OnResumed;

    [Header("Камеры")]
    [Tooltip("Основная (игровая, FPS) камера Cinemachine")]
    public CinemachineCamera mainCamera;
    [Tooltip("Камера, которая активируется во время паузы (например, статичный общий план)")]
    public CinemachineCamera pauseCamera;
    public CinemachineStationaryLook cameraController; 

    [Header("UI паузы")]
    [Tooltip("Канвас-группа с меню паузы (кнопки Resume/Exit и т.д.)")]
    public CanvasGroup pauseMenuCanvasGroup;

    [Header("Игровой прицел (Crosshair)")]
    [Tooltip("Канвас-группа с прицелом, которая будет фейдиться при паузе")]
    public CanvasGroup cursorCanvasGroup;

    [Header("Настройки")]
    public float fadeDuration = 0.3f;

    public bool IsPaused { get; private set; }

    // Если true — пауза недоступна (например, идёт загрузка уровня)
    private bool pauseBlocked = false;
    private bool isExiting = false;
    private Sequence activeSequence;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Стартовое состояние — не на паузе
        if (pauseMenuCanvasGroup != null)
        {
            pauseMenuCanvasGroup.alpha = 0f;
            pauseMenuCanvasGroup.blocksRaycasts = false;
        }
        
        // В начале игры прицел (кроссхейр) должен быть виден
        if (cursorCanvasGroup != null)
        {
            cursorCanvasGroup.alpha = 1f;
            cursorCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (isExiting) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (IsPaused)
        {
            Resume();
        }
        else
        {
            TryPause();
        }
    }

    /// <summary>
    /// Пытается поставить игру на паузу. Ничего не делает, если пауза заблокирована или уже активна.
    /// </summary>
    public void TryPause()
    {
        if (IsPaused || pauseBlocked) return;

        activeSequence?.Kill();
        StopAllCoroutines();
        StartCoroutine(PauseRoutine());
    }

    /// <summary>
    /// Снимает паузу. Можно вызывать и из UI (например, кнопка "Resume" или системная кнопка "Назад").
    /// </summary>
    public void Resume()
    {
        if (!IsPaused) return;

        activeSequence?.Kill();
        StopAllCoroutines();
        StartCoroutine(ResumeRoutine());
    }

    /// <summary>
    /// Вызывается кнопкой "Назад" в UI паузы — просто алиас для Resume(),
    /// чтобы было явное отдельное имя для хука в инспекторе.
    /// </summary>
    public void OnBackButtonPressed()
    {
        Resume();
    }

    /// <summary>
    /// Блокирует/разблокирует возможность ставить игру на паузу.
    /// Используй, например, во время загрузки уровня (SceneLoader), катсцен и т.д.
    /// Если пауза принудительно блокируется, пока игра уже на паузе — снимаем её.
    /// </summary>
    public void SetPauseBlocked(bool blocked)
    {
        pauseBlocked = blocked;

        if (blocked && IsPaused)
        {
            Resume();
        }
    }

    private IEnumerator PauseRoutine()
    {
        IsPaused = true;

        Time.timeScale = 0f;
        
        if (cameraController != null) cameraController.enabled = false;

        // Переключаем приоритет камер
        if (mainCamera != null) mainCamera.Priority = 0;
        if (pauseCamera != null) pauseCamera.Priority = 10;

        // Разлочиваем и показываем СТАНДАРТНЫЙ системный курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuCanvasGroup != null) pauseMenuCanvasGroup.blocksRaycasts = true;

        // Фейдим UI: Меню появляется (1f), а игровой прицел ИСЧЕЗАЕТ (0f)
        activeSequence = DOTween.Sequence().SetUpdate(true);
        if (pauseMenuCanvasGroup != null) activeSequence.Join(pauseMenuCanvasGroup.DOFade(1f, fadeDuration));
        if (cursorCanvasGroup != null) activeSequence.Join(cursorCanvasGroup.DOFade(0f, fadeDuration));

        yield return activeSequence.WaitForCompletion();


        OnPaused?.Invoke();
    }

    private IEnumerator ResumeRoutine()
    {
        // Сначала возвращаем время, чтобы игра не "дёргалась" в момент фейда
        Time.timeScale = 1f;

        // Фейдим UI: Меню исчезает (0f), а игровой прицел ПОЯВЛЯЕТСЯ (1f)
        activeSequence = DOTween.Sequence().SetUpdate(true);
        if (pauseMenuCanvasGroup != null) activeSequence.Join(pauseMenuCanvasGroup.DOFade(0f, fadeDuration));
        if (cursorCanvasGroup != null) activeSequence.Join(cursorCanvasGroup.DOFade(1f, fadeDuration));

        yield return activeSequence.WaitForCompletion();

        if (pauseMenuCanvasGroup != null) pauseMenuCanvasGroup.blocksRaycasts = false;

        if (mainCamera != null) mainCamera.Priority = 10;
        if (pauseCamera != null) pauseCamera.Priority = 0;

        // Прячем и блокируем системный курсор обратно
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        IsPaused = false;

        if (cameraController != null) cameraController.enabled = true;

        OnResumed?.Invoke();
    }

    /// <summary>
    /// Вызывается кнопкой "Выйти из игры" в меню паузы.
    /// Т.к. отдельного главного меню в игре нет — выход происходит прямо из паузы:
    /// экран (поверх уже видимого меню) уходит в чёрное, музыка фейдится, приложение закрывается.
    /// </summary>
    public void ExitGame()
    {
        if (isExiting) return;
        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator ExitGameRoutine()
    {
        isExiting = true;
        // Блокируем паузу/ESC на время выхода, чтобы нельзя было "отменить" процесс закрытия
        pauseBlocked = true;

        Debug.Log("[PauseController] Выход из игры — затемняем экран...");

        Sequence exitSequence = DOTween.Sequence().SetUpdate(true);

        // Используем общий чёрный канвас из SceneLoader, если он есть — он рисуется
        // поверх меню паузы и полностью скрывает экран перед закрытием приложения
        if (SceneLoader.Instance != null && SceneLoader.Instance.fadeCanvasGroup != null)
        {
            exitSequence.Join(SceneLoader.Instance.fadeCanvasGroup.DOFade(1f, fadeDuration));
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.FadeOutMusic(fadeDuration);
        }

        yield return exitSequence.WaitForCompletion();

        Debug.Log("[PauseController] Экран затемнён. Закрываем приложение...");

        Application.Quit();

        // На случай, если Application.Quit() не сработает в редакторе
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        activeSequence?.Kill();
    }
}