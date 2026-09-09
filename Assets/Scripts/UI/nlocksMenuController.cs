using System.Collections.Generic;
using ShellGame.Items;
using ShellGame.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace ShellGame.UI
{
    public class UnlocksMenuController : MonoBehaviour
    {
        [Header("UI Ссылки")]
        public Transform SlotsContainer;
        public GameObject UnlockSlotPrefab;

        [Inject] private IUnlockManager _unlockManager;
        [Inject] private UnlocksConfig _unlocksConfig;

        private void OnEnable()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            // Очищаем старые слоты
            foreach (Transform child in SlotsContainer)
            {
                Destroy(child.gameObject);
            }

            if (_unlockManager == null || _unlocksConfig == null) return;

            // Создаем слоты по конфигу
            foreach (var entry in _unlockManager.GetAllEntries())
            {
                GameObject slotObj = Instantiate(UnlockSlotPrefab, SlotsContainer);
                var uiTexts = slotObj.GetComponentsInChildren<TMP_Text>();
                var uiImages = slotObj.GetComponentsInChildren<Image>();

                TMP_Text nameText = uiTexts.Length > 0 ? uiTexts[0] : null;
                TMP_Text descText = uiTexts.Length > 1 ? uiTexts[1] : null;
                Image iconImage = uiImages.Length > 0 ? uiImages[0] : null; // Предполагаем что первая картинка это иконка

                bool isUnlocked = _unlockManager.IsUnlocked(entry.Item);

                if (isUnlocked)
                {
                    if (nameText != null) nameText.text = entry.Item.DisplayName;
                    if (descText != null) descText.text = entry.Item.TooltipDescription;
                    if (iconImage != null && entry.Item.Icon != null) 
                        iconImage.sprite = entry.Item.Icon;
                }
                else
                {
                    if (nameText != null) nameText.text = "???";
                    if (descText != null) descText.text = entry.LockedHintText;
                    if (iconImage != null && _unlocksConfig.UnknownIcon != null) 
                        iconImage.sprite = _unlocksConfig.UnknownIcon;
                }
            }
        }
    }
}