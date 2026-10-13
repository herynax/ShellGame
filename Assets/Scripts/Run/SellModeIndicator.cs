using ShellGame.Items;
using UnityEngine;

namespace ShellGame.Run
{
    /// <summary>
    /// Индикатор режима продажи. Управляет подсказками и подсветкой предметов инвентаря игрока.
    /// </summary>
    public sealed class SellModeIndicator : MonoBehaviour
    {
        public static SellModeIndicator Instance { get; private set; }

        [SerializeField] private GameObject _hintRoot;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Deactivate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Подсветить или снять подсветку с предмета для продажи.
        /// </summary>
        public void HighlightItem(ItemPickupView pickup, bool highlight)
        {
            if (pickup != null)
            {
                pickup.SetHovered(highlight);
            }
        }

        /// <summary>
        /// Включить визуальное отображение режима продажи (подсказки, плашки).
        /// </summary>
        public void Activate()
        {
            if (_hintRoot != null)
                _hintRoot.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        /// <summary>
        /// Отключить визуальное отображение режима продажи.
        /// </summary>
        public void Deactivate()
        {
            if (_hintRoot != null)
                _hintRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }
}