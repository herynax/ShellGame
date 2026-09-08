using DG.Tweening;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Плейсхолдер-фидбек на попадание по противнику: тряска модели +
    /// punch-масштаб + цветовая вспышка (через MaterialPropertyBlock, без
    /// создания лишних инстансов материала). Когда появится риг/анимации
    /// противника, эти твины заменяются на Animator.SetTrigger(...) —
    /// подписка и лог урона (в базовом классе) не меняются.
    /// </summary>
    public sealed class EnemyDamageFeedback : DamageFeedbackBase
    {
        [Header("Тряска модели")]
        [SerializeField] private Transform _enemyModelTransform;
        [SerializeField] private float _shakeDuration = 0.3f;
        [SerializeField] private float _shakeStrength = 0.15f;
        [SerializeField] private int _shakeVibrato = 25;
        [SerializeField] private float _shakeRandomness = 90f;

        [Header("Punch-масштаб")]
        [SerializeField] private Vector3 _scalePunch = new Vector3(0.15f, 0.15f, 0.15f);
        [SerializeField] private float _scalePunchDuration = 0.25f;
        [SerializeField] private int _scalePunchVibrato = 8;

        [Header("Цветовая вспышка (плейсхолдер)")]
        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _flashDuration = 0.2f;
        [SerializeField] private string _colorPropertyName = "_Color";

        private Vector3 _modelBasePosition;
        private Vector3 _modelBaseScale;
        private Renderer[] _enemyRenderers;
        private Color[] _rendererBaseColors;
        private bool[] _rendererHasColorProperty;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _shakeTween;
        private Tween _scaleTween;
        private Tween _flashTween;

        protected override TurnSide WatchedSide => TurnSide.Enemy;

        protected override void Awake()
        {
            base.Awake();

            if (_enemyModelTransform != null)
            {
                _modelBasePosition = _enemyModelTransform.localPosition;
                _modelBaseScale = _enemyModelTransform.localScale;
            }

            if (_enemyModelTransform != null)
            {
                _propertyBlock = new MaterialPropertyBlock();
                _enemyRenderers = _enemyModelTransform.GetComponentsInChildren<Renderer>(includeInactive: true);
                _rendererBaseColors = new Color[_enemyRenderers.Length];
                _rendererHasColorProperty = new bool[_enemyRenderers.Length];

                for (int i = 0; i < _enemyRenderers.Length; i++)
                {
                    Material material = _enemyRenderers[i].sharedMaterial;
                    _rendererHasColorProperty[i] = material != null && material.HasProperty(_colorPropertyName);
                    _rendererBaseColors[i] = _rendererHasColorProperty[i]
                        ? material.GetColor(_colorPropertyName)
                        : Color.white;
                }
            }
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            ShakeModel();
            PunchScale();
            FlashColor();
        }

        private void ShakeModel()
        {
            if (_enemyModelTransform == null) return;

            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill();
                _enemyModelTransform.localPosition = _modelBasePosition;
            }

            _shakeTween = _enemyModelTransform.DOShakePosition(
                _shakeDuration, _shakeStrength, _shakeVibrato, _shakeRandomness, fadeOut: true);
        }

        private void PunchScale()
        {
            if (_enemyModelTransform == null) return;

            if (_scaleTween != null && _scaleTween.IsActive())
            {
                _scaleTween.Kill();
                _enemyModelTransform.localScale = _modelBaseScale;
            }

            _scaleTween = _enemyModelTransform.DOPunchScale(_scalePunch, _scalePunchDuration, _scalePunchVibrato);
        }

        private void FlashColor()
        {
            if (_enemyRenderers == null || _enemyRenderers.Length == 0 || _propertyBlock == null) return;

            _flashTween?.Kill();
            _flashTween = DOTween.Sequence()
                .AppendCallback(() => SetRenderersColor(_flashColor))
                .AppendInterval(_flashDuration)
                .AppendCallback(RestoreRenderersColor);
        }

        private void SetRenderersColor(Color color)
        {
            for (int i = 0; i < _enemyRenderers.Length; i++)
            {
                if (!_rendererHasColorProperty[i]) continue;

                _enemyRenderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(_colorPropertyName, color);
                _enemyRenderers[i].SetPropertyBlock(_propertyBlock);
            }
        }

        private void RestoreRenderersColor()
        {
            for (int i = 0; i < _enemyRenderers.Length; i++)
            {
                if (!_rendererHasColorProperty[i]) continue;

                _enemyRenderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(_colorPropertyName, _rendererBaseColors[i]);
                _enemyRenderers[i].SetPropertyBlock(_propertyBlock);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _shakeTween?.Kill();
            _scaleTween?.Kill();
            _flashTween?.Kill();
        }
    }
}
