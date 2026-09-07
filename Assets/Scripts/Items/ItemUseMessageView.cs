// ItemUseMessageView.cs
using System.Collections;
using TMPro;
using UnityEngine;

namespace ShellGame.Items
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ItemUseMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _visibleDuration = 2f;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            _text.text = string.Empty;
        }

        public void Show(ItemDefinition item)
        {
            if (_text == null || item == null)
                return;

            _text.text = $"Вы использовали предмет: {item.UseDescription}";
            RestartHideTimer();
        }

        public void ShowUnavailable()
        {
            if (_text == null)
                return;

            _text.text = "Вы не можете это использовать";
            RestartHideTimer();
        }

        /// <summary>Показывает сообщение и НЕ прячет его по таймеру — для случаев, когда нужно дождаться действия игрока (например, Монокль: "выберите наперсток"). Убрать вручную через ClearMessage().</summary>
        public void ShowPersistent(string message)
        {
            if (_text == null)
                return;

            StopHideTimer();
            _text.text = message;
        }

        /// <summary>Немедленно убирает текущее сообщение (в т.ч. персистентное) без ожидания таймера.</summary>
        public void ClearMessage()
        {
            if (_text == null)
                return;

            StopHideTimer();
            _text.text = string.Empty;
        }

        private void RestartHideTimer()
        {
            StopHideTimer();
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private void StopHideTimer()
        {
            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _visibleDuration));
            _text.text = string.Empty;
            _hideRoutine = null;
        }

        public void ShowMessage(string message)
        {
            if (_text == null) return;
            _text.text = message;
            RestartHideTimer();
        }
    }
}