// START OF FILE UnlockNotificationScreenController.cs
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using ShellGame.Items;
using ShellGame.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShellGame.UI
{
    public sealed class UnlockNotificationScreenController : MonoBehaviour
    {
        [Header("Панель")]
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Button _continueButton;

        [Header("Элементы предмета")]
        [SerializeField] private TMP_Text _titleText; // "Поздравляем, вы открыли:"
        [SerializeField] private TMP_Text _itemNameText;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private TMP_Text _itemDescText;

        [Header("Тайминги")]
        [SerializeField] private float _panelFadeDuration = 0.3f;

        private bool _continueRequested;

        private void Awake()
        {
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 0f;
                _panelGroup.blocksRaycasts = false;
            }

            if (_continueButton != null)
            {
                    _continueButton.onClick.AddListener(() =>
                    {
                        _continueButton.gameObject.SetActive(false); // <--- Кнопка мгновенно пропадает при клике
                        _continueRequested = true;
                    });
            }

            gameObject.SetActive(false);
        }

        public IEnumerator ShowSequence(List<ItemDefinition> unlockedItems, IUnlockManager unlockManager)
        {
            gameObject.SetActive(true);
            
            if (_panelGroup != null)
            {
                _panelGroup.blocksRaycasts = true;
                _panelGroup.DOFade(1f, _panelFadeDuration).SetUpdate(true);
            }

            // Показываем предметы по очереди, если их открылось несколько за раз
            foreach (var item in unlockedItems)
            {
                _continueRequested = false;

                if (_itemNameText != null) _itemNameText.text = item.DisplayName;
                if (_itemDescText != null) _itemDescText.text = item.TooltipDescription;
                if (_itemIcon != null && item.Icon != null) 
                {
                    _itemIcon.sprite = item.Icon;
                    _itemIcon.gameObject.SetActive(true);
                }
                else if (_itemIcon != null)
                {
                    _itemIcon.gameObject.SetActive(false);
                }

                // Легкая анимация "Поп-ап" для каждого нового предмета
                if (_panelRoot != null)
                {
                    _panelRoot.localScale = Vector3.one * 0.8f;
                    _panelRoot.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
                }

                while (!_continueRequested) yield return null;

                // Сохраняем, что игрок увидел этот анлок
                unlockManager.AcknowledgeUnlock(item);
            }

            if (_panelGroup != null)
            {
                yield return _panelGroup.DOFade(0f, _panelFadeDuration).SetUpdate(true).WaitForCompletion();
                _panelGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }
    }
}
// END OF FILE