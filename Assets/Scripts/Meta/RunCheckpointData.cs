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
    public sealed class CoinPileCheckpointData
    {
        public Vector3 Position;     // Local position within coin zone
        public int CoinCount;
        public bool IsStacked;
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

        // --- Прогресс рана (основа новой формулы сложности) ---
        // Старые чекпоинты этих полей не содержат: JsonUtility даст 0, и
        // GameSessionProgression восстановит EncountersClearedInRun из LevelIndex.
        public int EncountersClearedInRun;
        public int RoundsInCurrentEncounter;

        // Id пресета сложности. Старые чекпоинты его не содержат: JsonUtility
        // даст null -> GameSessionProgression возьмёт «Средний» по умолчанию.
        public string DifficultyPresetId;

        public int PlayerHealth;
        public int PlayerMaxHealth;
        public int EnemyHealth;
        public int EnemyMaxHealth;

        public List<ItemStackCheckpointData> PlayerItems = new List<ItemStackCheckpointData>();
        public List<ItemStackCheckpointData> EnemyItems = new List<ItemStackCheckpointData>();
        public List<ShellCheckpointData> Shells = new List<ShellCheckpointData>();

        // Coins & Coin Piles
        public int PlayerCoins = 0;
        public List<CoinPileCheckpointData> CoinPiles = new List<CoinPileCheckpointData>();

        // ---- данные забега (карта) ----
        public bool HasRunData;
        public bool OnMap;
        public int RunSeed;
        public bool RunIsFirstRun;
        public int MapCurrentNodeId;
        public List<int> MapCompletedNodeIds = new List<int>();
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