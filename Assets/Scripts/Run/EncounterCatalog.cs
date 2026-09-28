using System.Collections.Generic;
using ShellGame.Map;
using UnityEngine;

namespace ShellGame.Run
{
    [CreateAssetMenu(fileName = "EncounterCatalog", menuName = "ShellGame/Run/Encounter Catalog")]
    public sealed class EncounterCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Configs/EncounterCatalog"; // Resources/Configs/EncounterCatalog.asset

        public List<EncounterDefinition> Encounters = new List<EncounterDefinition>();

        public bool TryGet(string id, out EncounterDefinition def)
        {
            def = Encounters.Find(e => e != null && e.Id == id);
            return def != null;
        }

        public EncounterDefinition PickRandom(EncounterKind kind, IRunRandom random)
        {
            var pool = Encounters.FindAll(e => e != null && e.Kind == kind && e.RigPrefab != null);
            return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
        }
    }
}