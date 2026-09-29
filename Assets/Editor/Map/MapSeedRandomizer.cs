using UnityEditor;
using UnityEngine;
using ShellGame.Meta;

namespace ShellGame.Map
{
    public static class MapSeedRandomizer
    {
        [MenuItem("Tools/Map/Randomize Run Seed")]
        public static void RandomizeRunSeed()
        {
            var data = RunCheckpointStorage.Load();

            int currentSeed = data?.RunSeed ?? 0;
            int newSeed;

            if (currentSeed == 0)
            {
                newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            else
            {
                newSeed = currentSeed + 1;
                if (newSeed == int.MinValue) newSeed = int.MaxValue;
            }

            if (data == null)
            {
                data = new RunCheckpointData
                {
                    RunSeed = newSeed,
                    RunIsFirstRun = true
                };
            }
            else
            {
                data.RunSeed = newSeed;
            }

            RunCheckpointStorage.Save(data);

            Debug.Log($"[MapSeedRandomizer] Seed randomized: {currentSeed} -> {newSeed}");
        }
    }
}