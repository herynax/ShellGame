using UnityEngine;
using FMODUnity;

namespace ShellGame.Health
{
    /// <summary>
    /// Хранилище звуков и позиций для текущей сцены. 
    /// HealthController обращается к нему для получения нужных аудиособытий.
    /// </summary>
    public class HealthSoundProvider : MonoBehaviour
    {
        public static HealthSoundProvider Instance { get; private set; }

        [Header("Точки для 3D звуков (откуда исходит звук)")]
        public Transform playerTransform;
        public Transform enemyTransform;

        [Header("Звуки: Укол (2D)")]
        public EventReference injectionSound;

        [Header("Звуки: Игрок (3D)")]
        public EventReference playerDamageSound;
        public EventReference playerDeathSound;

        [Header("Звуки: Враг (3D)")]
        public EventReference enemyDamageSound;
        public EventReference enemyDeathSound;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}