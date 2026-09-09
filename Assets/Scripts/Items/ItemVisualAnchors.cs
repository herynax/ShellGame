// START OF FILE ItemVisualAnchors.cs
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Хранилище точек в сцене, куда должны подлетать и где должны парить предметы.
    /// Настраивается вручную геймдизайнером в сцене.
    /// </summary>
    public class ItemVisualAnchors : MonoBehaviour
    {
        public static ItemVisualAnchors Instance { get; private set; }

        [Header("Нож (Где висит перед броском)")]
        [Tooltip("Точка перед камерой игрока. Задает позицию и ПОВОРОТ лезвия.")]
        public Transform PlayerKnifeHoverPoint;
        public Transform EnemyKnifeHoverPoint;

        [Header("Крест (Где висит щит)")]
        public Transform PlayerCrossHoverPoint;
        public Transform EnemyCrossHoverPoint;

        [Header("Куда прилетает урон (Цели для ножа/молотка)")]
        public Transform PlayerHitPoint;
        public Transform EnemyHitPoint;

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
// END OF FILE