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
        [SerializeField] private float _visibleDuration = 3f;
        [SerializeField] private float _fadeInDuration = 0.12f;
        [SerializeField] private float _fadeOutDuration = 0.8f;

        private Coroutine _hideRoutine;
        private Color _baseColor;

        private void Awake()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();

            _baseColor = _text.color;
            _text.text = string.Empty;
        }

        public void Show(ItemDefinition item)
        {
            if (_text == null || item == null)
                return;

            ShowTimedMessage($"Вы использовали предмет: {item.UseDescription}");
        }

        public void ShowUnavailable()
        {
            if (_text == null)
                return;

            ShowTimedMessage("Вы не можете это использовать");
        }

        /// <summary>Показывает сообщение и НЕ прячет его по таймеру — для случаев, когда нужно дождаться действия игрока (например, Монокль: "выберите наперсток"). Убрать вручную через ClearMessage().</summary>
        public void ShowPersistent(string message)
        {
            if (_text == null)
                return;

            StopHideTimer();
            _text.text = message;
            SetAlpha(1f);
        }

        /// <summary>Немедленно убирает текущее сообщение (в т.ч. персистентное) без ожидания таймера.</summary>
        public void ClearMessage()
        {
            if (_text == null)
                return;

            StopHideTimer();
            _text.text = string.Empty;
            SetAlpha(0f);
        }

        private void ShowTimedMessage(string message)
        {
            StopHideTimer();
            _text.text = message;
            _hideRoutine = StartCoroutine(ShowAndHide());
        }

        private void StopHideTimer()
        {
            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        private IEnumerator ShowAndHide()
        {
            yield return FadeTo(1f, _fadeInDuration);
            yield return new WaitForSeconds(Mathf.Max(0f, _visibleDuration));
            yield return FadeTo(0f, _fadeOutDuration);
            _text.text = string.Empty;
            _hideRoutine = null;
        }

        public void ShowMessage(string message)
        {
            if (_text == null) return;
            ShowTimedMessage(message);
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            float startAlpha = _text.color.a;
            float safeDuration = Mathf.Max(0f, duration);

            if (safeDuration <= 0f)
            {
                SetAlpha(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / safeDuration));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        private void SetAlpha(float alpha)
        {
            if (_text == null)
                return;

            Color color = _baseColor;
            color.a *= Mathf.Clamp01(alpha);
            _text.color = color;
        }
    }
}