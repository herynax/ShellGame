// ShellGame.Gameplay / ShellPeekGate.cs
using System;
using ShellGame.Shells;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Шлюз между расходуемыми предметами (сейчас — Монокль) и обычным
    /// циклом выбора наперстка. Пока шлюз активен, СЛЕДУЮЩИЙ клик по
    /// наперстку (Shell.Select()) не считается финальным выбором раунда —
    /// вместо этого наперсток просто "подглядывается" (Shell.RevealMarker),
    /// после чего шлюз сам себя гасит и все дальнейшие клики снова работают
    /// как обычный Select(). Единственный источник правды здесь, чтобы
    /// Shell не знал про конкретные предметы — только про факт "сейчас peek".
    /// </summary>
    public static class ShellPeekGate
    {
        public static bool IsPending { get; private set; }

        private static float _holdDuration;
        private static Action<Shell> _onPeeked;

        public static void Begin(float holdDuration, Action<Shell> onPeeked = null)
        {
            IsPending = true;
            _holdDuration = holdDuration;
            _onPeeked = onPeeked;
        }

        /// <summary>Отменяет ожидание без последствий (например, если раунд закончился раньше, чем игрок кликнул).</summary>
        public static void Cancel()
        {
            IsPending = false;
            _onPeeked = null;
        }

        /// <summary>Вызывается изнутри Shell.Select(). Если шлюз был активен — гасит его и возвращает параметры peek.</summary>
        public static bool TryConsume(Shell shell, out float holdDuration, out Action<Shell> onPeeked)
        {
            holdDuration = _holdDuration;
            onPeeked = _onPeeked;

            if (!IsPending)
                return false;

            IsPending = false;
            _onPeeked = null;
            return true;
        }
    }
}