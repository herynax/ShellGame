using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class FogRenderFeature : ScriptableRendererFeature
    {
        FogPass fogPass;

        public override void Create()
        {
            fogPass = new FogPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(fogPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(fogPass?.Material);
            fogPass = null;
        }
    }

    public class FogPass : ScriptableRenderPass
    {
        private static readonly string shaderPath = "PostEffect/Fog";
        private const string k_RenderTag = "Render Fog Effects";

        static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
        static readonly int FogDistance = Shader.PropertyToID("_FogDistance");
        static readonly int FogColor = Shader.PropertyToID("_FogColor");
        static readonly int FogNear = Shader.PropertyToID("_FogNear");
        static readonly int FogFar = Shader.PropertyToID("_FogFar");
        static readonly int FogAltScale = Shader.PropertyToID("_FogAltScale");
        static readonly int FogThinning = Shader.PropertyToID("_FogThinning");
        static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");

        Fog fog;
        Material fogMaterial;

        public Material Material => fogMaterial;

        public FogPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;

            // Глубина читается в шейдере через SampleSceneDepth (_CameraDepthTexture).
            // В Render Graph требования собираются ДО записи пассов, поэтому флаг
            // нужно выставить здесь, а не в RecordRenderGraph.
            ConfigureInput(ScriptableRenderPassInput.Depth);

            var shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Fog] Shader not found at path: {shaderPath}");
                return;
            }
            this.fogMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (this.fogMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            this.fog = stack.GetComponent<Fog>();
            if (this.fog == null) return;
            if (!this.fog.IsActive()) return;

            this.fogMaterial.SetFloat(FogDensity, this.fog.fogDensity.value);
            this.fogMaterial.SetFloat(FogDistance, this.fog.fogDistance.value);
            this.fogMaterial.SetColor(FogColor, this.fog.fogColor.value);
            this.fogMaterial.SetFloat(FogNear, this.fog.fogNear.value);
            this.fogMaterial.SetFloat(FogFar, this.fog.fogFar.value);
            this.fogMaterial.SetFloat(FogAltScale, this.fog.fogAltScale.value);
            this.fogMaterial.SetFloat(FogThinning, this.fog.fogThinning.value);
            this.fogMaterial.SetFloat(NoiseScale, this.fog.noiseScale.value);
            this.fogMaterial.SetFloat(NoiseStrength, this.fog.noiseStrength.value);

            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempTargetFog", false);

            // Render Graph не даёт читать и писать одну текстуру в одном пассе,
            // поэтому эффект пишем во временную текстуру и копируем её обратно.
            RenderGraphUtils.BlitMaterialParameters effectParams =
                new RenderGraphUtils.BlitMaterialParameters(source, temp, this.fogMaterial, 0);
            renderGraph.AddBlitPass(effectParams, k_RenderTag);

            // Копия обязана быть point-семплинговой: дефолт AddBlitPass — ClampBilinear,
            // из-за чего весь кадр размывался после каждого эффекта.
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest, passName: $"{k_RenderTag} Copy Back");
        }
    }
}