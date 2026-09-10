using ShellGame.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShellGame.UI
{
    public class UnlockSlotView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _descText;
        [SerializeField] private Image _iconImage;

        /// <summary>
        /// Инициализирует слот данными
        /// </summary>
        public void Setup(string title, string description, Sprite icon)
        {
            if (_nameText != null) 
                _nameText.text = title;

            if (_descText != null) 
                _descText.text = description;

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null; // Скрываем Image, если спрайта нет
            }
        }
    }
}