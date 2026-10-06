using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Параметры «плавания» экрана (screen warp) + шумового дрожания для
    /// Volume-стека. Раньше эта логика жила внутри ChromaticAberrationVolume и
    /// была смешана с хроматикой в одном шейдере, поэтому вынесена в отдельную
    /// рендер-фичу (WobbleRendererFeature) и отдельный шейдер
    /// Hidden/ShellGame/Wobble. Теперь эффект можно крутить независимо от
    /// хроматики и включать постоянно, а не только под наркотик.
    /// </summary>
    [Serializable, VolumeComponentMenu("ShellGame/Wobble (PSX)")]
    public sealed class WobbleVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Screen Warp (плывущая картинка)")]
        [Tooltip("Амплитуда смещения UV. 0 = картинка полностью статична.")]
        public ClampedFloatParameter warpAmplitude = new ClampedFloatParameter(0f, 0f, 0.12f);
        [Tooltip("Частота волны по экрану.")]
        public MinFloatParameter warpFrequency = new MinFloatParameter(6f, 0f);
        [Tooltip("Скорость волны во времени.")]
        public MinFloatParameter warpSpeed = new MinFloatParameter(1.2f, 0f);

        [Header("Noise (доп. дрожание на поздних стадиях)")]
        [Tooltip("Амплитуда шумового смещения UV. 0 = выключен.")]
        public ClampedFloatParameter noiseAmplitude = new ClampedFloatParameter(0f, 0f, 0.2f);
        [Tooltip("Частота шума по экрану.")]
        public MinFloatParameter noiseFrequency = new MinFloatParameter(8f, 0f);
        [Tooltip("Скорость шума во времени.")]
        public MinFloatParameter noiseSpeed = new MinFloatParameter(1f, 0f);

        public bool IsActive() => warpAmplitude.value > 0f || noiseAmplitude.value > 0f;
        public bool IsTileCompatible() => false;
    }
}