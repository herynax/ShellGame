using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

namespace ShellGame.Tutorial
{
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class DialogueCameraFeedback : MonoBehaviour
    {
        [Header("Зум")]
        [Tooltip("На сколько уменьшать FOV во время реплики")]
        public float zoomAmount = 3f;
        public float zoomDuration = 0.3f;

        private CinemachineImpulseSource _impulseSource;
        private CinemachineBrain _brain;
        
        private CinemachineCamera _currentActiveCam;
        private float _originalFov;
        private Tween _fovTween;

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (Camera.main != null)
                _brain = Camera.main.GetComponent<CinemachineBrain>();
        }

        private void OnEnable() => DialogueView.OnDialogueActive += HandleDialogue;
        
        private void OnDisable() 
        {
            DialogueView.OnDialogueActive -= HandleDialogue;
            _fovTween?.Kill(); // Убиваем твин при выключении, чтобы избежать утечек
        }

        private void HandleDialogue(bool isActive)
        {
            if (_brain == null) return;

            if (isActive)
            {
                _fovTween?.Kill(); // Останавливаем прошлый отъезд/наезд, если он ещё идёт

                _currentActiveCam = _brain.ActiveVirtualCamera as CinemachineCamera;
                if (_currentActiveCam != null)
                {
                    _originalFov = _currentActiveCam.Lens.FieldOfView;
                    
                    // Плавно зумим
                    _fovTween = DOVirtual.Float(_originalFov, _originalFov - zoomAmount, zoomDuration, fov =>
                    {
                        if (_currentActiveCam == null) return; // Защита от NullReference
                        var lens = _currentActiveCam.Lens;
                        lens.FieldOfView = fov;
                        _currentActiveCam.Lens = lens;
                    });
                }

                if (_impulseSource != null)
                    _impulseSource.GenerateImpulse();
            }
            else
            {
                // Возвращаем FOV обратно
                if (_currentActiveCam != null)
                {
                    _fovTween?.Kill();
                    
                    // Запоминаем камеру локально, чтобы твин работал с ней, 
                    // даже если фокус сменится во время анимации.
                    var camToRestore = _currentActiveCam;
                    
                    _fovTween = DOVirtual.Float(camToRestore.Lens.FieldOfView, _originalFov, zoomDuration, fov =>
                    {
                        if (camToRestore == null) return; // Защита от NullReference
                        var lens = camToRestore.Lens;
                        lens.FieldOfView = fov;
                        camToRestore.Lens = lens;
                    })
                    .OnComplete(() => 
                    {
                        // Очищаем ссылку только когда отъезд полностью закончен
                        if (_currentActiveCam == camToRestore) 
                            _currentActiveCam = null;
                    });
                }
            }
        }
    }
}