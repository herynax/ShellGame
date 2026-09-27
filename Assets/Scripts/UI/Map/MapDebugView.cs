using System;
using System.Text;
using UnityEngine;

namespace ShellGame.MapDebug
{
    // Временный отладочный вид карты через OnGUI. Не финальная презентация —
    // задача только в том, чтобы можно было руками пройти сгенерированную
    // карту и убедиться, что MapGenerator/MapStateResolver работают вместе
    // до того, как будет готов 3D-вид в духе Inscryption (UI.Map, шаг позже).
    public sealed class MapDebugView : MonoBehaviour
    {
        public event Action<int> NodeSelected;

        private ShellGame.Map.MapData _map;
        private ShellGame.Map.MapState _state;
        private Vector2 _scroll;

        public void Bind(ShellGame.Map.MapData map, ShellGame.Map.MapState state)
        {
            _map = map;
            _state = state;
        }

        private void OnGUI()
        {
            if (_map == null || _state == null)
            {
                GUILayout.Label("MapDebugView: карта не забинжена (Bind() не вызван)");
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(420));

            foreach (var layer in _map.Layers)
            {
                GUILayout.Label($"— Layer {layer.Index} —", GUI.skin.box);

                foreach (var nodeId in layer.NodeIds)
                {
                    var node = _map.GetNode(nodeId);
                    var viewState = ShellGame.Map.MapStateResolver.Resolve(_map, _state, nodeId);

                    DrawNodeRow(node, viewState);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawNodeRow(ShellGame.Map.MapNode node, ShellGame.Map.MapNodeViewState viewState)
        {
            GUILayout.BeginHorizontal();

            var label = BuildLabel(node, viewState);
            GUI.enabled = viewState == ShellGame.Map.MapNodeViewState.Available;

            if (GUILayout.Button(label, GUILayout.Width(380)))
                NodeSelected?.Invoke(node.Id);

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static string BuildLabel(ShellGame.Map.MapNode node, ShellGame.Map.MapNodeViewState viewState)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(viewState).Append(']').Append(' ');
            sb.Append(node.Id).Append(' ').Append(node.Type);

            if (!string.IsNullOrEmpty(node.ForcedEncounterId))
                sb.Append(" (").Append(node.ForcedEncounterId).Append(')');

            sb.Append("  → ").Append(string.Join(",", node.Connections));

            return sb.ToString();
        }
    }
}