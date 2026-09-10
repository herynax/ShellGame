    using System.Collections.Generic;
    using ShellGame.Core;
    using UnityEngine;

    namespace ShellGame.Meta
    {
        [System.Serializable]
        public sealed class ShellCheckpointData
        {
            public int SlotIndex;
            public bool HasMarker;
        }

        [System.Serializable]
        public sealed class ItemStackCheckpointData
        {
            public string ItemAssetName; // ItemDefinition.name — резолвится обратно через UnlocksConfig/фоллбэк-список ItemSpawner'а
            public int Count;
        }

        [System.Serializable]
        public sealed class RunCheckpointData
        {
            public string SceneName;
            public int LevelIndex;
            public float DifficultyIndex;
            public int CompletedRoundsInSession;
            public int MaxShellsPenalty;
            public TurnSide ActiveSide;

            public int PlayerHealth;
            public int PlayerMaxHealth;
            public int EnemyHealth;
            public int EnemyMaxHealth;

            public List<ItemStackCheckpointData> PlayerItems = new List<ItemStackCheckpointData>();
            public List<ItemStackCheckpointData> EnemyItems = new List<ItemStackCheckpointData>();
            public List<ShellCheckpointData> Shells = new List<ShellCheckpointData>();
        }

        /// <summary>
        /// Единственная точка чтения/записи чекпоинта забега. Намеренно НЕ через
        /// Zenject — нужен и в игровых сценах, и в главном меню
        /// (MainMenuController сейчас вообще без DI), заводить под это отдельный
        /// биндинг/контейнер избыточно. Хранит ровно один чекпоинт (последний
        /// сохранённый раунд) — предыдущий перетирается при каждом новом
        /// сохранении.
        /// </summary>
        public static class RunCheckpointStorage
        {
            private const string Key = "ShellGame_RunCheckpoint";

            public static bool HasCheckpoint => PlayerPrefs.HasKey(Key);

            public static void Save(RunCheckpointData data)
            {
                if (data == null) return;
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save();
            }

            public static RunCheckpointData Load()
            {
                if (!HasCheckpoint) return null;
                string json = PlayerPrefs.GetString(Key, string.Empty);
                if (string.IsNullOrEmpty(json)) return null;

                try
                {
                    return JsonUtility.FromJson<RunCheckpointData>(json);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RunCheckpointStorage] Не удалось распарсить чекпоинт, считаю его отсутствующим: {e.Message}");
                    Clear();
                    return null;
                }
            }

            public static void Clear()
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
        }
    }