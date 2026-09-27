using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    // Минимальное мутируемое состояние прохождения текущей карты.
    // Пять состояний узла (Hidden/Available/...) сюда НЕ пишутся — они
    // выводятся через MapStateResolver из CurrentNodeId + CompletedNodeIds.
    [Serializable]
    public sealed class MapState
    {
        public int CurrentNodeId;
        public List<int> CompletedNodeIds = new();

        public MapState(int startNodeId)
        {
            CurrentNodeId = startNodeId;
        }

        // Разрешён ли переход в узел targetId из текущего положения.
        public bool CanMoveTo(MapData map, int targetNodeId)
        {
            var current = map.GetNode(CurrentNodeId);
            return current.Connections.Contains(targetNodeId);
        }

        // Помечает текущий узел завершённым и переводит указатель на выбранный узел.
        // Валидацию (CanMoveTo) вызывающий код обязан сделать заранее — этот метод
        // не бросает исключение специально, чтобы вызывающая сторона (RunManager)
        // сама решала, что делать с недопустимым переходом.
        public void CompleteCurrentAndMoveTo(int nextNodeId)
        {
            if (!CompletedNodeIds.Contains(CurrentNodeId))
                CompletedNodeIds.Add(CurrentNodeId);

            CurrentNodeId = nextNodeId;
        }

        public bool IsCompleted(int nodeId) => CompletedNodeIds.Contains(nodeId);
    }
}