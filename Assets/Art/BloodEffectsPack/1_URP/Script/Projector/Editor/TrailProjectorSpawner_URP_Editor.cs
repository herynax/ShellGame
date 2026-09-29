using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BloodEffectsPack
{
    [CustomEditor(typeof(BloodEffectsPack.TrailProjectorSpawner_URP))]
    public class TrailProjectorSpawner_URP_Editor : Editor
    {
        public override void OnInspectorGUI()
        {
            var spawner = (BloodEffectsPack.TrailProjectorSpawner_URP)target;
            EditorGUILayout.LabelField("Rendering Layers", EditorStyles.boldLabel);

            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            string[] layerNames = GetRenderingLayerNames(pipelineAsset);

            spawner.renderingLayerMask = EditorGUILayout.MaskField("Rendering Layer Mask", spawner.renderingLayerMask, layerNames);

            // Draw default inspector
            DrawDefaultInspector();

            // Apply changes
            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }

        private static string[] GetRenderingLayerNames(UnityEngine.Rendering.RenderPipelineAsset pipelineAsset)
        {
#if UNITY_2023_1_OR_NEWER
            if (pipelineAsset != null)
            {
                var names = new string[32];
                for (int i = 0; i < 32; i++)
                {
                    names[i] = RenderingLayerMask.RenderingLayerToName(i);
                }
                return names;
            }
#endif
            return new string[] { "Layer 0", "Layer 1", "Layer 2", "Layer 3" };
        }
    }
}