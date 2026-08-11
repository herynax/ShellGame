using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Feedback
{
    public static class VolumeStackDebugger
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            try
            {
                var stack = VolumeManager.instance.stack;
                Debug.Log("VolumeStackDebugger: VolumeManager.stack available, querying known components...");

                var vignette = stack.GetComponent<UnityEngine.Rendering.Universal.Vignette>();
                if (vignette != null)
                {
                    Debug.Log($"VolumeStackDebugger: Vignette found intensity={vignette.intensity.value} smoothness={vignette.smoothness.value}");
                }
                else
                {
                    Debug.Log("VolumeStackDebugger: Vignette not found in stack.");
                }

                var chroma = stack.GetComponent<ChromaticAberrationVolume>();
                if (chroma != null)
                {
                    Debug.Log($"VolumeStackDebugger: ChromaticAberrationVolume found intensity={chroma.intensity.value} warpAmp={chroma.warpAmplitude.value} noiseAmp={chroma.noiseAmplitude.value}");
                }
                else
                {
                    Debug.Log("VolumeStackDebugger: ChromaticAberrationVolume not found in stack.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"VolumeStackDebugger: exception while reading Volume stack: {ex}");
            }
        }
    }
}
