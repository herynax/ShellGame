using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Параметры кастомного PSX-постэффекта для Volume-стека: хроматическая
    /// аберрация + "плывущий" screen warp (синусоидальное искажение UV) +
    /// шум (доп. дрожание/цветовая смазка), включающийся только на поздних
    /// стадиях. Core URP не поставляет ничего из этого из коробки, поэтому
    /// нужна своя рендер-фича (ChromaticAberrationRendererFeature), которая
    /// читает эти значения и реально рисует эффект.
    /// </summary>
    [Serializable, VolumeComponentMenu("ShellGame/Chromatic Aberration (PSX)")]
    public sealed class ChromaticAberrationVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Хроматическая аберрация")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 3f);

        [Header("Screen Warp (плывущая картинка)")]
        [Tooltip("Амплитуда смещения UV. 0 = картинка полностью статична.")]
        public ClampedFloatParameter warpAmplitude = new ClampedFloatParameter(0f, 0f, 0.12f);
        [Tooltip("Частота волны по экрану.")]
        public MinFloatParameter warpFrequency = new MinFloatParameter(6f, 0f);
        [Tooltip("Скорость волны во времени.")]
        public MinFloatParameter warpSpeed = new MinFloatParameter(1.2f, 0f);

        [Header("Noise (доп. дрожание/цветовая смазка на поздних стадиях)")]
        [Tooltip("Амплитуда шумового смещения UV, отдельного для R/G/B. 0 = выключен.")]
        public ClampedFloatParameter noiseAmplitude = new ClampedFloatParameter(0f, 0f, 0.2f);
        [Tooltip("Частота шума по экрану.")]
        public MinFloatParameter noiseFrequency = new MinFloatParameter(8f, 0f);
        [Tooltip("Скорость шума во времени.")]
        public MinFloatParameter noiseSpeed = new MinFloatParameter(1f, 0f);

        public bool IsActive() => intensity.value > 0f || warpAmplitude.value > 0f || noiseAmplitude.value > 0f;
        public bool IsTileCompatible() => false;
    }
}