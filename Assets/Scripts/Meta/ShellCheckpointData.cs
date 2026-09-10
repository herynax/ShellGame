using ShellGame.Core;
using System.Collections.Generic;

[System.Serializable]
public sealed class ShellCheckpointData
{
    public int SlotIndex;
    public bool HasMarker;
}

[System.Serializable]
public sealed class ItemStackCheckpointData
{
    public string ItemAssetName; // ItemDefinition.name — резолвится обратно через UnlocksConfig
    public int Count;
}

[System.Serializable]
public sealed class RunCheckpointData
{
    public int LevelIndex;
    public float DifficultyIndex;
    public int CompletedRoundsInSession;
    public int MaxShellsPenalty;
    public TurnSide ActiveSide;
    public int PlayerHealth;
    public int EnemyHealth;
    public int PlayerMaxHealth;
    public int EnemyMaxHealth;
    public List<ItemStackCheckpointData> PlayerItems = new();
    public List<ItemStackCheckpointData> EnemyItems = new();
    public List<ShellCheckpointData> Shells = new();
    public string SceneName;
}