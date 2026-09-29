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
    public string ItemAssetName; // ItemDefinition.name � ���������� ������� ����� UnlocksConfig
    public int Count;
}

