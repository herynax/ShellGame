using FMOD.Studio;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Pooling;
using ShellGame.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShellGame.Shells
{
    /// <summary>
    /// Наперсток как 3D-объект стола. Сам объект — меш (не спрайт), но может
    /// нести дочерние спрайт-биллборды (например, декор/иконку метки при
    /// показе) — отсюда 2.5D-подход, упомянутый в дизайн-документе.
    ///
    /// Единственный источник правды по состоянию конкретного наперстка.
    /// Вся логика раунда (кто выиграл, сколько меток и т.д.) живёт выше —
    /// в ShellsTableController — этот класс сознательно "глупый".
    /// </summary>
    [RequireComponent(typeof(ShellAnimator))]
    [RequireComponent(typeof(Collider))]
    public sealed class Shell : MonoBehaviour, IPoolResettable
    {
        [SerializeField] private Collider _clickCollider;
        [SerializeField] private Transform _markerVisualAnchor; // сюда включается спрайт метки при Reveal, если нужно

        private ShellAnimator _animator;
        private ShellConfig _config;
        private IAudioService _audio;
        private Marker _marker;
        private EventInstance _revealStartInstance;

        public int SlotIndex { get; private set; } = -1;
        public ShellSlot AssignedSlot { get; private set; }
        public bool HasMarker { get; private set; }
        public ShellState State { get; private set; } = ShellState.PooledInactive;

        private void Awake()
        {
            _animator = GetComponent<ShellAnimator>();
            if (_clickCollider == null)
                _clickCollider = GetComponent<Collider>();
        }

        /// <summary>Вызывается пул-сервисом сразу после создания/раздачи слота — задаёт зависимости и правило один раз.</summary>
        public void Initialize(ShellConfig config, IAudioService audioService)
        {
            _config = config;
            _audio = audioService;
            _animator.Initialize(config);
        }

        public void AssignToSlot(int slotIndex)
        {
            SlotIndex = slotIndex;
        }

        public void AssignToSlot(ShellSlot slot)
        {
            AssignedSlot = slot;
            SlotIndex = slot != null ? slot.Index : -1;
        }

        public void PlaceAtSurface(Vector3 surfacePoint)
        {
            transform.position = surfacePoint;
        }

        public void AttachMarker(Marker marker)
        {
            _marker = marker;
            if (marker == null)
                return;

            marker.transform.SetParent(null, true);
            marker.Hide();
        }

        public void SetMarker(bool hasMarker)
        {
            HasMarker = hasMarker;
            if (_marker != null)
                _marker.Hide();
        }

        public void RevealMarker(float holdDuration)
        {
            State = ShellState.Revealing;
            ApplySpawnSurfacePosition();
            ShowMarkerVisual();

            // Звук лифта — сразу, синхронно со стартом анимации подъёма.
            PlayRevealStartSound();

            _animator.PlayReveal(
                holdDuration,
                onPeakReached: () =>
                {
                    // Лифт долетел до верха — глушим стартовый звук с фейд-аутом ровно здесь.
                    StopRevealStartSound();
                },
                onDescendingStarted: () => { },
                onComplete: () =>
                {
                    HideMarkerVisual();
                    State = ShellState.Idle;
                    PlayRevealEndSound();
                });
        }

        public void HideMarker()
        {
            HideMarkerVisual();
        }

        public void SetInteractable(bool interactable)
        {
            _clickCollider.enabled = interactable;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!enabled)
                return;

            var label = HasMarker ? "Marker" : "No marker";
            var color = HasMarker ? Color.yellow : Color.gray;
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.22f, 0.03f);
            Handles.Label(transform.position + Vector3.up * 0.28f, label);
#endif
        }

        public void OnHoverEnter()
        {
            if (State != ShellState.Idle) return;
            _animator.PlayHover(true);
            _audio?.PlayOneShot(_config.AudioEvents.Hover, transform.position);
            GameEvents.RaiseShellHoverEnter(this);
        }

        public void OnHoverExit()
        {
            if (State != ShellState.Idle) return;
            _animator.PlayHover(false);
            GameEvents.RaiseShellHoverExit(this);
        }

        /// <summary>Вызывается контроллером стола по клику ЛКМ на этот наперсток.</summary>
        public void Select()
        {
            if (State != ShellState.Idle) return;

            State = ShellState.Selected;
            SetInteractable(false);
            _audio?.PlayOneShot(_config.AudioEvents.Select, transform.position);
            GameEvents.RaiseShellSelected(this);

            ShowMarkerVisual();
            PlayRevealStartSound();

            _animator.PlayReveal(
                onPeakReached: () =>
                {
                    StopRevealStartSound();

                    var revealClip = HasMarker ? _config.AudioEvents.RevealMarked : _config.AudioEvents.RevealEmpty;
                    _audio?.PlayOneShot(revealClip, transform.position);
                    GameEvents.RaiseShellRevealed(this, HasMarker);
                },
                onDescendingStarted: () => { },
                onComplete: () =>
                {
                    HideMarkerVisual();
                    PlayRevealEndSound();
                });
        }

        /// <summary>Переместить наперсток на новую позицию слота (используется алгоритмом перемешивания).</summary>
        public void MoveToSlot(ShellSlot targetSlot, System.Action onComplete = null)
        {
            if (targetSlot == null)
            {
                onComplete?.Invoke();
                return;
            }

            State = ShellState.Shuffling;
            if (AssignedSlot != null)
                AssignedSlot.OccupyingShell = null;

            AssignedSlot = targetSlot;
            targetSlot.OccupyingShell = this;
            SlotIndex = targetSlot.Index;
            _audio?.PlayOneShot(_config.AudioEvents.ShuffleMove, transform.position);
            _animator.PlayMoveTo(targetSlot.Position, () =>
            {
                State = ShellState.Idle;
                onComplete?.Invoke();
            });
        }

        public void PlaySpawnIn()
        {
            gameObject.SetActive(true);
            _animator.PlaySpawnIn();
        }

        public void RevealResult()
        {
            if (State != ShellState.Selected)
                return;

            State = ShellState.Revealing;
            ShowMarkerVisual();
            PlayRevealStartSound();

            _animator.PlayReveal(
                onPeakReached: () => StopRevealStartSound(),
                onDescendingStarted: () => { },
                onComplete: () =>
                {
                    HideMarkerVisual();
                    State = ShellState.Idle;
                    PlayRevealEndSound();
                });
        }

        private void PlayRevealStartSound()
        {
            StopRevealStartSound(); // на случай повторного вызова без завершения предыдущего инстанса

            if (_audio == null)
                return;

            _revealStartInstance = _audio.CreateInstance(_config.AudioEvents.RevealStart);
            _revealStartInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
            _revealStartInstance.start();
        }

        private void StopRevealStartSound()
        {
            if (!_revealStartInstance.isValid())
                return;

            _revealStartInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _revealStartInstance.release();
            _revealStartInstance.clearHandle();
        }

        private void PlayRevealEndSound()
        {
            _audio?.PlayOneShot(_config.AudioEvents.RevealEnd, transform.position);
        }

        private void ApplySpawnSurfacePosition()
        {
            if (AssignedSlot != null)
            {
                PlaceAtSurface(AssignedSlot.SpawnPosition);
            }
        }

        private void ShowMarkerVisual()
        {
            if (!HasMarker)
                return;

            var surfacePosition = AssignedSlot != null ? AssignedSlot.SpawnPosition : transform.position;

            if (_marker != null)
            {
                _marker.PlaceAtSurface(surfacePosition);
                _marker.Show();
            }
            else if (_markerVisualAnchor != null)
            {
                _markerVisualAnchor.position = surfacePosition;
                _markerVisualAnchor.gameObject.SetActive(true);
            }
        }

        private void HideMarkerVisual()
        {
            if (!HasMarker)
                return;

            if (_marker != null)
                _marker.Hide();
            else if (_markerVisualAnchor != null)
                _markerVisualAnchor.gameObject.SetActive(false);
        }

        // --- IPoolResettable ---

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            State = ShellState.Idle;
            HasMarker = false;
            SlotIndex = -1;
            AssignedSlot = null;
            _marker = null;
            SetInteractable(true);
            if (_markerVisualAnchor != null)
                _markerVisualAnchor.gameObject.SetActive(false);

            if (_animator != null)
            {
                var baseScale = _animator.BaseScale;
                if (baseScale == Vector3.zero)
                    baseScale = Vector3.one;
                transform.localScale = baseScale;
            }
        }

        public void OnReturnToPool()
        {
            _animator.Kill();
            StopRevealStartSound();
            State = ShellState.PooledInactive;
            SetInteractable(false);
            if (_marker != null)
            {
                _marker.Hide();
                _marker = null;
            }
        }
    }
}