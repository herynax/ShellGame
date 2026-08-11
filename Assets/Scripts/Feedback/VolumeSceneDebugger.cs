using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Feedback
{
    public static class VolumeSceneDebugger
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            try
            {
                var volumes = Object.FindObjectsOfType<Volume>(true);
                Debug.Log($"VolumeSceneDebugger: found {volumes.Length} Volume objects in scene.");
                foreach (var v in volumes)
                {
                    var profileName = v.profile != null ? v.profile.name : "<null>";
                    Debug.Log($"VolumeSceneDebugger: Volume '{v.gameObject.name}' layer={LayerMask.LayerToName(v.gameObject.layer)} isGlobal={v.isGlobal} weight={v.weight} profile={profileName}");

                    if (v.profile != null)
                    {
                        if (v.profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vg))
                            Debug.Log($"  profile has Vignette intensity={vg.intensity.value} smoothness={vg.smoothness.value}");
                        else
                            Debug.Log("  profile missing Vignette override");

                        if (v.profile.TryGet<ChromaticAberrationVolume>(out var ca))
                            Debug.Log($"  profile has ChromaticAberrationVolume intensity={ca.intensity.value} warpAmp={ca.warpAmplitude.value} noiseAmp={ca.noiseAmplitude.value}");
                        else
                            Debug.Log("  profile missing ChromaticAberrationVolume override");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"VolumeSceneDebugger: exception while enumerating Volume objects: {ex}");
            }
        }
    }
}
