using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;
using ShellGame.Shells;

namespace ShellGame.Pooling
{
    /// <summary>
    /// Обёртка над LeanPool специально под наперстки.
    /// LeanPool сам умеет пуллить по префабу (LeanPool.Spawn/Despawn), но мы
    /// заворачиваем это в отдельный сервис, чтобы:
    ///  1) не размазывать вызовы LeanPool по всему геймплейному коду;
    ///  2) централизованно вызывать IPoolResettable хуки;
    ///  3) иметь единый список "что сейчас выдано", чтобы уметь DespawnAll().
    /// </summary>
    public sealed class ShellPoolService : IShellPoolService
    {
        private readonly Shell _shellPrefab;
        private readonly Transform _poolParent;
        private readonly HashSet<Shell> _active = new HashSet<Shell>();

        public ShellPoolService(Shell shellPrefab, Transform poolParent = null)
        {
            _shellPrefab = shellPrefab;
            _poolParent = poolParent;
        }

        public void Prewarm(int count)
        {
            // LeanPool не имеет отдельного API "прогреть N штук" — эмулируем
            // спавном и немедленным деспавном, это заполняет внутренний пул
            // инстансов под конкретный префаб без затрат в момент реального раунда.
            var warm = new Shell[count];
            for (int i = 0; i < count; i++)
            {
                warm[i] = LeanPool.Spawn(_shellPrefab, Vector3.zero, Quaternion.identity, _poolParent);
            }

            for (int i = 0; i < count; i++)
            {
                LeanPool.Despawn(warm[i]);
            }
        }

        public Shell Spawn(Vector3 position, Quaternion rotation)
        {
            var instance = LeanPool.Spawn(_shellPrefab, position, rotation, _poolParent);
            instance.transform.localScale = _shellPrefab.transform.localScale;
            _active.Add(instance);
            instance.OnSpawnFromPool();
            return instance;
        }

        public void Despawn(Shell shell)
        {
            if (shell == null || !_active.Contains(shell))
                return;

            shell.OnReturnToPool();
            _active.Remove(shell);
            LeanPool.Despawn(shell);
        }

        public void DespawnAll()
        {
            // Копия, т.к. Despawn модифицирует _active во время итерации.
            var snapshot = new List<Shell>(_active);
            foreach (var shell in snapshot)
            {
                Despawn(shell);
            }
        }
    }
}
