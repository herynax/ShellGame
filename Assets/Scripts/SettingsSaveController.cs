using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Живёт на том же объекте, что и SettingsMenuController (корень панели настроек).
/// Собирает все ISettingsModule (яркость, FOV, дисплей, звук...), следит за "грязным"
/// состоянием и реализует ICloseGuard — при попытке закрыть настройки с несохранёнными
/// изменениями показывает панель подтверждения вместо немедленного закрытия.
/// </summary>
public class SettingsSaveController : MonoBehaviour, ICloseGuard
{
    public static SettingsSaveController Instance { get; private set; }

    [Header("Панель подтверждения (поверх меню настроек)")]
    [SerializeField] private CanvasGroup unsavedChangesPanel;
    [SerializeField] private Button confirmSaveButton;   // "Да" — сохранить и выйти
    [SerializeField] private Button confirmDiscardButton; // "Нет" — откатить и выйти
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Кнопка сохранения на самой панели настроек")]
    [SerializeField] private Button saveButton;

    private readonly List<ISettingsModule> _modules = new List<ISettingsModule>();
    private bool _isDirty;
    private Action _pendingProceedClose;

    private void Awake()
    {
        Instance = this;

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveButtonPressed);

        if (confirmSaveButton != null)
            confirmSaveButton.onClick.AddListener(OnConfirmSaveAndExit);

        if (confirmDiscardButton != null)
            confirmDiscardButton.onClick.AddListener(OnConfirmDiscardAndExit);

        if (unsavedChangesPanel != null)
        {
            unsavedChangesPanel.alpha = 0f;
            unsavedChangesPanel.interactable = false;
            unsavedChangesPanel.blocksRaycasts = false;
            unsavedChangesPanel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Панель настроек снова открылась — берём свежий снэпшот и сбрасываем "грязный" флаг.
        _isDirty = false;
        foreach (var module in _modules)
            module.CaptureSnapshot();
    }

    /// <summary>
    /// Каждый ISettingsModule регистрируется здесь в своём Awake/OnEnable.
    /// </summary>
    public void RegisterModule(ISettingsModule module)
    {
        if (!_modules.Contains(module))
            _modules.Add(module);
    }

    public void UnregisterModule(ISettingsModule module)
    {
        _modules.Remove(module);
    }

    /// <summary>
    /// Дёргать из любого слайдера/тумблера при изменении значения — ПОСЛЕ применения
    /// превью, но вместо немедленного сохранения.
    /// </summary>
    public void MarkDirty()
    {
        _isDirty = true;
    }

    private void SaveButtonPressed()
    {
        SaveAll();
    }

    private void SaveAll()
    {
        foreach (var module in _modules)
            module.Save();

        _isDirty = false;
    }

    private void RevertAll()
    {
        foreach (var module in _modules)
            module.Revert();

        _isDirty = false;
    }

    // ==========================================
    // ICloseGuard — вызывается из MainMenuController/PauseController.CloseTopMenu()
    // ==========================================

    public void RequestClose(Action proceedClose)
    {
        if (!_isDirty)
        {
            proceedClose();
            return;
        }

        _pendingProceedClose = proceedClose;
        ShowConfirmPanel();
    }

    private void ShowConfirmPanel()
    {
        if (unsavedChangesPanel == null)
        {
            // Нет панели подтверждения — на всякий случай просто сохраняем и выходим,
            // чтобы не блокировать игрока намертво.
            SaveAll();
            _pendingProceedClose?.Invoke();
            _pendingProceedClose = null;
            return;
        }

        unsavedChangesPanel.gameObject.SetActive(true);
        unsavedChangesPanel.alpha = 0f;
        unsavedChangesPanel.interactable = true;
        unsavedChangesPanel.blocksRaycasts = true;
        unsavedChangesPanel.DOKill();
        unsavedChangesPanel.DOFade(1f, fadeDuration).SetUpdate(true);
    }

    private void HideConfirmPanel()
    {
        if (unsavedChangesPanel == null) return;

        unsavedChangesPanel.interactable = false;
        unsavedChangesPanel.blocksRaycasts = false;
        unsavedChangesPanel.DOKill();
        unsavedChangesPanel.DOFade(0f, fadeDuration).SetUpdate(true)
            .OnComplete(() => unsavedChangesPanel.gameObject.SetActive(false));
    }

    private void OnConfirmSaveAndExit()
    {
        SaveAll();
        HideConfirmPanel();

        _pendingProceedClose?.Invoke();
        _pendingProceedClose = null;
    }

    private void OnConfirmDiscardAndExit()
    {
        RevertAll();
        HideConfirmPanel();

        _pendingProceedClose?.Invoke();
        _pendingProceedClose = null;
    }
}