using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ShellGame.Map;

namespace ShellGame.Map.Presentation
{
    [RequireComponent(typeof(MapView3D))]
    public sealed class MapHarness3D : MonoBehaviour
    {
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _useFirstRunPattern = true;
        [SerializeField] private SinArchetype[] _firstRunEncounters = { SinArchetype.Wrath, SinArchetype.Envy };
        [SerializeField] private float _scrollSpeed = 1.2f;

        private MapView3D _view;
        private float _currentScroll;
        private float _targetScroll;
        private float _scrollVelocity;

        private void Start()
        {
            var config = new MapGenerationConfig();

            var map = _useFirstRunPattern
                ? FirstRunMapFactory.Build(_firstRunEncounters.Select(s => s.ToEncounterId()).ToArray())
                : MapGenerator.Generate(_seed, config);

            var state = new MapState(map.StartNode.Id);

            _view = GetComponent<MapView3D>();
            _view.Build(map, state);
            _view.NodeSelected += nodeId => UnityEngine.Debug.Log($"[MapHarness3D] Выбран узел {nodeId}");

            _targetScroll = _currentScroll = _view.CalculateScrollForNode(state.CurrentNodeId);
        }

        private void Update()
        {
            if (_view == null) return;

            float scrollDelta = 0f;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    scrollDelta -= wheel * 0.002f;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float keyInput = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    keyInput += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    keyInput -= 1f;

                if (Mathf.Abs(keyInput) > 0.01f)
                    scrollDelta -= keyInput * _scrollSpeed * Time.deltaTime;
            }

            var (minScroll, maxScroll) = _view.GetScrollBounds();
            _targetScroll = Mathf.Clamp(_targetScroll + scrollDelta, minScroll, maxScroll);
            _currentScroll = Mathf.SmoothDamp(_currentScroll, _targetScroll, ref _scrollVelocity, 0.08f);
            _view.SetScrollOffset(_currentScroll);
        }
    }
}
