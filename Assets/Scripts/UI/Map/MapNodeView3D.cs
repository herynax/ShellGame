using System;
using UnityEngine;

namespace ShellGame.Map.Presentation
{
    [RequireComponent(typeof(Renderer))]
    public sealed class MapNodeView3D : MonoBehaviour
    {
        public int NodeId { get; private set; }

        private Action<int> _onClicked;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        public void Initialize(int nodeId, Action<int> onClicked)
        {
            NodeId = nodeId;
            _onClicked = onClicked;
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        public void SetColor(Color color)
        {
            // URP Lit использует _BaseColor, Built-in/Standard — _Color.
            // Ставим оба на всякий случай, лишний ключ шейдер просто проигнорирует.
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnMouseDown()
        {
            _onClicked?.Invoke(NodeId);
        }
    }
}