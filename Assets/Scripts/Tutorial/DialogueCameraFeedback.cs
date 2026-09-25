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

        /// <summary>
        /// Активный экземпляр. Компонент может лежать и в префабе окружения, и
        /// в сцене (в обучении он свой) — работает только один, чтобы FOV не
        /// двигали два твина одновременно.
        /// </summary>
        private static DialogueCameraFeedback _active;

        /// <summary>
        /// Показывает, идёт ли сейчас анимация зума или камера находится в зуме.
        /// </summary>
        public static bool IsZoomActive { get; private set; }

        private void Awake()
        {
            if (_impulseSource == null)
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            ResolveBrain();
        }

        private void ResolveBrain()
        {
            if (_brain != null)
                return;

            if (Camera.main != null)
                _brain = Camera.main.GetComponent<CinemachineBrain>();

            // Камера могла не иметь тега MainCamera на момент Awake.
            if (_brain == null)
                _brain = FindFirstObjectByType<CinemachineBrain>();
        }

        private void OnEnable()
        {
            if (_active != null && _active != this)
                return;

            _active = this;
            DialogueView.OnDialogueActive += HandleDialogue;
        }

        private void OnDisable() 
        {
            if (_active != this)
                return;

            _active = null;
            DialogueView.OnDialogueActive -= HandleDialogue;
            _fovTween?.Kill();
            _fovTween = null;
            _currentActiveCam = null;
            _originalFov = 0f;
            IsZoomActive = false;
        }

        private void HandleDialogue(bool isActive)
        {
            ResolveBrain();
            if (_brain == null) return;

            if (isActive)
            {
                IsZoomActive = true;
                _fovTween?.Kill();

                _currentActiveCam = _brain.ActiveVirtualCamera as CinemachineCamera;
                if (_currentActiveCam != null)
                {
                    // Если исходный FOV ещё не сохранен — сохраняем текущий
                    if (_originalFov <= 0f)
                        _originalFov = _currentActiveCam.Lens.FieldOfView;
                    
                    float targetFov = _originalFov - zoomAmount;

                    _fovTween = DOVirtual.Float(_currentActiveCam.Lens.FieldOfView, targetFov, zoomDuration, fov =>
                    {
                        if (_currentActiveCam == null) return;
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
                // Реплика закончилась — плавно возвращаем FOV в дефолтное состояние
                if (_currentActiveCam != null && _originalFov > 0f)
                {
                    _fovTween?.Kill();
                    var camToRestore = _currentActiveCam;
                    
                    _fovTween = DOVirtual.Float(camToRestore.Lens.FieldOfView, _originalFov, zoomDuration, fov =>
                    {
                        if (camToRestore == null) return;
                        var lens = camToRestore.Lens;
                        lens.FieldOfView = fov;
                        camToRestore.Lens = lens;
                    })
                    .OnComplete(() => 
                    {
                        IsZoomActive = false; // Камера полностью вернулась в норму
                        _originalFov = 0f;    // Сбрасываем для следующего замера
                    });
                }
                else
                {
                    IsZoomActive = false;
                    _originalFov = 0f;
                    _currentActiveCam = null;
                }
            }
        }
    }
}