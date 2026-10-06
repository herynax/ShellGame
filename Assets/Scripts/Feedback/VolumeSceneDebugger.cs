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
                var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                            Debug.Log($"  profile has ChromaticAberrationVolume intensity={ca.intensity.value}");
                        else
                            Debug.Log("  profile missing ChromaticAberrationVolume override");

                        if (v.profile.TryGet<WobbleVolume>(out var wb))
                            Debug.Log($"  profile has WobbleVolume warpAmp={wb.warpAmplitude.value} warpFreq={wb.warpFrequency.value} warpSpeed={wb.warpSpeed.value} noiseAmp={wb.noiseAmplitude.value}");
                        else
                            Debug.Log("  profile missing WobbleVolume override");

                        if (v.profile.TryGet<OldScreenVolume>(out var os))
                            Debug.Log($"  profile has OldScreenVolume curvature={os.curvature.value} aperture={os.apertureIntensity.value} scanlines={os.scanlineIntensity.value} roll={os.rollIntensity.value} bleed={os.bleedIntensity.value}");
                        else
                            Debug.Log("  profile missing OldScreenVolume override");
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
