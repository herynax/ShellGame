using System.Collections.Generic;
using UnityEngine;
using ShellGame.Items;

namespace ShellGame.Run
{
    [CreateAssetMenu(fileName = "FirstEncounterConfig", menuName = "ShellGame/Run/First Encounter Config")]
    public class FirstEncounterConfig : ScriptableObject
    {
        [Header("Starting Items")]
        [Tooltip("Пул предметов, из которых игрок выбирает/получает на первом энкаунтере")]
        public List<ItemDefinition> StartingItemPool = new List<ItemDefinition>();

        [Tooltip("Сколько предметов игрок должен выбрать/получить")]
        public int ItemsToPick = 2;

        [Header("Selection Mode")]
        [Tooltip("Если true — предметы выдаются случайно без выбора игрока")]
        public bool UseRandomSelection = false;

        [Tooltip("Мин. предметов при случайной выдаче")]
        public int MinRandomItems = 2;

        [Tooltip("Макс. предметов при случайной выдаче")]
        public int MaxRandomItems = 2;

        [Header("UI")]
        [Tooltip("Префаб индикатора выбора (подсветка, счетчик выбранных)")]
        public GameObject SelectionIndicatorPrefab;
    }
}