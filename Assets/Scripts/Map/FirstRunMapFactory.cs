// File: Assets/Scripts/Map/FirstRunMapFactory.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    // Карта первого рана. Два режима:
    //  - Build: полностью захардкоженная линейная карта (используется в
    //    debug-харнессах — MapDebugHarness/MapHarness3D).
    //  - BuildFirstRun: скриптованный префикс (Рыба, Гнев) + дальше —
    //    нормальная процедурная генерация через MapGenerator, склеенная с
    //    префиксом так, что игрок не видит шва между ними.
    public static class FirstRunMapFactory
    {
        private const int ConfigVersion = 1;
        private const int FirstRunSeed = 0; // условный маркер "не процедурный seed" — только для Build()
        private const int GeneratedIdOffset = 100000; // чтобы Id узлов префикса и сгенерированного хвоста не пересекались

        public static MapData Build(string[] forcedEncounterPattern, bool includeBoss = true)
        {
            if (forcedEncounterPattern == null || forcedEncounterPattern.Length == 0)
                throw new ArgumentException("Нужен хотя бы один энкаунтер в паттерне первого рана");

            int layerCount = forcedEncounterPattern.Length + 1 + (includeBoss ? 1 : 0); // + Start [+ Boss]
            var layers = new MapLayer[layerCount];
            var nodes = new MapNode[layerCount];

            layers[0] = new MapLayer(0, new[] { 0 });
            nodes[0] = new MapNode(0, 0, MapNodeType.Start);

            for (int i = 0; i < forcedEncounterPattern.Length; i++)
            {
                int layerIndex = i + 1;
                int nodeId = layerIndex * 100;

                layers[layerIndex] = new MapLayer(layerIndex, new[] { nodeId });
                nodes[layerIndex] = new MapNode(nodeId, layerIndex, MapNodeType.Enemy)
                {
                    ForcedEncounterId = forcedEncounterPattern[i]
                };
            }

            if (includeBoss)
            {
                int bossLayerIndex = layerCount - 1;
                int bossId = bossLayerIndex * 100;
                layers[bossLayerIndex] = new MapLayer(bossLayerIndex, new[] { bossId });
                nodes[bossLayerIndex] = new MapNode(bossId, bossLayerIndex, MapNodeType.Boss)
                {
                    ForcedEncounterId = "Fish"
                };
            }

            LinkLinear(nodes);

            return new MapData(FirstRunSeed, ConfigVersion, layers, nodes);
        }

        /// <summary>
        /// Реальная карта первого рана: forcedPrefix (например ["Fish", "Wrath"])
        /// как жёсткая линейная последовательность, а дальше — обычная
        /// MapGenerator.Generate(seed, config), пришитая встык. Детерминирована
        /// по seed (хвост), сам префикс всегда одинаковый.
        /// </summary>
        public static MapData BuildFirstRun(string[] forcedPrefix, int seed, MapGenerationConfig config)
        {
            if (forcedPrefix == null || forcedPrefix.Length == 0)
                throw new ArgumentException("Нужен хотя бы один энкаунтер в forcedPrefix");

            var fixedLayers = new List<MapLayer>();
            var fixedNodes = new List<MapNode>();

            fixedLayers.Add(new MapLayer(0, new[] { 0 }));
            fixedNodes.Add(new MapNode(0, 0, MapNodeType.Start));

            for (int i = 0; i < forcedPrefix.Length; i++)
            {
                int layerIndex = i + 1;
                int nodeId = layerIndex * 100;
                fixedLayers.Add(new MapLayer(layerIndex, new[] { nodeId }));
                fixedNodes.Add(new MapNode(nodeId, layerIndex, MapNodeType.Enemy)
                {
                    ForcedEncounterId = forcedPrefix[i]
                });
            }

            LinkLinear(fixedNodes);

            var generated = MapGenerator.Generate(seed, config);
            int layerOffset = fixedLayers.Count; // сколько слоёв уже занято префиксом

            // Сшиваем встык: собственный Start сгенерированной карты (layer 0,
            // один узел) выбрасываем, а последний узел префикса напрямую
            // получает связи, которые вёл этот Start — граф остаётся тем же
            // самым, просто источник рёбер меняется с "чужого" Start на
            // последний узел префикса.
            var oldToNewId = new Dictionary<int, int>();
            var tailLayers = new List<MapLayer>();
            var tailNodes = new List<MapNode>();

            for (int oldLayerIndex = 1; oldLayerIndex < generated.Layers.Length; oldLayerIndex++)
            {
                var oldLayer = generated.Layers[oldLayerIndex];
                int newLayerIndex = layerOffset + (oldLayerIndex - 1);
                var newIds = new int[oldLayer.NodeIds.Length];

                for (int j = 0; j < oldLayer.NodeIds.Length; j++)
                {
                    int oldId = oldLayer.NodeIds[j];
                    int newId = GeneratedIdOffset + oldId;
                    oldToNewId[oldId] = newId;
                    newIds[j] = newId;
                }

                tailLayers.Add(new MapLayer(newLayerIndex, newIds));
            }

            foreach (var oldNode in generated.Nodes)
            {
                if (oldNode.LayerIndex == 0) continue; // это выброшенный Start сгенерированной карты

                var newNode = new MapNode(
                    oldToNewId[oldNode.Id],
                    layerOffset + (oldNode.LayerIndex - 1),
                    oldNode.Type)
                {
                    ForcedEncounterId = oldNode.ForcedEncounterId,
                    Connections = oldNode.Connections.Select(c => oldToNewId[c]).ToArray()
                };

                tailNodes.Add(newNode);
            }

            var generatedStart = generated.StartNode;
            fixedNodes[^1].Connections = generatedStart.Connections.Select(c => oldToNewId[c]).ToArray();

            var allLayers = fixedLayers.Concat(tailLayers).ToArray();
            var allNodes = fixedNodes.Concat(tailNodes).ToArray();

            return new MapData(seed, ConfigVersion, allLayers, allNodes);
        }

        private static void LinkLinear(IList<MapNode> nodes)
        {
            for (int i = 0; i < nodes.Count - 1; i++)
                nodes[i].Connections = new[] { nodes[i + 1].Id };
        }
    }
}