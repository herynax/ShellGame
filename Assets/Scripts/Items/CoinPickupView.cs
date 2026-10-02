using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Физическая монета на столе в двух состояниях:
    /// уложена (кинематическая, лежит в стопке) и разлетевшаяся (динамическая —
    /// падает, отскакивает и катится).
    ///
    /// Legacy OnMouseEnter/OnMouseDown здесь намеренно НЕ используются: они требуют
    /// камеру с тегом MainCamera и в этом проекте срабатывают нестабильно
    /// (та же причина, по которой предметы переведены на внешний ввод — см.
    /// ItemPickupView). Весь ввод по монетам держит CoinPileController.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CoinPickupView : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Дочерний визуал. Двигаем и масштабируем только его — корень с коллайдером и Rigidbody не трогаем, иначе ховер уводит луч с монеты.")]
        [SerializeField] private Transform _visual;
        [SerializeField] private float _hoverHeight = 0.02f;
        [SerializeField] private float _hoverScale = 1.2f;
        [SerializeField] private float _hoverDuration = 0.15f;

        [Header("Stacking")]
        [SerializeField] private float _stackDistance = 0.03f; // Distance between coins in stack
        [SerializeField] private float _scatterDuration = 0.3f;

        [Header("Scatter Impulse")]
        [Tooltip("Скорость броска в сторону. 0.5 — сдержанно, 1.5 — сильно.")]
        [SerializeField, Range(0.1f, 3f)] private float _scatterSpeed = 0.9f;
        [Tooltip("Насколько монета подпрыгивает вверх при разбросе.")]
        [SerializeField, Range(0f, 1f)] private float _scatterUpSpeed = 0.35f;
        [Tooltip("Разброс вращения, чтобы монетка кувыркалась при падении.")]
        [SerializeField, Range(0f, 0.5f)] private float _scatterSpin = 0.06f;

        [Header("Sounds")]
        [SerializeField] private EventReference _pickupSound;
        [SerializeField] private EventReference _stackSound;
        [SerializeField] private EventReference _scatterSound;

        private bool _isHovered;
        private bool _isFlying;
        private CoinPileController _currentPile;
        private Tween _hoverTween;
        private IAudioService _audio;

        public Rigidbody Body { get; private set; }

        public CoinPileController CurrentPile
        {
            get => _currentPile;
            internal set => _currentPile = value;
        }
        public bool IsInPile => _currentPile != null;
        public bool IsFlying => _isFlying;
        public bool IsHovered => _isHovered;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }

            // По умолчанию монета уложена: физику включаем только на разбросе.
            Settle();
        }

        /// <summary>Уложить монету: кинематика, гравитация выключена, лежит на месте.</summary>
        public void Settle()
        {
            _isFlying = false;
            transform.DOKill();

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localPosition = Vector3.zero;
                _visual.localScale = Vector3.one;
            }

            if (Body == null) return;

            Body.isKinematic = true;
            Body.useGravity = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Разбросить монету: отлипает от стопки, становится динамической и
        /// улетает в случайную сторону — дальше падает по-настоящему.
        /// </summary>
        public void RequestScatter()
        {
            if (Body == null) return;

            if (_currentPile != null)
            {
                // Монета остаётся в общем списке кучи, но выходит из стопки.
                _currentPile.DetachFromStack(this);
                _currentPile = null;
            }

            Settle();

            _isFlying = true;
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;

            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

            float speed = _scatterSpeed * Random.Range(0.75f, 1.25f);
            Vector3 impulse = new Vector3(dir.x, 0f, dir.y) + Vector3.up * _scatterUpSpeed;

            // VelocityChange — скорость в м/с независимо от массы.
            Body.AddForce(impulse * speed, ForceMode.VelocityChange);
            Body.AddTorque(Random.insideUnitSphere * _scatterSpin, ForceMode.VelocityChange);

            if (!_scatterSound.IsNull)
                _audio?.PlayOneShot(_scatterSound, transform.position);
        }

        /// <summary>Ховер двигает только визуал — коллайдер и физика не шевелятся.</summary>
        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            if (_visual == null) return;

            _hoverTween?.Kill();
            _visual.DOKill();

            _hoverTween = _visual
                .DOScale(hovered ? _hoverScale : 1f, _hoverDuration)
                .SetEase(Ease.OutQuad);
            _visual.DOLocalMoveY(hovered ? _hoverHeight : 0f, _hoverDuration)
                .SetEase(Ease.OutQuad);

            if (!_pickupSound.IsNull)
                _audio?.PlayOneShot(_pickupSound, transform.position);
        }

        /// <summary>Анимированный перенос в заданную точку (сборка в стопку).</summary>
        public void MoveTo(Vector3 targetPosition, float duration, Ease ease)
        {
            Settle();

            transform.DOKill();
            transform.DOMove(targetPosition, duration).SetEase(ease).OnComplete(() =>
            {
                if (!_stackSound.IsNull)
                    _audio?.PlayOneShot(_stackSound, transform.position);
            });
        }

        public void StackAtPosition(Vector3 position, int indexInStack)
        {
            MoveTo(position + Vector3.up * (_stackDistance * indexInStack), 0.2f, Ease.OutBack);
        }

        private void OnDestroy()
        {
            _hoverTween?.Kill();
            transform.DOKill();
            if (_visual != null) _visual.DOKill();
        }
    }
}
