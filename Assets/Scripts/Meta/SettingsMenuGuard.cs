using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Живёт на Canvas/SettingsMenu (UI). Отвечает за кнопку "Сохранить" 
/// и панель "Несохранённые изменения" при попытке закрыть меню.
/// </summary>
public class SettingsMenuGuard : MonoBehaviour, ICloseGuard
{
    [Header("Панель подтверждения")]
    [SerializeField] private CanvasGroup unsavedChangesPanel;
    [SerializeField] private Button confirmSaveButton;
    [SerializeField] private Button confirmDiscardButton;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Кнопка сохранения")]
    [SerializeField] private Button saveButton;

    private Action _pendingProceedClose;

    private void Awake()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonPressed);

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
        SettingsSaveController.Instance?.CaptureSnapshotAll();
    }

    private void OnSaveButtonPressed()
    {
        SettingsSaveController.Instance?.SaveAll();
    }

    public void RequestClose(Action proceedClose)
    {
        if (SettingsSaveController.Instance == null || !SettingsSaveController.Instance.IsDirty)
        {
            proceedClose?.Invoke();
            return;
        }

        _pendingProceedClose = proceedClose;
        ShowConfirmPanel();
    }

    private void ShowConfirmPanel()
    {
        if (unsavedChangesPanel == null)
        {
            SettingsSaveController.Instance?.SaveAll();
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
        SettingsSaveController.Instance?.SaveAll();
        HideConfirmPanel();
        _pendingProceedClose?.Invoke();
        _pendingProceedClose = null;
    }

    private void OnConfirmDiscardAndExit()
    {
        SettingsSaveController.Instance?.RevertAll();
        HideConfirmPanel();
        _pendingProceedClose?.Invoke();
        _pendingProceedClose = null;
    }
}