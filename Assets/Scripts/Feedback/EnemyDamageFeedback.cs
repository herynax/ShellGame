using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using System.Collections;
using FMODUnity;
using Unity.Cinemachine;
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

        [Header("Кровь (спавнится как отдельный объект в точке укола, проигрывается сама)")]
        [Tooltip("Точка укола иглы на модели врага. Если не задано — берётся позиция модели/этого объекта")]
        [SerializeField] private Transform _injectionPoint;
        [Tooltip("Префаб с ParticleSystem (Play On Awake = true, Stop Action = Destroy), сам себя проигрывает и убирает")]
        [SerializeField] private GameObject _bloodSplashParticlesPrefab;

        [Header("Animator (Damage/Return)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _damageTriggerName = "Damage";
        [SerializeField] private string _returnAnimationName = "Return";
        [SerializeField] private float _returnAnimDuration = 0.6f;

        [Header("Смерть — доп. кровь и тряска камеры")]
        [Tooltip("Префаб с ParticleSystem (Play On Awake = true, Stop Action = Destroy)")]
        [SerializeField] private GameObject _deathBloodBurstParticlesPrefab;
        [SerializeField] private CinemachineImpulseSource _deathImpulseSource;
        [SerializeField] private float _deathImpulseForce = 1.5f;

        [Header("Смерть — конвульсии передоза (черновые твины)")]
        [Tooltip("Судороги перед тем как тело обмякнет — имитация передозировки")]
        [SerializeField] private float _convulsionDuration = 0.7f;
        [SerializeField] private float _convulsionStrength = 0.35f;
        [SerializeField] private int _convulsionVibrato = 35;
        [SerializeField] private Vector3 _convulsionScalePunch = new Vector3(0.1f, -0.1f, 0.1f);

        [Header("Смерть — обмякание и падение (черновые твины)")]
        [SerializeField] private Color _deathTintColor = new Color(0.35f, 0.35f, 0.35f);
        [SerializeField] private float _deathFallDuration = 0.5f;
        [Tooltip("Ось + угол заваливания модели набок (локальный поворот)")]
        [SerializeField] private Vector3 _deathFallRotation = new Vector3(0f, 0f, 85f);
        [SerializeField] private Ease _deathFallEase = Ease.InQuad;
        [SerializeField] private float _deathSinkDuration = 0.6f;
        [SerializeField] private float _deathSinkDistance = 0.35f;

        [Header("Смерть — PSX-растворение (идёт параллельно с оседанием)")]
        [Tooltip("Имя float-свойства в шейдере (0 = обычная модель, 1 = полностью растворена). Если у материала нет такого свойства — просто не сработает")]
        [SerializeField] private string _dissolvePropertyName = "_DissolveAmount";
        [SerializeField] private float _deathDissolveDuration = 1.2f;
        [Tooltip("Через сколько секунд после начала конвульсий стартует растворение")]
        [SerializeField] private float _dissolveStartDelay = 0.2f;

        [Header("Звук урона")]
        [SerializeField] private EnemySoundConfig _soundConfig;

        private Vector3 _modelBasePosition;
        private Vector3 _modelBaseScale;
        private Renderer[] _enemyRenderers;
        private Color[] _rendererBaseColors;
        private bool[] _rendererHasColorProperty;
        private bool[] _rendererHasDissolveProperty;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _shakeTween;
        private Tween _scaleTween;
        private Tween _flashTween;
        private Sequence _deathSequence;
        private Coroutine _animationCoroutine;
        private Coroutine _painPulseCoroutine;
        private Coroutine _dissolveCoroutine;

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
                _rendererHasDissolveProperty = new bool[_enemyRenderers.Length];

                for (int i = 0; i < _enemyRenderers.Length; i++)
                {
                    Material material = _enemyRenderers[i].sharedMaterial;

                    _rendererHasColorProperty[i] = material != null && material.HasProperty(_colorPropertyName);
                    _rendererBaseColors[i] = _rendererHasColorProperty[i]
                        ? material.GetColor(_colorPropertyName)
                        : Color.white;

                    _rendererHasDissolveProperty[i] = material != null && material.HasProperty(_dissolvePropertyName);
                }
            }
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(died
                ? PlayDeathSequenceCoroutine()
                : PlayDamageSequenceCoroutine());
        }

        private IEnumerator PlayDamageSequenceCoroutine()
        {
            // 1. Укол иглой
            if (_animator != null)
                _animator.SetTrigger(_damageTriggerName);

            yield return new WaitForSeconds(_damageAnimDuration);

            // 2. Инъекция сделана — звук + кровь + реакция стартуют параллельно
            PlayEnemyDamageSound();
            SpawnBloodSplash();
            ShakeModel(_reactionDuration);

            if (_painPulseCoroutine != null)
                StopCoroutine(_painPulseCoroutine);
            _painPulseCoroutine = StartCoroutine(PainPulseCoroutine());

            yield return new WaitForSeconds(_reactionDuration);

            // 3. Возврат в исходную позу
            if (_animator != null)
                _animator.SetTrigger(_returnAnimationName);

            yield return new WaitForSeconds(_returnAnimDuration);

            _animationCoroutine = null;
        }

        private IEnumerator PlayDeathSequenceCoroutine()
        {
            // 1. Укол иглой — последняя, смертельная инъекция
            if (_animator != null)
                _animator.SetTrigger(_damageTriggerName);

            yield return new WaitForSeconds(_damageAnimDuration);

            // 2. Тот же болевой отклик, что и на обычном хите
            PlayEnemyDamageSound();
            SpawnBloodSplash();
            ShakeModel(_reactionDuration);

            if (_painPulseCoroutine != null)
                StopCoroutine(_painPulseCoroutine);
            _painPulseCoroutine = StartCoroutine(PainPulseCoroutine());

            yield return new WaitForSeconds(_reactionDuration);

            // 3. Передоз накрывает: судороги → тело обмякает и падает → растворяется
            SpawnDeathBloodBurst();
            ShakeCameraOnDeath();

            float totalDuration = PlayOverdoseDeathTweens();
            yield return new WaitForSeconds(totalDuration);

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

        /// <summary>
        /// Черновая анимация смерти от передозировки на DOTween: сначала резкие судороги
        /// (симулируют передоз/приступ), затем тело теряет опору и заваливается/оседает,
        /// одновременно с оседанием запускается PSX-растворение шейдером.
        /// Заменить на Animator-стейт, когда появится полноценный арт — тогда
        /// PlayDeathSequenceCoroutine просто вызовет _animator.SetTrigger(deathTriggerName).
        /// </summary>
        /// <returns>Полная длительность анимации смерти</returns>
        private float PlayOverdoseDeathTweens()
        {
            if (_enemyModelTransform == null) return 0f;

            _shakeTween?.Kill();
            _scaleTween?.Kill();
            _flashTween?.Kill();
            _deathSequence?.Kill();

            // Судороги: тряска чаще и резче обычной боли + рваные микро-панчи скейла
            Tween convulsionShake = _enemyModelTransform.DOShakePosition(
                _convulsionDuration, _convulsionStrength, _convulsionVibrato, 90f, fadeOut: true);

            Tween convulsionPunch = _enemyModelTransform.DOPunchScale(
                _convulsionScalePunch, _convulsionDuration, vibrato: 12, elasticity: 0.8f);

            // Обмякание: заваливается набок и оседает "в пол"
            Tween fallRotate = _enemyModelTransform
                .DOLocalRotate(_deathFallRotation, _deathFallDuration, RotateMode.LocalAxisAdd)
                .SetEase(_deathFallEase);

            Tween sinkDown = _enemyModelTransform
                .DOLocalMoveY(_modelBasePosition.y - _deathSinkDistance, _deathSinkDuration)
                .SetEase(Ease.InQuad);

            _deathSequence = DOTween.Sequence()
                .Append(convulsionShake)
                .Join(convulsionPunch)
                .AppendCallback(() => SetRenderersColor(_deathTintColor))
                .Append(fallRotate)
                .Join(sinkDown);

            // Растворение стартует чуть позже начала конвульсий, идёт параллельно с падением
            if (_dissolveCoroutine != null)
                StopCoroutine(_dissolveCoroutine);
            _dissolveCoroutine = StartCoroutine(DissolveCoroutine(_convulsionDuration + _dissolveStartDelay));

            float sinkPhaseStart = _convulsionDuration;
            float sinkPhaseDuration = Mathf.Max(_deathFallDuration, _deathSinkDuration);
            float dissolvePhaseEnd = _convulsionDuration + _dissolveStartDelay + _deathDissolveDuration;

            return Mathf.Max(sinkPhaseStart + sinkPhaseDuration, dissolvePhaseEnd);
        }

        private IEnumerator DissolveCoroutine(float startDelay)
        {
            if (_enemyRenderers == null) yield break;

            yield return new WaitForSeconds(startDelay);

            float elapsed = 0f;
            while (elapsed < _deathDissolveDuration)
            {
                SetRenderersDissolve(elapsed / _deathDissolveDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetRenderersDissolve(1f);
        }

        private void PlayEnemyDamageSound()
        {
            if (_soundConfig != null)
            {
                RuntimeManager.PlayOneShot(_soundConfig.injectionSound, transform.position);
                RuntimeManager.PlayOneShot(_soundConfig.damageSound, transform.position);
            }
        }

        private void SpawnBloodSplash()
        {
            SpawnParticles(_bloodSplashParticlesPrefab);
        }

        private void SpawnDeathBloodBurst()
        {
            SpawnParticles(_deathBloodBurstParticlesPrefab);
        }

        private void SpawnParticles(GameObject particlesPrefab)
        {
            if (particlesPrefab == null) return;

            Vector3 spawnPosition = _injectionPoint != null
                ? _injectionPoint.position
                : (_enemyModelTransform != null ? _enemyModelTransform.position : transform.position);

            // Партиклы сами себя проигрывают (Play On Awake) и сами себя убирают
            // (Stop Action = Destroy в модуле ParticleSystem на префабе) —
            // здесь только спавн, без ручного Play()/Stop()/Destroy().
            Instantiate(particlesPrefab, spawnPosition, particlesPrefab.transform.rotation);
        }

        private void ShakeCameraOnDeath()
        {
            if (_deathImpulseSource == null) return;

            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized;
            _deathImpulseSource.GenerateImpulse(randomDirection * _deathImpulseForce);
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

        private void SetRenderersDissolve(float amount)
        {
            for (int i = 0; i < _enemyRenderers.Length; i++)
            {
                if (!_rendererHasDissolveProperty[i]) continue;

                _enemyRenderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(_dissolvePropertyName, amount);
                _enemyRenderers[i].SetPropertyBlock(_propertyBlock);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _shakeTween?.Kill();
            _scaleTween?.Kill();
            _flashTween?.Kill();
            _deathSequence?.Kill();

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

            if (_dissolveCoroutine != null)
            {
                StopCoroutine(_dissolveCoroutine);
                _dissolveCoroutine = null;
            }
        }
    }
}