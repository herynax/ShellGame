using ShellGame.Gameplay;
using ShellGame.Items;
using ShellGame.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace ShellGame.UI
{
    public class UnlocksMenuController : MonoBehaviour
    {
        [Header("UI Ссылки")]
        [SerializeField] private Transform _slotsContainer;
        [SerializeField] private UnlockSlotView _unlockSlotPrefab;

        [Header("Сброс прогресса")]
        [Tooltip("Опционально: панель подтверждения ('вы уверены?') — если назначена, кнопка 'Сбросить прогресс' сначала открывает её (через тот же CanvasGroup-паттерн, что и в MainMenuController), а фактический сброс вызывается отдельно из кнопки 'Да' внутри неё через ConfirmResetProgress(). Если не назначена — ResetProgress() сбрасывает сразу, без вопросов.")]
        [SerializeField] private CanvasGroup _resetConfirmationPanel;
        [Tooltip("Имя сцены, которая загрузится после сброса прогресса.")]
        [SerializeField] private string _tutorialSceneName = "Tutorial";

        [Inject] private IUnlockManager _unlockManager;
        [Inject] private UnlocksConfig _unlocksConfig;
        [Inject] private IGlobalProgressService _globalProgress;

        private void OnEnable()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            foreach (Transform child in _slotsContainer)
            {
                Destroy(child.gameObject);
            }

            if (_unlockManager == null || _unlocksConfig == null) 
                return;

            foreach (var entry in _unlockManager.GetAllEntries())
            {
                UnlockSlotView slot = Instantiate(_unlockSlotPrefab, _slotsContainer);
                bool isUnlocked = _unlockManager.IsUnlocked(entry.Item);

                if (isUnlocked)
                {
                    slot.Setup(
                        title: entry.Item.DisplayName,
                        description: entry.Item.TooltipDescription,
                        icon: entry.Item.Icon
                    );
                }
                else
                {
                    slot.Setup(
                        title: "???",
                        description: entry.LockedHintText,
                        icon: _unlocksConfig.UnknownIcon
                    );
                }
            }
        }

        /// <summary>Вешать на кнопку "Сбросить прогресс".</summary>
        public void ResetProgress()
        {
            if (_resetConfirmationPanel != null)
            {
                _resetConfirmationPanel.gameObject.SetActive(true);
                _resetConfirmationPanel.alpha = 1f;
                _resetConfirmationPanel.interactable = true;
                _resetConfirmationPanel.blocksRaycasts = true;
                return;
            }

            Debug.LogWarning("[UnlocksMenuController] _resetConfirmationPanel не назначена — сбрасываю прогресс без подтверждения. Рекомендую добавить панель подтверждения, это необратимое действие.");
            ConfirmResetProgress();
        }

        /// <summary>Вешать на кнопку "Да, сбросить" ВНУТРИ _resetConfirmationPanel.</summary>
        public void ConfirmResetProgress()
        {
            _globalProgress?.ResetAll();

            if (_unlocksConfig != null)
            {
                foreach (var entry in _unlocksConfig.Entries)
                {
                    if (entry.Item != null)
                        PlayerPrefs.DeleteKey("AckUnlock_" + entry.Item.name);
                }
            }

            RunCheckpointStorage.Clear();
            PlayerPrefs.DeleteKey(GameManager.TutorialCompletedPrefKey);
            PlayerPrefs.Save();

            if (GameSessionProgression.Instance != null)
                GameSessionProgression.Instance.Reset();

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(_tutorialSceneName);
            else
                SceneManager.LoadScene(_tutorialSceneName);
        }
    }
}