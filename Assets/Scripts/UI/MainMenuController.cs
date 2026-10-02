using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Gameplay;
using ShellGame.Meta;
using SpankyBoy.JuiceUI.Free;

/// <summary>
/// Контроллер Главного Меню. 
/// Поддерживает ту же систему глубины (стека) окон, что и меню паузы.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI Главного меню")]
    [Tooltip("Самая первая (корневая) панель меню, где находятся кнопки 'Играть', 'Настройки' и т.д.")]
    [SerializeField] private CanvasGroup rootMenuCanvasGroup;

    [Header("Продолжение забега")]
    [Tooltip("Кнопка 'Продолжить попытку' — становится неактивной (не скрывается), если сохранённого чекпоинта нет.")]
    [SerializeField] private Button continueAttemptButton;

    [Tooltip("Панель подтверждения 'начать заново поверх существующего чекпоинта' — показывается через OpenSubmenu только если чекпоинт есть. Кнопка 'Да' внутри неё должна вызывать ConfirmStartNewGame().")]
    [SerializeField] private CanvasGroup newRunConfirmationPanel;

    [Header("Выбор сложности")]
    [Tooltip("Панель выбора сложности перед стартом новой попытки. Кнопки внутри должны вызывать SelectDifficulty(presetId). Пусто — сложность берётся по умолчанию («Средний»).")]
    [SerializeField] private CanvasGroup difficultyPanel;

    [Header("Настройки анимации (для Fallback)")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Настройки сцены")]
    [Tooltip("Имя сцены, которая загрузится при нажатии 'Новая попытка', если обучение уже пройдено")]
    [SerializeField] private string firstGameplaySceneName = "Level_1";
    [Tooltip("Имя сцены обучения — старт новой попытки, пока обучение не пройдено")]
    [SerializeField] private string tutorialSceneName = "Tutorial";
    private Stack<CanvasGroup> _menuStack = new Stack<CanvasGroup>();
    private bool isExitingOrLoading = false;
    private bool _difficultyChosenForThisStart = false;

    private void Start()
    {
        // Меню всегда кликабельно: курсор нужен независимо от того, откуда
        // игрок в него пришёл (после победы он остался скрыт от загрузки).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (rootMenuCanvasGroup != null)
        {
            _menuStack.Push(rootMenuCanvasGroup);
            PlayMenuIn(rootMenuCanvasGroup);
        }

        if (continueAttemptButton != null)
            continueAttemptButton.gameObject.SetActive(RunCheckpointStorage.HasCheckpoint);
    }

    private void Update()
    {
        if (Keyboard.current == null || isExitingOrLoading) return;
        
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_menuStack.Count > 1)
            {
                CloseTopMenu();
            }
        }
    }

    // ==========================================
    // ЛОГИКА АНИМАЦИИ И ГЛУБИНЫ МЕНЮ
    // ==========================================

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

    /// <summary>
    /// Открывает вложенное меню (например, Настройки).
    /// Вызывать из UI кнопок.
    /// </summary>
    public void OpenSubmenu(CanvasGroup submenu)
    {
        if (submenu == null || isExitingOrLoading) return;

        if (_menuStack.Count > 0)
        {
            CanvasGroup current = _menuStack.Peek();
            PlayMenuOut(current);
        }

        _menuStack.Push(submenu);
        PlayMenuIn(submenu);
    }

    /// <summary>
    /// Закрывает текущее окно и возвращает к предыдущему.
    /// Если это корневое меню — ничего не делает.
    /// </summary>
    public void CloseTopMenu()
    {
        if (_menuStack.Count <= 1 || isExitingOrLoading) return; // или свой аналогичный guard

        CanvasGroup top = _menuStack.Peek();

        // Если у верхней панели есть ICloseGuard (например, это панель настроек с
        // несохранёнными изменениями) — отдаём ей решение, закрываться сразу или нет.
        if (top.TryGetComponent<ICloseGuard>(out var guard))
        {
            guard.RequestClose(ForceCloseTopMenu);
            return;
        }

        ForceCloseTopMenu();
    }

    private void ForceCloseTopMenu()
    {
        CanvasGroup top = _menuStack.Pop();
        PlayMenuOut(top);

        if (_menuStack.Count > 0)
        {
            CanvasGroup previous = _menuStack.Peek();
            PlayMenuIn(previous);
        }
    }

    /// <summary>
    /// Алиас для UI-кнопки "Назад".
    /// </summary>
    public void OnBackButtonPressed()
    {
        CloseTopMenu();
    }

    // ==========================================
    // ЛОГИКА ЗАПУСКА И ВЫХОДА
    // ==========================================

    /// <summary>
    /// Вешать на кнопку "Начать игру"/"Новая попытка". Если чекпоинта нет —
    /// стартует сразу без вопросов. Если чекпоинт есть — новая попытка его
    /// сотрёт, поэтому вместо немедленного старта открывает панель
    /// подтверждения (обычная запись в тот же стек меню, что и любое другое
    /// подменю) и ничего не делает, пока игрок явно не подтвердит.
    /// </summary>
    public void StartGame()
    {
        if (isExitingOrLoading) return;

        // Сначала — выбор сложности (если панель назначена и игрок её ещё не
        // прошёл в этом нажатии). Дальше идёт обычная логика подтверждения.
        if (difficultyPanel != null && !_difficultyChosenForThisStart)
        {
            OpenSubmenu(difficultyPanel);
            return;
        }

        _difficultyChosenForThisStart = false;

        if (!RunCheckpointStorage.HasCheckpoint)
        {
            BeginNewGame();
            return;
        }

        if (newRunConfirmationPanel != null)
        {
            OpenSubmenu(newRunConfirmationPanel);
        }
        else
        {
            Debug.LogWarning("[MainMenuController] Есть чекпоинт, но newRunConfirmationPanel не назначена — стартую без подтверждения.");
            BeginNewGame();
        }
    }

    /// <summary>
    /// Выбор сложности из панели difficultyPanel. Вешать на кнопки пресетов;
    /// presetId — Id из DifficultyCatalog (Easy/Medium/Hard/Martyr).
    /// </summary>
    public void SelectDifficulty(string presetId)
    {
        if (isExitingOrLoading) return;

        if (GameSessionProgression.Instance != null)
            GameSessionProgression.Instance.SetSelectedPresetById(presetId);

        _difficultyChosenForThisStart = true;

        // Закрываем панель сложности и продолжаем запуск (проверка чекпоинта
        // откроет подтверждение поверх уже возвращённого корневого меню).
        if (_menuStack.Count > 1 && _menuStack.Peek() == difficultyPanel)
            ForceCloseTopMenu();

        StartGame();
    }

    /// <summary>
    /// Вешать на кнопку "Да, начать заново" ВНУТРИ newRunConfirmationPanel.
    /// </summary>
    public void ConfirmStartNewGame()
    {
        if (isExitingOrLoading) return;
        BeginNewGame();
    }

    /// <summary>
    /// Фактический запуск новой попытки — сбрасывает прогрессию забега и
    /// чекпоинт (даже если предыдущий забег не был доигран до победы/смерти
    /// и чекпоинт не стёрся сам, новая попытка не должна его унаследовать),
    /// затем грузит стартовую сцену.
    /// </summary>
    private void BeginNewGame()
    {
        isExitingOrLoading = true;
        _difficultyChosenForThisStart = false;

        if (GameSessionProgression.Instance != null)
            GameSessionProgression.Instance.Reset();
        RunCheckpointStorage.Clear();

        // Новая попытка — статистика и плейтайм прошлой больше не считаются.
        // Сам отсчёт начнётся заново, когда загрузится стартовая сцена забега.
        RunStatsTracker.Instance?.EndRun();

        if (SceneLoader.Instance != null)
        {
            // Обучение пройдено — новая попытка с Level_1, иначе — с Tutorial.
            string targetScene = GameManager.GetNewRunSceneName(tutorialSceneName, firstGameplaySceneName);
            Debug.Log($"[MainMenuController] Новая попытка со сцены '{targetScene}' (обучение пройдено: {GameManager.IsTutorialCompleted()}).");
            SceneLoader.Instance.LoadScene(targetScene);
        }
        else
        {
            Debug.LogError("[MainMenuController] Нет SceneLoader.Instance. Добавь на сцену.");
        }
    }

    /// <summary>
    /// "Продолжить попытку". Кнопка неактивна, если чекпоинта нет (см.
    /// Start()), так что этот метод в норме вызывается только когда он
    /// точно есть — проверка ниже это подтверждающая, не основная логика.
    /// </summary>
    public void ContinueAttempt()
    {
        if (isExitingOrLoading) return;

        var checkpoint = RunCheckpointStorage.Load();
        if (checkpoint == null)
        {
            Debug.LogWarning("[MainMenuController] Нажата 'Продолжить попытку', но чекпоинта нет.");
            if (continueAttemptButton != null) continueAttemptButton.interactable = false;
            return;
        }

        isExitingOrLoading = true;

        if (GameSessionProgression.Instance != null)
            GameSessionProgression.Instance.PendingContinueFromCheckpoint = true;

        // Возвращаемся в тот же забег: время, проведённое в меню, в плейтайм
        // не входит, поэтому секундомер снова пускается.
        RunStatsTracker.Instance?.ResumeClock();

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(checkpoint.SceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(checkpoint.SceneName);
    }

    /// <summary>
    /// Выйти из игры. Вешать на кнопку "Да" в окне подтверждения выхода.
    /// </summary>
    public void ExitGame()
    {
        if (isExitingOrLoading) return;
        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator ExitGameRoutine()
    {
        isExitingOrLoading = true;

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
}