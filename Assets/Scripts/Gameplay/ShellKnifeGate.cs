// START OF FILE ShellKnifeGate.cs
using System;
using ShellGame.Shells;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Шлюз для предмета "Нож". Похож на ShellPeekGate, но имеет свою логику:
    /// перехватывает следующий клик игрока по наперстку, чтобы нанести урон 
    /// врагу или самому себе в зависимости от наличия метки.
    /// </summary>
    public static class ShellKnifeGate
    {
        public static bool IsPending { get; private set; }

        private static float _holdDuration;
        private static Action<Shell> _onTargetSelected;

        public static void Begin(float holdDuration, Action<Shell> onTargetSelected = null)
        {
            IsPending = true;
            _holdDuration = holdDuration;
            _onTargetSelected = onTargetSelected;
        }

        public static void Cancel()
        {
            IsPending = false;
            _onTargetSelected = null;
        }

        public static bool TryConsume(Shell shell, out float holdDuration, out Action<Shell> onTargetSelected)
        {
            holdDuration = _holdDuration;
            onTargetSelected = _onTargetSelected;

            if (!IsPending)
                return false;

            IsPending = false;
            _onTargetSelected = null;
            return true;
        }
    }
}
// END OF FILE