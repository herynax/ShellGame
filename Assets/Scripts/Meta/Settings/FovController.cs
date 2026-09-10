// START OF FILE FovController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace ShellGame.Meta
{
    /// <summary>
    /// FOV-слайдер для вкладки "Другое". Значение общее на всю игру и переживает смену сцен
    /// (как и Brightness/VSync), а вот сами камеры — сценовые, поэтому они САМИ регистрируются
    /// здесь через RegisterCamera() при своём Start/OnEnable (например, там же, где у вас уже
    /// резолвятся mainCamera/pauseCamera в PauseController).
    /// </summary>
    public class FovController : MonoBehaviour
    {
        public static FovController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private Slider fovSlider;

        [Header("Диапазон FOV")]
        [Tooltip("60-70 — привычный минимум для шутеров/хорроров от первого лица")]
        [SerializeField] private float minFov = 60f;
        [Tooltip("100-110 — комфортный максимум, дальше начинает искажать картинку")]
        [SerializeField] private float maxFov = 100f;

        private const string FOV_KEY = "CameraFOV";
        private const float DEFAULT_FOV = 75f;

        private float _currentFov;
        private readonly List<CinemachineCamera> _registeredCameras = new List<CinemachineCamera>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            if (fovSlider != null)
            {
                fovSlider.minValue = minFov;
                fovSlider.maxValue = maxFov;
                fovSlider.SetValueWithoutNotify(_currentFov > 0f ? _currentFov : LoadSavedFov());
                fovSlider.onValueChanged.AddListener(SetFov);
            }
        }

        private void OnDisable()
        {
            if (fovSlider != null)
                fovSlider.onValueChanged.RemoveListener(SetFov);
        }

        private void Start()
        {
            _currentFov = LoadSavedFov();
            ApplyToAllRegistered();
        }

        private float LoadSavedFov()
        {
            return PlayerPrefs.GetFloat(FOV_KEY, DEFAULT_FOV);
        }

        /// <summary>
        /// Вызывать из скрипта, который резолвит камеры сцены (там же, где у вас уже находятся
        /// mainCamera/pauseCamera в PauseController) — например:
        /// FovController.Instance?.RegisterCamera(mainCamera);
        /// Так каждая новая сцена сама подтягивает текущий сохранённый FOV.
        /// </summary>
        public void RegisterCamera(CinemachineCamera camera)
        {
            if (camera == null || _registeredCameras.Contains(camera)) return;

            _registeredCameras.Add(camera);
            ApplyToCamera(camera, _currentFov);
        }

        public void UnregisterCamera(CinemachineCamera camera)
        {
            _registeredCameras.Remove(camera);
        }

        public void SetFov(float value)
        {
            _currentFov = Mathf.Clamp(value, minFov, maxFov);
            ApplyToAllRegistered();
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(FOV_KEY, _currentFov);
            PlayerPrefs.Save();
        }

        private void ApplyToAllRegistered()
        {
            foreach (var cam in _registeredCameras)
                ApplyToCamera(cam, _currentFov);
        }

        private static void ApplyToCamera(CinemachineCamera camera, float fov)
        {
            if (camera == null) return;

            var lens = camera.Lens;
            lens.FieldOfView = fov;
            camera.Lens = lens;
        }
    }
}
// END OF FILE
