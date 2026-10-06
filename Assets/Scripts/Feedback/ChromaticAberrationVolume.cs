using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Параметры кастомного PSX-постэффекта для Volume-стека: хроматическая
    /// аберрация. Core URP не поставляет ничего из этого из коробки, поэтому
    /// нужна своя рендер-фича (ChromaticAberrationRendererFeature), которая
    /// читает эти значения и реально рисует эффект.
    ///
    /// Раньше здесь же были screen warp и шум — они вынесены в отдельный
    /// WobbleVolume / WobbleRendererFeature (шейдер Hidden/ShellGame/Wobble),
    /// потому что «плавание» экрана не должно зависеть от хроматики.
    /// </summary>
    [Serializable, VolumeComponentMenu("ShellGame/Chromatic Aberration (PSX)")]
    public sealed class ChromaticAberrationVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Хроматическая аберрация")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 3f);

        public bool IsActive() => intensity.value > 0f;
        public bool IsTileCompatible() => false;
    }
}