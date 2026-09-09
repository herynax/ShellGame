// START OF FILE UnlocksConfig.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Items
{
    public enum UnlockConditionType
    {
        TotalDeaths,
        TotalWins
    }

    [Serializable]
    public class UnlockEntry
    {
        public ItemDefinition Item;

        [Tooltip("Если включено — предмет доступен с самого начала, условие ниже игнорируется. Используй для стартового набора предметов вместо фиктивных условий вроде 'TotalDeaths >= 0'.")]
        public bool UnlockedByDefault;

        public UnlockConditionType ConditionType;
        public int RequiredAmount;

        [Tooltip("Текст, который пишется вместо описания, если предмет закрыт")]
        public string LockedHintText = "Умрите 5 раз, чтобы открыть";
    }

    [CreateAssetMenu(fileName = "UnlocksConfig", menuName = "ShellGame/Meta/Unlocks Config")]
    public class UnlocksConfig : ScriptableObject
    {
        public Sprite UnknownIcon; // Спрайт вопроса
        public List<UnlockEntry> Entries = new List<UnlockEntry>();
    }
}
// END OF FILE