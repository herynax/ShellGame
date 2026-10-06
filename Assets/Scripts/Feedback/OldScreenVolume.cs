using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Эффект старого экрана (CRT) для Volume-стека: кривизна трубки,
    /// скругление углов, виньетка, RGB-апертура, сканлайны, interlace,
    /// бегущая полоса, свечение фосфора и статический шум.
    ///
    /// Каждый кусок выключается своим нулём, так что профиль можно
    /// настроить под любой уровень «ретро»: хоть постоянная лёгкая аберрация
    /// старой консоли, хоть полноценная ЭЛТ.
    ///
    /// Core URP ничего из этого не умеет (кроме LensDistortion/Vignette),
    /// поэтому рисует всё OldScreenRendererFeature через шейдер
    /// Hidden/ShellGame/OldScreen.
    /// </summary>
    [Serializable, VolumeComponentMenu("ShellGame/Old Screen (CRT)")]
    public sealed class OldScreenVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Форма экрана")]
        [Tooltip("Положительное значение выгибает картинку наружу (barrel), отрицательное — вогнуто.")]
        public ClampedFloatParameter curvature = new ClampedFloatParameter(0f, -0.5f, 0.5f);
        [Tooltip("Скругление углов трубки.")]
        public ClampedFloatParameter cornerRounding = new ClampedFloatParameter(0f, 0f, 0.5f);
        [Tooltip("Затемнение по краям, с усилением в углах.")]
        public ClampedFloatParameter vignetteIntensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Сетка субпикселей")]
        [Tooltip("Сила RGB-апертуры (триад). 0 = выключена.")]
        public ClampedFloatParameter apertureIntensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Сколько триад помещается по ширине экрана. Больше — тоньше полоски.")]
        public MinFloatParameter apertureResolution = new MinFloatParameter(640f, 1f);

        [Header("Строки развёртки")]
        [Tooltip("Глубина чёрных промежутков между строками.")]
        public ClampedFloatParameter scanlineIntensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Сколько строк помещается по высоте. 0 = выключено.")]
        public MinFloatParameter scanlineCount = new MinFloatParameter(256f, 0f);

        [Header("Interlace")]
        [Tooltip("Насколько гасится строка, не совпадающая с фазой кадра.")]
        public ClampedFloatParameter interlaceIntensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Сколько раз в секунду меняется фаза. 0 = эффект выключен.")]
        public MinFloatParameter interlaceSpeed = new MinFloatParameter(0f, 0f);

        [Header("Бегущая полоса")]
        [Tooltip("Яркость бегущей полосы поверх картинки.")]
        public ClampedFloatParameter rollIntensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Скорость полосы вверх по экрану.")]
        public MinFloatParameter rollSpeed = new MinFloatParameter(0.08f, -2f);
        [Tooltip("Высота полосы (в долях высоты экрана).")]
        public ClampedFloatParameter rollWidth = new ClampedFloatParameter(0.04f, 0.001f, 0.5f);

        [Header("Свечение и шум")]
        [Tooltip("Размазывание ярких пикселей — свечение фосфора.")]
        public ClampedFloatParameter bleedIntensity = new ClampedFloatParameter(0f, 0f, 2f);
        [Tooltip("Сила статического шума.")]
        public ClampedFloatParameter noiseIntensity = new ClampedFloatParameter(0f, 0f, 0.3f);
        [Tooltip("Крупность шума. Больше — мельче точки.")]
        public MinFloatParameter noiseScale = new MinFloatParameter(512f, 1f);

        public bool IsActive() =>
            curvature.value != 0f ||
            cornerRounding.value > 0f ||
            vignetteIntensity.value > 0f ||
            apertureIntensity.value > 0f ||
            scanlineIntensity.value > 0f ||
            interlaceIntensity.value > 0f ||
            rollIntensity.value > 0f ||
            bleedIntensity.value > 0f ||
            noiseIntensity.value > 0f;

        public bool IsTileCompatible() => false;
    }
}