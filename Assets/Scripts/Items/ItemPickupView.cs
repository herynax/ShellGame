using System;
using ShellGame.Tweening;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Предмет как объект в 3D-мире (на столе, в магазине — где угодно).
    /// Ховер поднимает и увеличивает его (ItemHoverAnimator), клик кидает
    /// событие Used — что делать дальше (добавить в инвентарь, списать
    /// деньги, закрыть магазин и т.д.) решает подписчик. Спавн/пул таких
    /// объектов в мире — отдельная задача, здесь только сам объект.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(ItemHoverAnimator))]
    public sealed class ItemPickupView : MonoBehaviour
    {
        [SerializeField] private ItemDefinition _item;

        private ItemHoverAnimator _hoverAnimator;
        private bool _isHovered;

        public ItemDefinition Item => _item;

        public event Action<ItemPickupView> Used;

        private void Awake()
        {
            _hoverAnimator = GetComponent<ItemHoverAnimator>();
            if (_item != null)
                _hoverAnimator.Configure(_item.HoverLiftHeight, _item.HoverScaleMultiplier, _item.HoverTweenDuration);
        }

        public void SetItem(ItemDefinition item)
        {
            _item = item;
            if (_item != null && _hoverAnimator != null)
                _hoverAnimator.Configure(_item.HoverLiftHeight, _item.HoverScaleMultiplier, _item.HoverTweenDuration);
        }

        private void OnMouseEnter()
        {
            if (_isHovered) return;
            _isHovered = true;
            _hoverAnimator.PlayHoverEnter();
        }

        private void OnMouseExit()
        {
            if (!_isHovered) return;
            _isHovered = false;
            _hoverAnimator.PlayHoverExit();
        }

        private void OnMouseDown()
        {
            Used?.Invoke(this);
        }
    }
}
