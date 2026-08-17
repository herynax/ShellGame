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
///
/// Камера-агностична: вместо жёстко захардкоженных приоритетов 0/10 для
/// mainCamera/pauseCamera, контроллер на паузе узнаёт РЕАЛЬНО активную в
/// данный момент камеру через CinemachineBrain (это может быть любая из
/// vcam-ов сценария туториала, не обязательно mainCamera), запоминает её и
/// её приоритет, а pauseCamera поднимает выше максимума среди ВСЕХ vcam-ов
/// на сцене — так пауза гарантированно перебивает любую камеру, даже если
/// туториал использует приоритеты 20+. На Resume() приоритет возвращается
/// именно той камере, что была активна до паузы.
///
/// ВАЖНО про два РАЗНЫХ курсора, которые легко перепутать:
/// 1) Cursor.visible / Cursor.lockState (ниже в коде) — это СИСТЕМНЫЙ курсор
///    мыши от Unity/ОС. На паузе он ПОЯВЛЯЕТСЯ (чтобы можно было кликать по
///    меню), в игре — скрыт и залочен в центре экрана.
/// 2) crosshairCanvasGroup (поле ниже) — это игровой UI-прицел (crosshair),
///    нарисованный на Canvas. Он живёт ПРОТИВОПОЛОЖНО системному курсору:
///    на паузе ПРОПАДАЕТ (фейд-аут), в игре — виден.
///
/// ВАЖНО про CinemachineStationaryLook: этот компонент висит НЕ в одном
/// экземпляре, а на КАЖДОЙ vcam сцены по отдельности. Раньше здесь была
/// одна ссылка cameraController — выключалась только она, а все остальные
/// экземпляры (на неактивных сейчас камерах) продолжали тикать в своём
/// Update() и каждый кадр сами перезахватывали курсор
/// (Cursor.lockState = Locked), перебивая то, что выставляет пауза, плюс
/// продолжали вращать свои камеры. Теперь на паузе выключаются ВСЕ
/// найденные на сцене экземпляры разом.
/// </summary>
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    public static event Action OnPaused;
    public static event Action OnResumed;

    [Header("Камеры")]
    [Tooltip("Основная (игровая, FPS) камера Cinemachine — используется как запасной вариант, если активную камеру определить не удалось (например, на самом первом кадре сцены).")]
    public CinemachineCamera mainCamera;
    [Tooltip("Камера, которая активируется во время паузы (например, статичный общий план)")]
    public CinemachineCamera pauseCamera;

    [Tooltip("Больше не используется напрямую — оставлено только для обратной совместимости со старыми ссылками в инспекторе, реально не читается. Все CinemachineStationaryLook на сцене находятся и выключаются автоматически через _allLookControllers.")]
    public CinemachineStationaryLook cameraController;

    [Tooltip("Cinemachine Brain сцены — нужен, чтобы понять, какая камера активна ПРЯМО СЕЙЧАС. Если не назначить в инспекторе, будет найден автоматически в Awake.")]
    [SerializeField] private CinemachineBrain _brain;

    [Header("UI паузы")]
    [Tooltip("Канвас-группа с меню паузы (кнопки Resume/Exit и т.д.)")]
    public CanvasGroup pauseMenuCanvasGroup;

    [Header("Игровой прицел (Crosshair)")]
    [Tooltip("Канвас-группа с ИГРОВЫМ ПРИЦЕЛОМ (crosshair), а не с системным курсором мыши — системный курсор управляется отдельно через Cursor.visible/lockState. На паузе этот прицел гаснет (фейд в 0), при снятии паузы — снова появляется (фейд в 1).")]
    public CanvasGroup crosshairCanvasGroup;

    [Header("Настройки")]
    public float fadeDuration = 0.3f;

    public bool IsPaused { get; private set; }

    // Если true — пауза недоступна (например, идёт загрузка уровня)
    private bool pauseBlocked = false;
    private bool isExiting = false;
    private Sequence activeSequence;

    // Камера, которая была активна ДО постановки на паузу, и её приоритет —
    // чтобы на Resume() вернуть управление именно ей, а не всегда mainCamera.
    private CinemachineVirtualCameraBase _pausedFromCamera;
    private PrioritySettings _pausedFromCameraPriority;

    // Кэш ВСЕХ CinemachineStationaryLook на сцене — этот компонент висит на
    // каждой vcam по отдельности, а не в одном экземпляре.
    private CinemachineStationaryLook[] _allLookControllers;

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

        if (_brain == null)
            _brain = FindFirstObjectByType<CinemachineBrain>();

        _allLookControllers = FindObjectsByType<CinemachineStationaryLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void Start()
    {
        // Стартовое состояние — не на паузе
        if (pauseMenuCanvasGroup != null)
        {
            pauseMenuCanvasGroup.alpha = 0f;
            pauseMenuCanvasGroup.blocksRaycasts = false;
        }

        // В начале игры прицел (crosshair) должен быть виден
        if (crosshairCanvasGroup != null)
        {
            crosshairCanvasGroup.alpha = 1f;
            crosshairCanvasGroup.blocksRaycasts = false;
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

    /// <summary>
    /// Определяет, какая vcam активна ПРЯМО СЕЙЧАС — через CinemachineBrain,
    /// а не по предположению "это всегда mainCamera". Именно это нужно,
    /// чтобы пауза корректно работала поверх любой из камер туториала
    /// (narrator/button/tableCenter/healthBar/gameplay и т.д.).
    /// </summary>
    private CinemachineVirtualCameraBase ResolveActiveCamera()
    {
        if (_brain != null && _brain.ActiveVirtualCamera is CinemachineVirtualCameraBase activeVcam)
            return activeVcam;

        // Запасной вариант — например, самый первый кадр сцены, когда Brain
        // ещё не успел определить активную камеру.
        return mainCamera;
    }

    /// <summary>
    /// Максимальный включённый приоритет среди всех vcam-ов сцены (кроме
    /// самой pauseCamera) — чтобы поднять pauseCamera гарантированно выше
    /// любой из них, сколько бы их ни было и какие приоритеты ни
    /// выставлял сценарий (в т.ч. туториал с приоритетами 20+).
    /// </summary>
    private int FindMaxScenePriority()
    {
        int max = 0;
        var allCameras = FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam == null || cam == pauseCamera)
                continue;

            if (cam.Priority.Enabled)
                max = Mathf.Max(max, cam.Priority.Value);
        }
        return max;
    }

    private IEnumerator PauseRoutine()
    {
        IsPaused = true;

        Time.timeScale = 0f;

        // Выключаем ВСЕ CinemachineStationaryLook на сцене — их несколько,
        // по одному на каждой vcam. Иначе неактивные сейчас камеры
        // продолжат тикать в Update() и каждый кадр сами перезахватывать
        // курсор + крутить свой transform.
        foreach (var look in _allLookControllers)
        {
            if (look != null)
                look.enabled = false;
        }

        // Запоминаем РЕАЛЬНО активную сейчас камеру и её приоритет — это
        // может быть mainCamera, а может быть любая камера туториала.
        _pausedFromCamera = ResolveActiveCamera();
        if (_pausedFromCamera != null)
            _pausedFromCameraPriority = _pausedFromCamera.Priority;

        // Поднимаем pauseCamera выше максимума среди ВСЕХ vcam-ов сцены —
        // а не хардкодим 10, которое туториал легко перебивает своими 20+.
        if (pauseCamera != null)
            pauseCamera.Priority = new PrioritySettings { Enabled = true, Value = FindMaxScenePriority() + 100 };

        // СИСТЕМНЫЙ курсор мыши — разлочиваем и показываем, чтобы можно
        // было кликать по меню паузы.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuCanvasGroup != null) pauseMenuCanvasGroup.blocksRaycasts = true;

        // Фейдим UI: меню паузы появляется (1f), а игровой ПРИЦЕЛ (crosshair,
        // не системный курсор!) исчезает (0f) — на паузе целиться незачем.
        activeSequence = DOTween.Sequence().SetUpdate(true);
        if (pauseMenuCanvasGroup != null) activeSequence.Join(pauseMenuCanvasGroup.DOFade(1f, fadeDuration));
        if (crosshairCanvasGroup != null) activeSequence.Join(crosshairCanvasGroup.DOFade(0f, fadeDuration));

        yield return activeSequence.WaitForCompletion();


        OnPaused?.Invoke();
    }

    private IEnumerator ResumeRoutine()
    {
        // Сначала возвращаем время, чтобы игра не "дёргалась" в момент фейда
        Time.timeScale = 1f;

        // Фейдим UI: меню паузы исчезает (0f), а игровой ПРИЦЕЛ (crosshair)
        // снова появляется (1f).
        activeSequence = DOTween.Sequence().SetUpdate(true);
        if (pauseMenuCanvasGroup != null) activeSequence.Join(pauseMenuCanvasGroup.DOFade(0f, fadeDuration));
        if (crosshairCanvasGroup != null) activeSequence.Join(crosshairCanvasGroup.DOFade(1f, fadeDuration));

        yield return activeSequence.WaitForCompletion();

        if (pauseMenuCanvasGroup != null) pauseMenuCanvasGroup.blocksRaycasts = false;

        // Возвращаем приоритет ИМЕННО той камере, что была активна до
        // паузы — а не всегда mainCamera, как раньше.
        if (_pausedFromCamera != null)
            _pausedFromCamera.Priority = _pausedFromCameraPriority;
        else if (mainCamera != null)
            mainCamera.Priority = new PrioritySettings { Enabled = true, Value = 10 };

        // pauseCamera просто выключаем — она больше не участвует в борьбе
        // за активность, независимо от того, какое число стояло в Value.
        if (pauseCamera != null)
        {
            var p = pauseCamera.Priority;
            p.Enabled = false;
            pauseCamera.Priority = p;
        }

        // Снова включаем ВСЕ CinemachineStationaryLook на сцене.
        foreach (var look in _allLookControllers)
        {
            if (look != null)
                look.enabled = true;
        }

        // СИСТЕМНЫЙ курсор мыши — прячем и лочим обратно в центр экрана.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        IsPaused = false;

        _pausedFromCamera = null;

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