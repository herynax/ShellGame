using ShellGame.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Психоделический эффект наркотика у игрока: чем больше текущая доза
    /// относительно порога передозировки, тем сильнее "плывёт" картинка.
    /// Ничего вызывать не нужно — сам подключается через GameEvents.HealthChanged.
    ///
    /// Управляет тремя вещами на одном Volume-профиле:
    ///  - Vignette (встроен в URP) — тёмная кайма экрана;
    ///  - ChromaticAberrationVolume.intensity — цветовые полосы по краям;
    ///  - ChromaticAberrationVolume.warp* — синусоидальное "плавание" экрана;
    ///  - ChromaticAberrationVolume.noise* — дополнительное дрожание/цветовая
    ///    смазка, которая включается только во второй половине дозы и резко
    ///    разгоняется к передозу.
    ///
    /// Виньетка и хроматика теперь растут не линейно, а по степенной кривой
    /// с "поздним рывком": до _lateKickStart эффект держится мягким, а после —
    /// дополнительно умножается вплоть до _lateKickMultiplier (по умолчанию x10)
    /// к моменту передозировки. Варп (плавание картинки) этой кривой не касается.
    /// </summary>
    public sealed class PsychedelicEffectController : MonoBehaviour
    {
        [SerializeField] private Volume _volume;

        [Header("Vignette (встроенный в URP)")]
        [SerializeField] private float _vignetteIntensityMin = 0.15f;
        [SerializeField] private float _vignetteIntensityMax = 0.55f;
        [SerializeField] private float _vignetteSmoothnessMin = 0.3f;
        [SerializeField] private float _vignetteSmoothnessMax = 0.6f;

        [Header("Chromatic Aberration")]
        [SerializeField] private float _chromaticAberrationMax = 2.6f;

        [Header("Screen Warp (плывущая картинка) — линейно, без поздней кривой")]
        [SerializeField] private float _warpAmplitudeMax = 0.06f;
        [SerializeField] private float _warpFrequencyMin = 4f;
        [SerializeField] private float _warpFrequencyMax = 10f;
        [SerializeField] private float _warpSpeedMin = 0.6f;
        [SerializeField] private float _warpSpeedMax = 2.5f;

        [Header("Поздняя кривая (для виньетки и хроматики)")]
        [Tooltip("Степень кривой fraction^power: чем больше, тем мягче начало")]
        [SerializeField] private float _curvePower = 3f;
        [Tooltip("Доля дозы, после которой включается дополнительный 'рывок'")]
        [Range(0f, 1f)]
        [SerializeField] private float _lateKickStart = 0.7f;
        [Tooltip("Во сколько раз усиливается эффект к моменту передозировки")]
        [SerializeField] private float _lateKickMultiplier = 10f;

        [Header("Шум поверх варпа (только вторая половина дозы)")]
        [Range(0f, 1f)]
        [SerializeField] private float _noiseThreshold = 0.5f;
        [SerializeField] private float _noiseAmplitudeMax = 0.05f;
        [SerializeField] private float _noiseFrequencyMin = 6f;
        [SerializeField] private float _noiseFrequencyMax = 18f;
        [SerializeField] private float _noiseSpeedMin = 1f;
        [SerializeField] private float _noiseSpeedMax = 4f;

        [Header("Опционально: довесок к амплитуде варпа от шумового прогресса")]
        [Tooltip("0 = варп не трогаем (как просили). >0 — добавляет дрожания на поздних стадиях сверху обычного плавания")]
        [SerializeField] private float _extraWarpBoostMax = 0.35f;

        [Header("Плавность отклика")]
        [Tooltip("Не дёргаем эффект резко на каждое событие урона/лечения — плавно подтягиваем к целевой доле дозы")]
        [SerializeField] private float _smoothTime = 0.4f;

        private Vignette _vignette;
        private ChromaticAberrationVolume _chromaticAberration;
        private float _targetFraction;
        private float _currentFraction;
        private float _velocity;
        private bool _isDead;

        private void Awake()
        {

            Debug.Log($"PsychedelicEffectController: Awake called on GameObject '{gameObject.name}', active={gameObject.activeInHierarchy}");

            if (_volume == null)
                _volume = FindObjectOfType<Volume>();

            if (_volume != null && _volume.profile != null)
            {
                _volume.profile.TryGet(out _vignette);
                _volume.profile.TryGet(out _chromaticAberration);
            }
        }

        private void OnEnable()
        {
            GameEvents.HealthChanged += OnHealthChanged;
            GameEvents.SideDied += OnSideDied;
            Debug.Log("PsychedelicEffectController: enabled and subscribed to GameEvents.");
        }

        private void OnDisable()
        {
            GameEvents.HealthChanged -= OnHealthChanged;
            GameEvents.SideDied -= OnSideDied;
            Debug.Log("PsychedelicEffectController: disabled and unsubscribed from GameEvents.");
        }

        private void OnHealthChanged(TurnSide side, int current, int max)
        {
            if (side != TurnSide.Player)
                return;

            _targetFraction = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            Debug.Log($"PsychedelicEffectController: OnHealthChanged player current={current} max={max} targetFraction={_targetFraction}");
        }

        private void OnSideDied(TurnSide side)
        {
            if (side != TurnSide.Player)
                return;

            _isDead = true;
            _currentFraction = 1f;
            _targetFraction = 1f;
            Debug.Log("PsychedelicEffectController: OnSideDied player — applying death blackout.");
        }

        private void Update()
        {
            if (_isDead)
            {
                ApplyDeathBlackout();
                return;
            }

            _currentFraction = Mathf.SmoothDamp(_currentFraction, _targetFraction, ref _velocity, _smoothTime);

            // Степенная кривая: мягкий старт.
            float curved = Mathf.Pow(_currentFraction, _curvePower);

            // Поздний рывок: 0 до _lateKickStart, дальше плавно уходит к 1
            // на моменте передозировки — и там curved домножается вплоть до x_lateKickMultiplier.
            float lateProgress = Mathf.Clamp01(Mathf.InverseLerp(_lateKickStart, 1f, _currentFraction));
            float lateKick = lateProgress * lateProgress * (3f - 2f * lateProgress); // smoothstep
            float aggressiveness = curved * Mathf.Lerp(1f, _lateKickMultiplier, lateKick);

            if (_vignette != null)
            {
                // LerpUnclamped специально: даём эффекту "перехлестнуть" максимум на пике,
                // URP сам зажмёт значение в валидный диапазон параметра.
                _vignette.intensity.value = Mathf.LerpUnclamped(_vignetteIntensityMin, _vignetteIntensityMax, aggressiveness);
                _vignette.smoothness.value = Mathf.LerpUnclamped(_vignetteSmoothnessMin, _vignetteSmoothnessMax, aggressiveness);
            }

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = aggressiveness * _chromaticAberrationMax;

                // Варп — линейно от текущей дозы, кривую не применяем (как просили).
                float warpBoost = 1f;
                float noiseRamp = Mathf.Clamp01(Mathf.InverseLerp(_noiseThreshold, 1f, _currentFraction));
                if (_extraWarpBoostMax > 0f)
                    warpBoost = 1f + noiseRamp * _extraWarpBoostMax;

                _chromaticAberration.warpAmplitude.value = _currentFraction * _warpAmplitudeMax * warpBoost;
                _chromaticAberration.warpFrequency.value = Mathf.Lerp(_warpFrequencyMin, _warpFrequencyMax, _currentFraction);
                _chromaticAberration.warpSpeed.value = Mathf.Lerp(_warpSpeedMin, _warpSpeedMax, _currentFraction);

                // Шум: строго 0 до половины дозы, затем разгон к максимуму.
                _chromaticAberration.noiseAmplitude.value = noiseRamp * _noiseAmplitudeMax;
                _chromaticAberration.noiseFrequency.value = Mathf.Lerp(_noiseFrequencyMin, _noiseFrequencyMax, noiseRamp);
                _chromaticAberration.noiseSpeed.value = Mathf.Lerp(_noiseSpeedMin, _noiseSpeedMax, noiseRamp);
            }
        }

        private void ApplyDeathBlackout()
        {
            if (_vignette != null)
            {
                _vignette.intensity.value = 1f;
                _vignette.smoothness.value = 0f;
            }

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = _chromaticAberrationMax;
                _chromaticAberration.warpAmplitude.value = _warpAmplitudeMax;
                _chromaticAberration.warpFrequency.value = _warpFrequencyMax;
                _chromaticAberration.warpSpeed.value = _warpSpeedMax;
                _chromaticAberration.noiseAmplitude.value = _noiseAmplitudeMax;
                _chromaticAberration.noiseFrequency.value = _noiseFrequencyMax;
                _chromaticAberration.noiseSpeed.value = _noiseSpeedMax;
            }
        }
    }
}