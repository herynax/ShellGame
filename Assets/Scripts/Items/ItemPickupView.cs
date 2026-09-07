// ItemPickupView.cs
using System;
using System.Collections;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Tweening;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Предмет как объект в 3D-мире (на столе, в магазине — где угодно).
    /// Ховер поднимает и увеличивает его (ItemHoverAnimator), долгий ховер
    /// (ItemDefinition.TooltipHoverDelay) показывает тултип с описанием
    /// (ItemTooltipView), клик кидает событие Used — что делать дальше
    /// (добавить в инвентарь, списать деньги, закрыть магазин и т.д.)
    /// решает подписчик.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(ItemHoverAnimator))]
    public sealed class ItemPickupView : MonoBehaviour
    {
        [SerializeField] private ItemDefinition _item;

        private ItemHoverAnimator _hoverAnimator;
        private bool _isHovered;
        private TurnSide _owner;
        private Coroutine _tooltipRoutine;

        private IAudioService Audio => ServiceLocator.TryGet(out IAudioService audio) ? audio : null;

        public ItemDefinition Item => _item;
        public TurnSide Owner => _owner;

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
            if (_hoverAnimator == null)
                _hoverAnimator = GetComponent<ItemHoverAnimator>() ?? gameObject.AddComponent<ItemHoverAnimator>();

            if (_item != null)
                _hoverAnimator.Configure(_item.HoverLiftHeight, _item.HoverScaleMultiplier, _item.HoverTweenDuration);
        }

        public void SetOwner(TurnSide owner)
        {
            bool wasHovered = _isHovered;
            _owner = owner;
            _isHovered = false;
            _hoverAnimator?.PlayHoverExit();
            if (wasHovered && _item != null)
                PlayHoverSound(_item.HoverExitSound);
            StopTooltipTimer();
            ItemTooltipView.Instance?.Hide(this);
        }

        private bool IsInteractive => _owner == TurnSide.Player;

        private void OnMouseEnter()
        {
            if (!IsInteractive || _isHovered) return;
            _isHovered = true;
            if (_item != null)
                PlayHoverSound(_item.HoverEnterSound);
            _hoverAnimator.PlayHoverEnter();
            StartTooltipTimer();
        }

        private void OnMouseExit()
        {
            if (!IsInteractive || !_isHovered) return;
            _isHovered = false;
            _hoverAnimator.PlayHoverExit();
            if (_item != null)
                PlayHoverSound(_item.HoverExitSound);
            StopTooltipTimer();
            ItemTooltipView.Instance?.Hide(this);
        }

        private void PlayHoverSound(FMODUnity.EventReference sound)
        {
            Audio?.PlayOneShot(sound, transform.position);
        }

        private void OnMouseDown()
        {
            if (!IsInteractive) return;
            StopTooltipTimer();
            ItemTooltipView.Instance?.Hide(this);
            Used?.Invoke(this);
        }

        private void StartTooltipTimer()
        {
            StopTooltipTimer();
            if (_item == null) return;
            _tooltipRoutine = StartCoroutine(ShowTooltipAfterDelay());
        }

        private void StopTooltipTimer()
        {
            if (_tooltipRoutine == null) return;
            StopCoroutine(_tooltipRoutine);
            _tooltipRoutine = null;
        }

        private IEnumerator ShowTooltipAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _item.TooltipHoverDelay));
            ItemTooltipView.Instance?.Show(_item.TooltipDescription, transform.position, this);
        }

        private void OnDisable()
        {
            if (_isHovered && _item != null)
                PlayHoverSound(_item.HoverExitSound);
            _isHovered = false;
            StopTooltipTimer();
            ItemTooltipView.Instance?.Hide(this);
        }
    }
}