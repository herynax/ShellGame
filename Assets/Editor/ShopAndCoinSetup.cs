using System.Collections.Generic;
using System.IO;
using ShellGame.Items;
using ShellGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShellGame.EditorTools
{
    /// <summary>
    /// Разовая автонастройка ассетов и сцены под систему монет/магазина.
    /// Запуск: меню ShellGame/Setup/Configure Shop and Coins
    /// или из батча: -executeMethod ShellGame.EditorTools.ShopAndCoinSetup.ConfigureAllBatch
    /// </summary>
    public static class ShopAndCoinSetup
    {
        public const int StartingCoins = 20;

        private const string ConfigsDir = "Assets/Resources/Configs";
        private const string EncountersDir = ConfigsDir + "/Encounters";
        private const string ItemsDir = ConfigsDir + "/Items";

        private const string PlayerInventoryPath = ConfigsDir + "/PlayerInventory.asset";
        private const string ShopConfigPath = EncountersDir + "/ShopConfig.asset";
        private const string ShopEncounterPath = EncountersDir + "/Shop.asset";
        private const string SellMarkerPath = ItemsDir + "/SellMarker.asset";
        private const string CoinPrefabPath = "Assets/Prefabs/Propses/Coin.prefab";
        private const string ShopRigPath = "Assets/Prefabs/EncoutersRigs/ShopEncouterRig.prefab";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        [MenuItem("ShellGame/Setup/Configure Shop and Coins")]
        public static void ConfigureAll()
        {
            var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
            if (coinPrefab == null)
            {
                Debug.LogError($"[ShopAndCoinSetup] Не найден префаб монеты по пути {CoinPrefabPath}");
                return;
            }

            EnsureFolder(ConfigsDir);
            EnsureFolder(ItemsDir);

            ConfigurePlayerInventory();
            var sellMarker = ConfigureSellMarker();
            ConfigureShopConfig(sellMarker);
            ConfigureShopRig();
            ConfigureShopEncounterDefinition();
            ConfigureGameScene(coinPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ShopAndCoinSetup] Настройка завершена.");
        }

        /// <summary>Точка входа для -executeMethod в batch mode.</summary>
        public static void ConfigureAllBatch() => ConfigureAll();

        // ---------------------------------------------------------------- PlayerInventory

        private static void ConfigurePlayerInventory()
        {
            var inv = AssetDatabase.LoadAssetAtPath<PlayerInventorySO>(PlayerInventoryPath);
            if (inv == null)
            {
                inv = ScriptableObject.CreateInstance<PlayerInventorySO>();
                AssetDatabase.CreateAsset(inv, PlayerInventoryPath);
            }

            var so = new SerializedObject(inv);
            so.FindProperty("_maxPlayerSlots").intValue = 4;
            so.FindProperty("_maxEnemySlots").intValue = 4;
            so.FindProperty("_startingCoins").intValue = StartingCoins;
            // _coins в ассете — только значение по умолчанию для визуализации;
            // рантайм всё равно создаётся через CreateInstance + BeginNewRun().
            if (so.FindProperty("_coins") != null)
                so.FindProperty("_coins").intValue = StartingCoins;
            if (so.FindProperty("_playerItems") != null)
                so.FindProperty("_playerItems").arraySize = 0;
            if (so.FindProperty("_enemyItems") != null)
                so.FindProperty("_enemyItems").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(inv);
            Debug.Log($"[ShopAndCoinSetup] PlayerInventory: стартовых монет = {StartingCoins}");
        }

        // ---------------------------------------------------------------- SellMarker

        private static UIMarkerItemDefinition ConfigureSellMarker()
        {
            var marker = AssetDatabase.LoadAssetAtPath<UIMarkerItemDefinition>(SellMarkerPath);
            if (marker == null)
            {
                marker = ScriptableObject.CreateInstance<UIMarkerItemDefinition>();
                AssetDatabase.CreateAsset(marker, SellMarkerPath);
            }

            marker.name = "SellMarker";
            // BuyPrice = 0 → предмет никогда не попадёт в ассортимент магазина.
            marker.BuyPrice = 0;
            marker.SellPriceMultiplier = 0f;

            // Внешний вид маркера: если у item-префабов есть отдельный визуал — укажи здесь.
            if (marker.WorldPrefab == null)
            {
                var fallback = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Propses/SellMarker.prefab");
                if (fallback != null)
                    marker.WorldPrefab = fallback;
            }

            EditorUtility.SetDirty(marker);
            return marker;
        }

        // ---------------------------------------------------------------- ShopConfig

        private static void ConfigureShopConfig(UIMarkerItemDefinition sellMarker)
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfig>(ShopConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ShopConfig>();
                AssetDatabase.CreateAsset(config, ShopConfigPath);
            }

            config.UseUnlockedItems = true;
            config.MaxShopItemsPerVisit = 4;
            config.OnlySellableItems = true;
            config.GrantFreeUnlockedItemOnEntry = true;
            config.SellPriceMultiplier = 0.5f;
            config.SellMarkerItemAsset = sellMarker;

            EditorUtility.SetDirty(config);
            Debug.Log("[ShopAndCoinSetup] ShopConfig: UseUnlockedItems=true, маркер продажи назначен.");
        }

        // ---------------------------------------------------------------- ShopRig

        private static void ConfigureShopRig()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ShopRigPath);
            if (root == null)
            {
                Debug.LogError($"[ShopAndCoinSetup] Не найден префаб магазина по пути {ShopRigPath}");
                return;
            }

            var prefab = PrefabUtility.LoadPrefabContents(ShopRigPath);
            try
            {
                var rig = prefab.GetComponent<ShopEncounterRig>();
                if (rig == null)
                {
                    Debug.LogError("[ShopAndCoinSetup] В префабе магазина нет ShopEncounterRig.");
                    return;
                }

                var tableRoot = FindDeep(prefab.transform, "Table_Root") ?? prefab.transform;

                // Спавнеры предметов
                var playerSpawner = prefab.GetComponentInChildren<PlayerItemSpawner>(true);
                if (playerSpawner == null)
                    playerSpawner = AddTo<PlayerItemSpawner>(tableRoot.gameObject);

                var shopSpawner = prefab.GetComponentInChildren<ShopItemSpawner>(true);
                if (shopSpawner == null)
                    shopSpawner = AddTo<ShopItemSpawner>(tableRoot.gameObject);

                // Точки спавна. Слоты игрока лежат на его стороне стола (z < 0 относительно
                // ItemSlots), слоты "EnemieItemSlot" — на стороне торговца: это витрина.
                var playerPoints = new List<ItemSpawnPoint>();
                var shopPoints = new List<ItemSpawnPoint>();
                foreach (var slot in prefab.GetComponentsInChildren<ItemSpawnPoint>(true))
                {
                    if (slot.name.StartsWith("EnemieItemSlot")) shopPoints.Add(slot);
                    else if (slot.name.StartsWith("PlayerItemSlot")) playerPoints.Add(slot);
                }

                AssignPoints(playerSpawner, "_spawnPoints", playerPoints);
                AssignPoints(shopSpawner, "_shopItemPoints", shopPoints);
                AssignPoints(shopSpawner, "_playerItemPoints", playerPoints);

                // Старый ItemSpawner конфликтует с новыми спавнерами (спавнит вторым набором).
                // На префаб магазина его больше никто не ссылается — просто гасим.
                foreach (var legacy in prefab.GetComponentsInChildren<ShellGame.Items.ItemSpawner>(true))
                {
                    if (legacy is PlayerItemSpawner || legacy is ShopItemSpawner) continue;
                    legacy.enabled = false;
                }

                // В магазине нет боя — гасим визуал врага, если он остался от боевого рига.
                var enemyVisual = FindDeep(prefab.transform, "Enemie");
                if (enemyVisual != null)
                    enemyVisual.gameObject.SetActive(false);

                // Зона монет магазина
                var coinZone = FindCoinZone(prefab.transform);
                if (coinZone == null)
                {
                    var go = new GameObject("CoinZone");
                    go.layer = prefab.layer;
                    var col = go.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    col.size = new Vector3(2.4f, 0.6f, 1.4f);

                    // Ставим рядом с зоной продажи, чтобы монеты лежали на том же столе
                    var sellZone = FindDeep(prefab.transform, "SellZone");
                    go.transform.SetParent(sellZone != null ? sellZone.parent : prefab.transform, false);
                    if (sellZone != null)
                    {
                        go.transform.position = sellZone.position + new Vector3(0f, 0.02f, 0f);
                        go.transform.rotation = sellZone.rotation;
                    }
                    coinZone = col;
                }
                coinZone.isTrigger = true;

                var so = new SerializedObject(rig);
                SetObjectRef(so, "_playerItemSpawner", playerSpawner);
                SetObjectRef(so, "_shopItemSpawner", shopSpawner);
                SetObjectRef(so, "_coinZone", coinZone);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(rig);
                PrefabUtility.SaveAsPrefabAsset(prefab, ShopRigPath);
                Debug.Log("[ShopAndCoinSetup] Префаб магазина: спавнеры и зона монет назначены.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void AssignPoints(BaseItemSpawner spawner, string propertyPath, List<ItemSpawnPoint> points)
        {
            if (spawner == null) return;

            var so = new SerializedObject(spawner);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
            {
                Debug.LogWarning($"[ShopAndCoinSetup] У {spawner.GetType().Name} нет поля {propertyPath}.");
                return;
            }

            prop.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Shop encounter

        /// <summary>
        /// Энкаунтер магазина обязан ссылаться на ShopEncounterRig, а не на боевой риг.
        /// Ошибка тут выглядит как «магазин грузится, но пустой».
        /// </summary>
        private static void ConfigureShopEncounterDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(ShopEncounterPath);
            if (def == null)
            {
                Debug.LogError($"[ShopAndCoinSetup] Не найден ассет энкаунтера магазина по пути {ShopEncounterPath}");
                return;
            }

            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(ShopRigPath);
            if (rig == null) return;

            if (def.RigPrefab != rig)
            {
                def.RigPrefab = rig;
                EditorUtility.SetDirty(def);
                Debug.Log($"[ShopAndCoinSetup] Энкаунтер магазина переведён на риг {rig.name}.");
            }
        }

        // ---------------------------------------------------------------- GameScene

        private static void ConfigureGameScene(GameObject coinPrefab)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var pile = FindDeep(scene.GetRootGameObjects(), "CoinPile");
            if (pile == null)
            {
                Debug.LogError("[ShopAndCoinSetup] В GameScene нет объекта CoinPile.");
                return;
            }

            // Зона монет: BoxCollider на самом CoinPile
            var zone = pile.GetComponent<BoxCollider>();
            if (zone == null)
                zone = pile.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            if (zone.size == Vector3.one)
                zone.size = new Vector3(1.6f, 0.6f, 1.2f);

            var pileController = pile.GetComponent<CoinPileController>();
            if (pileController != null)
            {
                var so = new SerializedObject(pileController);
                SetObjectRef(so, "_coinPrefab", coinPrefab.GetComponent<CoinPickupView>());
                SetObjectRef(so, "_coinZone", zone);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pileController);
            }

            // Контроллер награды нужен для анимации монет после смерти врага.
            // Ставим на тот же объект — он уже DontDestroyOnLoad вместе с кучей.
            var reward = pile.GetComponent<CoinRewardController>();
            if (reward == null)
                reward = pile.gameObject.AddComponent<CoinRewardController>();

            var rso = new SerializedObject(reward);
            SetObjectRef(rso, "_coinPrefab", coinPrefab.GetComponent<CoinPickupView>());
            rso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reward);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ShopAndCoinSetup] GameScene: CoinRewardController и префаб монеты назначены.");
        }

        // ---------------------------------------------------------------- helpers

        private static T AddTo<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetObjectRef(SerializedObject so, string propertyPath, Object value)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
            {
                Debug.LogWarning($"[ShopAndCoinSetup] Поле '{propertyPath}' не найдено в {so.targetObject.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static BoxCollider FindCoinZone(Transform root)
        {
            var named = FindDeep(root, "CoinZone");
            if (named != null)
            {
                var col = named.GetComponent<BoxCollider>();
                if (col != null) return col;
            }

            foreach (var col in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (col.isTrigger) return col;
            }
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDeep(GameObject[] roots, string name)
        {
            foreach (var root in roots)
            {
                var found = FindDeep(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
