using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Core
{
    /// <summary>
    /// Задержка перехода между уровнями. Нужен, чтобы финальные реплики врага
    /// (победа/поражение) успели договорить ДО того, как SceneLoader начнёт
    /// затемнение и загрузку следующей сцены.
    ///
    /// Отличается от GameplayGate тем, что раунд-луп к этому моменту уже
    /// остановлен (GameOver), поэтому блокировать там нечего — блокируется
    /// именно сам переход в SceneLoader.HandleSideDied.
    /// </summary>
    public static class SceneTransitionGate
    {
        private static readonly HashSet<object> _holders = new HashSet<object>();

        public static bool IsHeld => _holders.Count > 0;

        public static void Hold(object owner)
        {
            if (owner != null)
                _holders.Add(owner);
        }

        public static void Release(object owner)
        {
            if (owner != null)
                _holders.Remove(owner);
        }

        public static void Clear() => _holders.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _holders.Clear();
    }
}
