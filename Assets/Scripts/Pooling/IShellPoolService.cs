using UnityEngine;
using ShellGame.Shells;

namespace ShellGame.Pooling
{
    /// <summary>
    /// Абстракция над пулом наперстков. Геймплейный код (ShellsTableController)
    /// работает через этот интерфейс, а не через LeanPool напрямую — если
    /// пуллер когда-нибудь сменится, поменяется только реализация.
    /// </summary>
    public interface IShellPoolService
    {
        /// <summary>Прогрев пула — заранее создать N инстансов, чтобы первый раунд не спавнил объекты "на лету".</summary>
        void Prewarm(int count);

        Shell Spawn(Vector3 position, Quaternion rotation);
        void Despawn(Shell shell);

        /// <summary>Вернуть в пул все выданные на данный момент наперстки (конец раунда/уровня).</summary>
        void DespawnAll();
    }
}
