// START OF FILE CrossVisual.cs
using DG.Tweening;
using FMODUnity;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Визуальное отображение активного деревянного креста.
    /// Автоматически подписывается на поломку щита своей стороны.
    /// </summary>
    public class CrossVisual : MonoBehaviour
    {
        private TurnSide _owner;
        private EventReference _breakSound;
        private bool _isBroken;

        public void Initialize(TurnSide owner, EventReference breakSound)
        {
            _owner = owner;
            _breakSound = breakSound;
            
            // Анимация появления (пружинит из ниоткуда)
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            
            // Покачивание в воздухе (левитация)
            transform.DOMoveY(transform.position.y + 0.15f, 1.2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnEnable()
        {
            GameEvents.ShieldBroken += OnShieldBroken;
        }

        private void OnDisable()
        {
            GameEvents.ShieldBroken -= OnShieldBroken;
            transform.DOKill();
        }

        private void OnShieldBroken(TurnSide side)
        {
            if (side != _owner || _isBroken) return;
            _isBroken = true;

            // Убиваем покачивание
            transform.DOKill(); 

            // Звук поломки
            if (!_breakSound.IsNull)
                RuntimeManager.PlayOneShot(_breakSound, transform.position);

            // Анимация разрушения: жестко трясется, затем сжимается в ноль и удаляется
            Sequence breakSeq = DOTween.Sequence();
            breakSeq.Append(transform.DOShakeRotation(0.25f, 60f, 20, 90f));
            breakSeq.Append(transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            breakSeq.OnComplete(() => Destroy(gameObject));
        }
    }
}
// END OF FILE