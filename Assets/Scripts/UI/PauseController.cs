// START OF FILE PauseController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 
using Unity.Cinemachine;
using DG.Tweening;
using ShellGame.Audio; 
using ShellGame.Gameplay; // Добавлено для GameSessionProgression
using Zenject;
using SpankyBoy.JuiceUI.Free;

/// <summary>
/// Контроллер паузы с поддержкой вложенных меню (Стек меню).
/// ESC ставит игру на паузу. Повторный ESC закрывает текущее подменю, 
/// а если подменю нет — снимает игру с паузы.
/// </summary>
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    public static event Action OnPaused;
    public static event Action OnResumed;

    [Header("Камеры")]
    [HideInInspector] public CinemachineCamera mainCamera;
    [HideInInspector] public CinemachineCamera pauseCamera;
    [HideInInspector] public CinemachineStationaryLook cameraController;
    [HideInInspector] [SerializeField] private CinemachineBrain _brain;

    [Header("UI паузы")]
    [Tooltip("Канвас-группа с ГЛАВНЫМ меню паузы (кнопки Resume/Exit и т.д.)")]
    public CanvasGroup pauseMenuCanvasGroup;

    [Header("Игровой прицел (Crosshair)")]
    [HideInInspector] public CanvasGroup crosshairCanvasGroup;

    [Header("Настройки")]
    public float fadeDuration = 0.3f;

    public bool IsPaused { get; private set; }

    private bool pauseBlocked = false;
    private bool isExiting = false;
    private Sequence activeSequence;

    private float _timeScaleBeforePause = 1f;
    private CinemachineVirtualCameraBase _pausedFromCamera;
    private PrioritySettings _pausedFromCameraPriority;

    private CinemachineStationaryLook[] _allLookControllers;
    private List<CinemachineCamera> _sceneCameras = new List<CinemachineCamera>();
    private List<CinemachineVirtualCameraBase> _sceneVirtualCameras = new List<CinemachineVirtualCameraBase>();
    private List<CanvasGroup> _sceneCanvasGroups = new List<CanvasGroup>();

    private Stack<CanvasGroup> _menuStack = new Stack<CanvasGroup>();

    [Inject]
    private void InjectSceneObjects(
        CinemachineBrain brain,
        List<CinemachineCamera> sceneCameras,
        List<CinemachineVirtualCameraBase> sceneVirtualCameras,
        List<CinemachineStationaryLook> lookControllers,
        List<CanvasGroup> canvasGroups)
    {
        _brain = brain;
        _sceneCameras = sceneCameras;
        _sceneVirtualCameras = sceneVirtualCameras;
        _allLookControllers = lookControllers.ToArray();
        _sceneCanvasGroups = canvasGroups;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        _allLookControllers ??= new CinemachineStationaryLook[0];
    }

    private void ResolveReferences()
    {
        var cameras = (_sceneCameras ?? new List<CinemachineCamera>()).ToArray();
        if (mainCamera == null) mainCamera = FindNamedComponent(cameras, "MainCamera");
        if (pauseCamera == null) pauseCamera = FindNamedComponent(cameras, "PauseCamera");

        if (mainCamera == null)
        {
            foreach (var camera in cameras)
                if (camera != pauseCamera) { mainCamera = camera; break; }
        }

        if (pauseCamera == null)
        {
            foreach (var camera in cameras)
                if (camera != mainCamera && camera.name.IndexOf("pause", StringComparison.OrdinalIgnoreCase) >= 0)
                { pauseCamera = camera; break; }
        }

        if (cameraController == null)
            cameraController = _allLookControllers != null && _allLookControllers.Length > 0 ? _allLookControllers[0] : null;
        if (pauseMenuCanvasGroup == null)
            pauseMenuCanvasGroup = FindCanvasGroup("PauseMenu");
        if (crosshairCanvasGroup == null)
            crosshairCanvasGroup = FindCanvasGroup("Pointer") ?? FindCanvasGroup("Crosshair");
    }

    private static CinemachineCamera FindNamedComponent(CinemachineCamera[] components, string name)
    {
        foreach (var component in components)
            if (component != null && component.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return component;
        foreach (var component in components)
            if (component != null && component.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return component;
        return null;
    }

    private CanvasGroup FindCanvasGroup(string name)
    {
        var groups = _sceneCanvasGroups ?? new List<CanvasGroup>();
        foreach (var group in groups)
            if (group != null && group.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return group;
        return null;
    }

    private void Start()
    {
        ResolveReferences();

        if (pauseMenuCanvasGroup != null)
        {
            pauseMenuCanvasGroup.alpha = 0f;
            pauseMenuCanvasGroup.blocksRaycasts = false;
            pauseMenuCanvasGroup.gameObject.SetActive(false);
        }

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
            CloseTopMenu();
        }
        else
        {
            TryPause();
        }
    }

    private void PlayMenuIn(CanvasGroup menu)
    {
        if (menu == null) return;

        menu.gameObject.SetActive(true);
        menu.interactable = true;
        menu.blocksRaycasts = true;

        if (menu.TryGetComponent<PanelAnimator_Free>(out var animator))
        {
            animator.Show();
        }
        else
        {
            menu.alpha = 0f;
            menu.DOKill();
            menu.DOFade(1f, fadeDuration).SetUpdate(true);
        }
    }

    private void PlayMenuOut(CanvasGroup menu)
    {
        if (menu == null) return;

        menu.interactable = false;
        menu.blocksRaycasts = false;

        if (menu.TryGetComponent<PanelAnimator_Free>(out var animator))
        {
            animator.Hide(); 
        }
        else
        {
            menu.DOKill();
            menu.DOFade(0f, fadeDuration).SetUpdate(true)
                .OnComplete(() => menu.gameObject.SetActive(false));
        }
    }

    public void OpenSubmenu(CanvasGroup submenu)
    {
        if (submenu == null || !IsPaused) return;

        if (_menuStack.Count > 0)
        {
            CanvasGroup current = _menuStack.Peek();
            PlayMenuOut(current);
        }

        _menuStack.Push(submenu);
        PlayMenuIn(submenu);
    }

    public void CloseTopMenu()
    {
        if (_menuStack.Count <= 1)
        {
            Resume(); 
            return;
        }

        CanvasGroup top = _menuStack.Pop();
        PlayMenuOut(top);

        if (_menuStack.Count > 0)
        {
            CanvasGroup previous = _menuStack.Peek();
            PlayMenuIn(previous);
        }
    }

    public void OnBackButtonPressed()
    {
        CloseTopMenu();
    }

    public void TryPause()
    {
        if (IsPaused || pauseBlocked) return;

        activeSequence?.Kill();
        StopAllCoroutines();
        StartCoroutine(PauseRoutine());
    }

    public void Resume()
    {
        if (!IsPaused) return;

        activeSequence?.Kill();
        StopAllCoroutines();
        StartCoroutine(ResumeRoutine());
    }

    public void SetPauseBlocked(bool blocked)
    {
        pauseBlocked = blocked;
        if (blocked && IsPaused) Resume();
    }

    private CinemachineVirtualCameraBase ResolveActiveCamera()
    {
        if (_brain != null && _brain.ActiveVirtualCamera is CinemachineVirtualCameraBase activeVcam)
            return activeVcam;
        return mainCamera;
    }

    private int FindMaxScenePriority()
    {
        int max = 0;
        foreach (var cam in _sceneVirtualCameras)
        {
            if (cam == null || cam == pauseCamera) continue;
            if (cam.Priority.Enabled) max = Mathf.Max(max, cam.Priority.Value);
        }
        return max;
    }

    private IEnumerator PauseRoutine()
    {
        IsPaused = true;

        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        
        foreach (var look in _allLookControllers)
            if (look != null) look.enabled = false;

        _pausedFromCamera = ResolveActiveCamera();
        if (_pausedFromCamera != null)
            _pausedFromCameraPriority = _pausedFromCamera.Priority;

        if (pauseCamera != null)
            pauseCamera.Priority = new PrioritySettings { Enabled = true, Value = FindMaxScenePriority() + 100 };

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        activeSequence = DOTween.Sequence().SetUpdate(true);
        
        _menuStack.Clear();
        if (pauseMenuCanvasGroup != null)
        {
            _menuStack.Push(pauseMenuCanvasGroup);
            PlayMenuIn(pauseMenuCanvasGroup);
        }

        if (crosshairCanvasGroup != null) 
            activeSequence.Join(crosshairCanvasGroup.DOFade(0f, fadeDuration));

        yield return activeSequence.WaitForCompletion();

        OnPaused?.Invoke();
    }

    private IEnumerator ResumeRoutine()
    {
        activeSequence = DOTween.Sequence().SetUpdate(true);
        
        foreach (var menu in _menuStack)
        {
            PlayMenuOut(menu);
        }
        _menuStack.Clear();

        if (crosshairCanvasGroup != null) 
            activeSequence.Join(crosshairCanvasGroup.DOFade(1f, fadeDuration));

        yield return activeSequence.WaitForCompletion();

        if (_pausedFromCamera != null)
            _pausedFromCamera.Priority = _pausedFromCameraPriority;
        else if (mainCamera != null)
            mainCamera.Priority = new PrioritySettings { Enabled = true, Value = 10 };

        if (pauseCamera != null)
        {
            var p = pauseCamera.Priority;
            p.Enabled = false;
            pauseCamera.Priority = p;
        }

        bool previousIgnoreTimeScale = false;
        if (_brain != null)
        {
            previousIgnoreTimeScale = _brain.IgnoreTimeScale;
            _brain.IgnoreTimeScale = true;
        }

        yield return null;
        while (_brain != null && _brain.IsBlending) yield return null;

        if (_brain != null) _brain.IgnoreTimeScale = previousIgnoreTimeScale;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = _timeScaleBeforePause;

        foreach (var look in _allLookControllers)
            if (look != null) look.enabled = true;

        IsPaused = false;
        _pausedFromCamera = null;

        OnResumed?.Invoke();
    }

    public void ExitGame()
    {
        if (isExiting) return;
        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator ExitGameRoutine()
    {
        isExiting = true;
        pauseBlocked = true;

        Sequence exitSequence = DOTween.Sequence().SetUpdate(true);

        if (SceneLoader.Instance != null && SceneLoader.Instance.fadeCanvasGroup != null)
        {
            exitSequence.Join(SceneLoader.Instance.fadeCanvasGroup.DOFade(1f, fadeDuration));
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.FadeOutMusic(fadeDuration);
        }

        yield return exitSequence.WaitForCompletion();

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Рестарт игры (вызывается из окна подтверждения).
    /// </summary>
    public void RestartGame()
    {
        if (isExiting) return;
        isExiting = true; // Блокируем дальнейший инпут
        pauseBlocked = true;

        // Обязательно возвращаем время в норму, иначе новая сцена загрузится "замороженной"
        Time.timeScale = 1f;
        IsPaused = false;

        // Сбрасываем прогрессию забега
        if (GameSessionProgression.Instance != null)
            GameSessionProgression.Instance.Reset();

        // Запускаем процесс загрузки через SceneLoader (загружаем самую первую сцену)
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene("Tutorial"); 
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Tutorial");
        }
    }

    private void OnDestroy()
    {
        activeSequence?.Kill();
    }
}
// END OF FILE