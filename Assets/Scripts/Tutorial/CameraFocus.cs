using System.Collections;
using Unity.Cinemachine;
using DG.Tweening;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Переключение активной камеры через приоритет виртуальной камеры
    /// Cinemachine — сам transform камеры мы не трогаем, CinemachineBrain
    /// плавно перебленживает переход между vcam-ами так, как настроено в
    /// самом Brain (Default Blend / Custom Blends). Дополнительно шаг может
    /// слегка зумить FOV этой камеры через DOTween.
    ///
    /// Написано под Cinemachine 3.x (CinemachineCamera, пакет
    /// com.unity.cinemachine 3.x, namespace Unity.Cinemachine).
    /// В этой версии Priority — не int, а структура PrioritySettings,
    /// а доступ к линзе идёт через публичное свойство Lens (без префикса m_).
    /// </summary>
    public sealed class CameraFocus : TutorialStep
    {
        private readonly CinemachineCamera _targetCamera;
        private readonly int _priority;
        private readonly float? _targetFov;
        private readonly float _duration;
        private readonly bool _waitForCompletion;
        private readonly Ease _ease;

        /// <param name="targetCamera">Виртуальная камера, которую нужно сделать активной.</param>
        /// <param name="priority">
        /// Приоритет, который выставляется камере, чтобы CinemachineBrain
        /// переключился на неё. Должен быть выше приоритета остальных
        /// vcam-ов в сцене (обычно у "дефолтной" камеры Priority = 10 —
        /// держите под фокусные камеры, например, 20).
        /// </param>
        /// <param name="targetFov">
        /// Целевой FOV для лёгкого зума. Если null — FOV не трогаем, меняем
        /// только приоритет (просто переключение на другую камеру без зума).
        /// </param>
        /// <param name="waitForCompletion">
        /// Ждать ли окончания твина FOV, прежде чем сценарий пойдёт дальше.
        /// На сам блендинг между камерами не влияет — его длительность
        /// настраивается отдельно, в CinemachineBrain.
        /// </param>
        public CameraFocus(CinemachineCamera targetCamera, int priority, float duration,
            float? targetFov = null, bool waitForCompletion = true, Ease ease = Ease.InOutQuad)
        {
            _targetCamera = targetCamera;
            _priority = priority;
            _targetFov = targetFov;
            _duration = duration;
            _waitForCompletion = waitForCompletion;
            _ease = ease;
        }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            if (_targetCamera == null)
                yield break;

            // Поднимаем приоритет — CinemachineBrain сам плавно перебленжит
            // на эту камеру. Ничего больше двигать руками не нужно.
            // В Cinemachine 3.x Priority — структура PrioritySettings,
            // Enabled нужно явно включить, иначе Value игнорируется.
            _targetCamera.Priority = new PrioritySettings { Enabled = true, Value = _priority };

            if (!_targetFov.HasValue)
                yield break;

            float startFov = _targetCamera.Lens.FieldOfView;
            float endFov = _targetFov.Value;

            bool fovDone = !_waitForCompletion;
            DOVirtual.Float(startFov, endFov, _duration, fov =>
                {
                    var lens = _targetCamera.Lens;
                    lens.FieldOfView = fov;
                    _targetCamera.Lens = lens;
                })
                .SetEase(_ease)
                .OnComplete(() => fovDone = true);

            while (!fovDone)
                yield return null;
        }
    }
}