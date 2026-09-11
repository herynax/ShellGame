using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace ShellGame.Meta
{
    public class FovController : MonoBehaviour, ISettingsModule
    {
        public static FovController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private Slider fovSlider;

        [Header("Диапазон FOV")]
        [SerializeField] private float minFov = 60f;
        [SerializeField] private float maxFov = 100f;

        private const string FOV_KEY = "CameraFOV";
        private const float DEFAULT_FOV = 75f;

        private float _currentFov;
        private float _snapshotFov;
        private readonly List<CinemachineCamera> _registeredCameras = new List<CinemachineCamera>();

        public float CurrentFov => _currentFov;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            LoadAndApply();
            SettingsSaveController.Instance?.RegisterModule(this);
        }

        private void OnEnable()
        {
            if (fovSlider != null)
            {
                fovSlider.minValue = minFov;
                fovSlider.maxValue = maxFov;
                fovSlider.SetValueWithoutNotify(_currentFov);
                fovSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        private void OnDisable()
        {
            if (fovSlider != null)
                fovSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        private void OnSliderChanged(float val)
        {
            SetFov(val);
            SettingsSaveController.Instance?.MarkDirty();
        }

        public void LoadAndApply()
        {
            _currentFov = PlayerPrefs.GetFloat(FOV_KEY, DEFAULT_FOV);
            ApplyToAllRegistered();
            if (fovSlider != null)
                fovSlider.SetValueWithoutNotify(_currentFov);
        }

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

        // --- ISettingsModule ---
        public void CaptureSnapshot() => _snapshotFov = _currentFov;
        public void Save() => PlayerPrefs.SetFloat(FOV_KEY, _currentFov);
        public void Revert()
        {
            _currentFov = _snapshotFov;
            ApplyToAllRegistered();
            if (fovSlider != null)
                fovSlider.SetValueWithoutNotify(_currentFov);
        }
    }
}