using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Items;
using ShellGame.Meta;
using ShellGame.Run;
using UnityEditor;
using UnityEngine;

namespace ShellGame.EditorTools
{
    public sealed class CheatPanelWindow : EditorWindow
    {
        private int _tab = 0;
        private static readonly string[] Tabs = { "Энкаунтеры", "Предметы", "Монеты" };

        // Encounters
        private List<EncounterDefinition> _encounters = new List<EncounterDefinition>();
        private string _encounterFilter = "";
        private Vector2 _encounterScroll;

        // Items
        private List<ItemDefinition> _items = new List<ItemDefinition>();
        private string _itemFilter = "";
        private Vector2 _itemScroll;
        private int _itemCount = 1;

        // Coins
        private int _coinAmount = 50;
        private bool _dropPhysicalCoins = true;

        [MenuItem("ShellGame/Cheat Panel")]
        private static void Open()
        {
            GetWindow<CheatPanelWindow>("Cheat Panel");
        }

        private void OnEnable()
        {
            RefreshItems();
            RefreshEncounters();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void RefreshItems()
        {
            _items = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .OrderBy(item => item.name)
                .ToList();
        }

        private void RefreshEncounters()
        {
            _encounters = AssetDatabase.FindAssets("t:EncounterDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(enc => enc != null)
                .OrderBy(enc => enc.Kind)
                .ThenBy(enc => string.IsNullOrEmpty(enc.Id) ? enc.name : enc.Id)
                .ToList();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Работает только в Play Mode.", MessageType.Info);
            }

            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space(8);

            switch (_tab)
            {
                case 0:
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        DrawEncounters();
                    }
                    break;

                case 1:
                    var inventoryItems = GetInventory();
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || inventoryItems == null))
                    {
                        DrawItems(inventoryItems);
                    }
                    break;

                case 2:
                    var inventoryCoins = GetInventory();
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || inventoryCoins == null))
                    {
                        DrawCoins(inventoryCoins);
                    }
                    break;
            }
        }

        #region Encounters

        private void DrawEncounters()
        {
            EditorGUILayout.LabelField("Энкаунтеры", EditorStyles.boldLabel);

            var runManager = RunManager.Instance;
            string currentId = runManager != null ? runManager.CurrentEncounterId : null;

            if (!string.IsNullOrEmpty(currentId))
            {
                EditorGUILayout.HelpBox($"Текущий активный энкаунтер: {currentId} ({runManager.CurrentEncounterKind})", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Нет активного энкаунтера (на карте или ран не запущен).", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _encounterFilter = EditorGUILayout.TextField("Поиск", _encounterFilter);
                if (GUILayout.Button("Обновить", GUILayout.Width(80)))
                    RefreshEncounters();
            }

            EditorGUILayout.Space(4);

            _encounterScroll = EditorGUILayout.BeginScrollView(_encounterScroll);

            foreach (var enc in _encounters)
            {
                if (enc == null) continue;

                if (!string.IsNullOrEmpty(_encounterFilter))
                {
                    bool matchId = !string.IsNullOrEmpty(enc.Id) && enc.Id.IndexOf(_encounterFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchName = enc.name.IndexOf(_encounterFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchKind = enc.Kind.ToString().IndexOf(_encounterFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchId && !matchName && !matchKind)
                        continue;
                }

                bool isCurrent = !string.IsNullOrEmpty(currentId) &&
                                 string.Equals(currentId, enc.Id, System.StringComparison.OrdinalIgnoreCase);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string displayName = string.IsNullOrEmpty(enc.Id) ? enc.name : $"{enc.Id} [{enc.name}]";
                    EditorGUILayout.LabelField($"[{enc.Kind}]", GUILayout.Width(75));
                    EditorGUILayout.LabelField(displayName);

                    if (enc.RigPrefab == null)
                    {
                        GUI.color = Color.yellow;
                        GUILayout.Label("Нет RigPrefab", EditorStyles.miniLabel, GUILayout.Width(90));
                        GUI.color = Color.white;
                    }
                    else if (isCurrent)
                    {
                        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
                        if (GUILayout.Button("Перезапуск", GUILayout.Width(90)))
                        {
                            LoadEncounter(enc);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        if (GUILayout.Button("Загрузить", GUILayout.Width(90)))
                        {
                            LoadEncounter(enc);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void LoadEncounter(EncounterDefinition def)
        {
            if (def == null || def.RigPrefab == null)
            {
                Debug.LogError($"[CheatPanel] У энкаунтера '{def?.name}' не назначен RigPrefab!");
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("[CheatPanel] Загрузка энкаунтера работает только в Play Mode.");
                return;
            }

            var host = Object.FindFirstObjectByType<EncounterHost>();
            if (host == null)
            {
                Debug.LogError("[CheatPanel] В сцене не найден EncounterHost! Убедитесь, что загружена игровая сцена.");
                return;
            }

            var runManager = RunManager.Instance;
            if (runManager != null && !runManager.HasActiveRun)
            {
                runManager.StartNewRun();
            }

            // Скрываем камеру и UI карты, если открыта карта
            var map = Object.FindFirstObjectByType<MapSceneController>();
            if (map != null)
                map.Hide();

            host.StartCoroutine(SwitchEncounterRoutine(host, def));
        }

        private static IEnumerator SwitchEncounterRoutine(EncounterHost host, EncounterDefinition def)
        {
            Debug.Log($"[CheatPanel] Смена энкаунтера на '{def.Id}' ({def.Kind})...");

            var runManager = RunManager.Instance;
            if (runManager != null)
            {
                var type = typeof(RunManager);
                type.GetProperty(nameof(RunManager.CurrentEncounterId))?.SetValue(runManager, def.Id);
                type.GetProperty(nameof(RunManager.CurrentEncounterKind))?.SetValue(runManager, def.Kind);
                type.GetProperty(nameof(RunManager.State))?.SetValue(runManager, RunState.EncounterLoading);
            }

            // Если загружаем магазин, подписываемся на кнопку выхода в карту
            if (def.Kind == EncounterKind.Shop && runManager != null)
            {
                void OnShopExit()
                {
                    host.OnExitRequested -= OnShopExit;
                    runManager.StartCoroutine(runManager.ReturnToMapRoutine());
                }
                host.OnExitRequested += OnShopExit;
            }

            // EnterRoutine сначала вызывает ExitRoutine() для старого рига, а затем собирает новый
            yield return host.EnterRoutine(def);

            if (runManager != null)
            {
                var type = typeof(RunManager);
                type.GetProperty(nameof(RunManager.State))?.SetValue(runManager, RunState.EncounterActive);
            }

            Debug.Log($"[CheatPanel] Энкаунтер '{def.Id}' успешно загружен.");
        }

        #endregion

        #region Inventory & Coins

        private static PlayerInventorySO GetInventory()
        {
            if (!Application.isPlaying) return null;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null) return null;
            return runManager.CurrentRun.PlayerInventory;
        }

        private void DrawCoins(PlayerInventorySO inventory)
        {
            EditorGUILayout.LabelField("Монеты", EditorStyles.boldLabel);

            if (inventory != null)
                EditorGUILayout.LabelField("Сейчас у игрока", inventory.Coins.ToString());

            _coinAmount = EditorGUILayout.IntField("Количество", _coinAmount);
            _dropPhysicalCoins = EditorGUILayout.Toggle("Кинуть монеты на стол", _dropPhysicalCoins);

            if (GUILayout.Button("Выдать монеты") && inventory != null)
            {
                inventory.Coins += _coinAmount;

                if (_dropPhysicalCoins && _coinAmount > 0)
                    CoinPileController.Instance?.DropCoins(_coinAmount);
            }
        }

        private void DrawItems(PlayerInventorySO inventory)
        {
            EditorGUILayout.LabelField("Предметы", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _itemFilter = EditorGUILayout.TextField("Поиск", _itemFilter);
                if (GUILayout.Button("Обновить", GUILayout.Width(80)))
                    RefreshItems();
            }

            _itemCount = Mathf.Max(1, EditorGUILayout.IntField("Количество", _itemCount));

            _itemScroll = EditorGUILayout.BeginScrollView(_itemScroll);
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(_itemFilter) &&
                    item.name.IndexOf(_itemFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(item.name);
                    if (GUILayout.Button("Выдать", GUILayout.Width(70)) && inventory != null)
                        GiveItem(inventory, item, _itemCount);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void GiveItem(PlayerInventorySO inventory, ItemDefinition item, int count)
        {
            var shopSpawner = Object.FindFirstObjectByType<ShopItemSpawner>();
            var playerSpawner = shopSpawner == null
                ? Object.FindFirstObjectByType<PlayerItemSpawner>()
                : null;

            for (int i = 0; i < count; i++)
            {
                if (!inventory.HasSpace(TurnSide.Player))
                {
                    Debug.LogWarning("[CheatPanel] В инвентаре игрока нет места.");
                    return;
                }

                if (shopSpawner != null)
                {
                    if (!shopSpawner.HasFreePlayerSlot)
                    {
                        Debug.LogWarning("[CheatPanel] На столе магазина нет свободных слотов.");
                        return;
                    }

                    if (shopSpawner.SpawnPlayerItem(item) == null)
                    {
                        Debug.LogWarning("[CheatPanel] Не удалось создать предмет на столе магазина.");
                        return;
                    }

                    inventory.Add(item, 1, TurnSide.Player);
                }
                else if (playerSpawner != null)
                {
                    if (!playerSpawner.HasFreeSlot)
                    {
                        Debug.LogWarning("[CheatPanel] На столе игрока нет свободных слотов.");
                        return;
                    }

                    if (playerSpawner.SpawnNewItem(item) == null)
                    {
                        Debug.LogWarning("[CheatPanel] Не удалось создать предмет на столе игрока.");
                        return;
                    }
                }
                else
                {
                    inventory.Add(item, 1, TurnSide.Player);
                }
            }
        }

        #endregion
    }
}