using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using System.Collections;
using FMODUnity;
using UnityEngine;

namespace ShellGame.Feedback
{
    public sealed class EnemyDamageFeedback : DamageFeedbackBase
    {
        [Header("Анимация укола (Damage-триггер)")]
        [SerializeField] private float _damageAnimDuration = 0.21f;

        [Header("Реакция на урон (длится столько же, сколько звук стона)")]
        [SerializeField] private float _reactionDuration = 2.5f;
        [SerializeField, Tooltip("Сколько волн тряски/панча/вспышки будет за время реакции")]
        private int _painPulseCount = 5;

        [Header("Тряска модели")]
        [SerializeField] private Transform _enemyModelTransform;
        [SerializeField] private float _shakeStrength = 0.25f;
        [SerializeField, Tooltip("Кол-во микро-тряски за секунду — подбирай так, чтобы за _reactionDuration тряска не выглядела вялой")]
        private int _shakeVibratoPerSecond = 40;
        [SerializeField] private float _shakeRandomness = 90f;

        [Header("Punch-масштаб")]
        [SerializeField] private Vector3 _scalePunch = new Vector3(0.15f, 0.15f, 0.15f);
        [SerializeField] private float _scalePunchDuration = 0.35f;
        [SerializeField] private int _scalePunchVibrato = 8;

        [Header("Цветовая вспышка")]
        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _flashDuration = 0.15f;
        [SerializeField] private string _colorPropertyName = "_Color";

        [Header("Animator (Damage/Return)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _damageTriggerName = "Damage";
        [SerializeField] private string _returnAnimationName = "Return";
        [SerializeField] private float _returnAnimDuration = 0.6f;

        [Header("Звук урона")]
        [SerializeField] private EnemySoundConfig _soundConfig;

        private Vector3 _modelBasePosition;
        private Vector3 _modelBaseScale;
        private Renderer[] _enemyRenderers;
        private Color[] _rendererBaseColors;
        private bool[] _rendererHasColorProperty;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _shakeTween;
        private Tween _scaleTween;
        private Tween _flashTween;
        private Coroutine _animationCoroutine;
        private Coroutine _painPulseCoroutine;

        protected override TurnSide WatchedSide => TurnSide.Enemy;

        protected override void Awake()
        {
            base.Awake();

            if (_enemyModelTransform != null)
            {
                _modelBasePosition = _enemyModelTransform.localPosition;
                _modelBaseScale = _enemyModelTransform.localScale;

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
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(PlayDamageSequenceCoroutine());
        }

        private IEnumerator PlayDamageSequenceCoroutine()
        {
            // 1. Укол иглой
            if (_animator != null)
                _animator.SetTrigger(_damageTriggerName);

            yield return new WaitForSeconds(_damageAnimDuration);

            // 2. Инъекция сделана — звук укола + звук боли (долгий стон) стартуют параллельно
            PlayEnemyDamageSound();

            // 3. Долгая реакция: тряска на всю длительность + волны панч-скейла/вспышек
            ShakeModel(_reactionDuration);

            if (_painPulseCoroutine != null)
                StopCoroutine(_painPulseCoroutine);
            _painPulseCoroutine = StartCoroutine(PainPulseCoroutine());

            yield return new WaitForSeconds(_reactionDuration);

            // 4. Возврат в исходную позу
            if (_animator != null)
                _animator.SetTrigger(_returnAnimationName);

            yield return new WaitForSeconds(_returnAnimDuration);

            _animationCoroutine = null;
        }

        private IEnumerator PainPulseCoroutine()
        {
            if (_painPulseCount <= 0) yield break;

            float interval = _reactionDuration / _painPulseCount;

            for (int i = 0; i < _painPulseCount; i++)
            {
                PunchScale();
                FlashColor();
                yield return new WaitForSeconds(interval);
            }
        }

        private void PlayEnemyDamageSound()
        {
            if (_soundConfig != null)
            {
                RuntimeManager.PlayOneShot(_soundConfig.injectionSound, transform.position);
                RuntimeManager.PlayOneShot(_soundConfig.damageSound, transform.position);
            }
        }

        private void ShakeModel(float duration)
        {
            if (_enemyModelTransform == null) return;

            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill();
                _enemyModelTransform.localPosition = _modelBasePosition;
            }

            int vibrato = Mathf.Max(1, Mathf.RoundToInt(_shakeVibratoPerSecond * duration));

            _shakeTween = _enemyModelTransform.DOShakePosition(
                duration, _shakeStrength, vibrato, _shakeRandomness, fadeOut: true);
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

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            if (_painPulseCoroutine != null)
            {
                StopCoroutine(_painPulseCoroutine);
                _painPulseCoroutine = null;
            }
        }
    }
}