using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using ShellGame.Audio;
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

    [Header("Настройки анимации (для Fallback)")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Настройки сцены")]
    [Tooltip("Имя сцены, которая загрузится при нажатии 'Играть'")]
    [SerializeField] private string firstGameplaySceneName = "Tutorial";

    private Stack<CanvasGroup> _menuStack = new Stack<CanvasGroup>();
    private bool isExitingOrLoading = false;

    private void Start()
    {
        // Убеждаемся, что при старте показано только корневое меню
        if (rootMenuCanvasGroup != null)
        {
            _menuStack.Push(rootMenuCanvasGroup);
            PlayMenuIn(rootMenuCanvasGroup);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || isExitingOrLoading) return;
        
        // Обработка Escape
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Если мы находимся в подменю (в стеке больше 1 элемента) - закрываем его
            if (_menuStack.Count > 1)
            {
                CloseTopMenu();
            }
            // Если в стеке только корневое меню - ничего не делаем 
            // (или можно здесь вызывать окно подтверждения выхода, если хочешь)
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
        if (_menuStack.Count <= 1 || isExitingOrLoading) return;

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
    /// Начать игру. Вешать на кнопку "Играть".
    /// </summary>
    public void StartGame()
    {
        if (isExitingOrLoading) return;
        isExitingOrLoading = true;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(firstGameplaySceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(firstGameplaySceneName);
        }
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