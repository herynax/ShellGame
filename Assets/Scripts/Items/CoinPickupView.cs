using System;
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Физическая монета на столе. Может быть подобрана, перетаскивается, стекается в кучи.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CoinPickupView : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform _visual;
        [SerializeField] private float _hoverHeight = 0.05f;
        [SerializeField] private float _hoverScale = 1.2f;
        [SerializeField] private float _hoverDuration = 0.15f;

        [Header("Stacking")]
        [SerializeField] private float _stackDistance = 0.03f; // Distance between coins in stack
        [SerializeField] private float _scatterDuration = 0.3f;

        [Header("Sounds")]
        [SerializeField] private EventReference _pickupSound;
        [SerializeField] private EventReference _stackSound;
        [SerializeField] private EventReference _scatterSound;

        private bool _isHovered;
        private bool _isDragging;
        private Vector3 _dragOffset;
        private CoinPileController _currentPile;
        private Tween _hoverTween;
        private IAudioService _audio;

        public CoinPileController CurrentPile 
        { 
            get => _currentPile; 
            internal set => _currentPile = value; 
        }
        public bool IsInPile => _currentPile != null;

        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        public event Action<CoinPickupView> OnDoubleClicked;

        private void Awake()
        {
            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }
        }

        private void OnMouseEnter()
        {
            if (_isDragging) return;
            _isHovered = true;
            PlayHoverEnter();
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            PlayHoverExit();
        }

        private void OnMouseDown()
        {
            if (_isHovered)
            {
                float timeSinceLastClick = Time.time - _lastClickTime;
                _lastClickTime = Time.time;

                if (timeSinceLastClick < DoubleClickThreshold)
                {
                    OnDoubleClicked?.Invoke(this);
                }
                else
                {
                    StartDrag();
                }
            }
        }

        private void OnMouseUp()
        {
            if (_isDragging)
            {
                EndDrag();
            }
        }

        private void StartDrag()
        {
            _isDragging = true;
            _dragOffset = transform.position - GetMouseWorldPosition();
            
            // Lift up
            transform.DOKill();
            transform.DOMoveY(transform.position.y + _hoverHeight, _hoverDuration).SetEase(Ease.OutQuad);
            
            // Remove from pile if in one
            if (_currentPile != null)
            {
                _currentPile.RemoveCoin(this);
                _currentPile = null;
            }
        }

        private void EndDrag()
        {
            _isDragging = false;
            
            // Check if over a pile
            var pile = FindPileUnderMouse();
            if (pile != null)
            {
                pile.TryAddCoin(this);
            }
            else
            {
                // Drop back to table
                transform.DOMoveY(transform.position.y - _hoverHeight, _hoverDuration).SetEase(Ease.InQuad);
            }
        }

        private void Update()
        {
            if (_isDragging)
            {
                Vector3 targetPos = GetMouseWorldPosition() + _dragOffset;
                targetPos.y = transform.position.y; // Keep height
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 15f);
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, LayerMask.GetMask("Table", "Default")))
            {
                return hit.point;
            }
            return transform.position;
        }

        private CoinPileController FindPileUnderMouse()
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.GetComponentInParent<CoinPileController>();
            }
            return null;
        }

        private void PlayHoverEnter()
        {
            _hoverTween?.Kill();
            _hoverTween = transform.DOMoveY(transform.position.y + _hoverHeight, _hoverDuration).SetEase(Ease.OutQuad);
            if (_visual != null)
                _visual.DOScale(_hoverScale, _hoverDuration).SetEase(Ease.OutQuad);
            
            if (!_pickupSound.IsNull)
                _audio?.PlayOneShot(_pickupSound, transform.position);
        }

        private void PlayHoverExit()
        {
            _hoverTween?.Kill();
            _hoverTween = transform.DOMoveY(transform.position.y - _hoverHeight, _hoverDuration).SetEase(Ease.InQuad);
            if (_visual != null)
                _visual.DOScale(1f, _hoverDuration).SetEase(Ease.InQuad);
        }

        public void AddToPile(CoinPileController pile, Vector3 targetPosition, bool animate = true)
        {
            _currentPile = pile;
            
            if (animate)
            {
                transform.DOKill();
                transform.DOMove(targetPosition, 0.3f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (!_stackSound.IsNull)
                        _audio?.PlayOneShot(_stackSound, transform.position);
                });
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        public void Scatter(Vector3 center, float radius)
        {
            if (_currentPile != null)
            {
                _currentPile.RemoveCoin(this);
                _currentPile = null;
            }

            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * radius;
            randomOffset.y = 0;
            Vector3 targetPos = center + randomOffset;
            targetPos.y = transform.position.y;

            transform.DOKill();
            transform.DOMove(targetPos, _scatterDuration).SetEase(Ease.OutQuad);
            
            if (!_scatterSound.IsNull)
                _audio?.PlayOneShot(_scatterSound, transform.position);
        }

        public void StackAtPosition(Vector3 position, int indexInStack)
        {
            Vector3 targetPos = position + Vector3.up * (_stackDistance * indexInStack);
            transform.DOKill();
            transform.DOMove(targetPos, 0.2f).SetEase(Ease.OutBack);
            
            if (!_stackSound.IsNull)
                _audio?.PlayOneShot(_stackSound, transform.position);
        }

        private void OnDestroy()
        {
            _hoverTween?.Kill();
            transform.DOKill();
        }
    }
}