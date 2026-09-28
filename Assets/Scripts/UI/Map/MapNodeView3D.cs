using System;
using UnityEngine;
using DG.Tweening;
using ShellGame.Map;

namespace ShellGame.Map.Presentation
{
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(Collider))]
    public sealed class MapNodeView3D : MonoBehaviour
    {
        public int NodeId { get; private set; }
        public MapNodeType NodeType { get; private set; }
        public MapNodeViewState VisualState { get; private set; }

        [Header("Hover/Click Feedback")]
        [SerializeField] private float _hoverScale = 1.3f;
        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private float _clickScale = 0.85f;
        [SerializeField] private float _clickDuration = 0.08f;
        [SerializeField] private Color _hoverColor = new Color(1f, 1f, 0.6f, 1f);
        [SerializeField] private Color _clickColor = new Color(0.8f, 1f, 0.8f, 1f);

        private Action<int> _onClicked;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Collider _collider;
        private Vector3 _originalScale;
        private Color _baseColor;
        private bool _isHovered;
        private bool _isAvailable;
        private Tween _scaleTween;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        public void Initialize(int nodeId, MapNodeType nodeType, Action<int> onClicked)
        {
            NodeId = nodeId;
            NodeType = nodeType;
            _onClicked = onClicked;
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            _block = new MaterialPropertyBlock();
            _originalScale = transform.localScale;
        }

        // Backward compatibility
        public void Initialize(int nodeId, Action<int> onClicked)
        {
            Initialize(nodeId, MapNodeType.Enemy, onClicked);
        }

        public void SetColor(Color color)
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            if (_block == null)
                _block = new MaterialPropertyBlock();

            _baseColor = color;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        public void SetVisualState(MapNodeViewState state)
        {
            VisualState = state;
            _isAvailable = state == MapNodeViewState.Available;

            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            if (_block == null)
                _block = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_block);

            if (state == MapNodeViewState.Available)
            {
                _block.SetColor(EmissionColorId, new Color(1.0f, 0.85f, 0.2f, 1.0f));
                _block.SetFloat(EmissionStrengthId, 0.4f);
            }
            else if (state == MapNodeViewState.Current)
            {
                _block.SetColor(EmissionColorId, new Color(0.1f, 0.9f, 1.0f, 1.0f));
                _block.SetFloat(EmissionStrengthId, 0.6f);
            }
            else
            {
                _block.SetColor(EmissionColorId, Color.black);
                _block.SetFloat(EmissionStrengthId, 0f);
            }

            _renderer.SetPropertyBlock(_block);
        }

        private void OnMouseEnter()
        {
            if (!_isAvailable) return;
            _isHovered = true;

            // Scale up
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack);

            // Color highlight
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, _hoverColor);
            _block.SetColor(ColorId, _hoverColor);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnMouseExit()
        {
            if (!_isAvailable) return;
            _isHovered = false;

            // Scale back
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale, _hoverDuration).SetEase(Ease.OutQuad);

            // Restore base color
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, _baseColor);
            _block.SetColor(ColorId, _baseColor);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnMouseDown()
        {
            if (!_isAvailable) return;

            // Click punch
            _scaleTween?.Kill();
            var seq = DOTween.Sequence();
            seq.Append(transform.DOScale(_originalScale * _clickScale, _clickDuration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack));

            // Click flash
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, _clickColor);
            _block.SetColor(ColorId, _clickColor);
            _renderer.SetPropertyBlock(_block);

            DOVirtual.DelayedCall(_clickDuration + 0.05f, () =>
            {
                if (_isHovered)
                {
                    _renderer.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, _hoverColor);
                    _block.SetColor(ColorId, _hoverColor);
                    _renderer.SetPropertyBlock(_block);
                }
            });

            _onClicked?.Invoke(NodeId);
        }

        private void OnDestroy()
        {
            _scaleTween?.Kill();
        }
    }
}