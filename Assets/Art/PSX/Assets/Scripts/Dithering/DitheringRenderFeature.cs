using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace PSX
{
    public class DitheringRenderFeature : ScriptableRendererFeature
    {
        DitheringPass ditheringPass;

        public override void Create()
        {
            ditheringPass = new DitheringPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(ditheringPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(ditheringPass?.Material);
            ditheringPass = null;
        }
    }

    public class DitheringPass : ScriptableRenderPass
    {
        private static readonly string shaderPath = "PostEffect/Dithering";
        private const string k_RenderTag = "Render Dithering Effects";

        static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        static readonly int DitherThreshold = Shader.PropertyToID("_DitherThreshold");
        static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
        static readonly int DitherScale = Shader.PropertyToID("_DitherScale");

        Dithering dithering;
        Material ditheringMaterial;

        public Material Material => ditheringMaterial;

        public DitheringPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Dithering] Shader not found at path: {shaderPath}. Проверьте компиляцию шейдера в консоли и путь.");
                return;
            }
            this.ditheringMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (this.ditheringMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            this.dithering = stack.GetComponent<Dithering>();
            if (this.dithering == null) return;
            if (!this.dithering.IsActive()) return;

            this.ditheringMaterial.SetInt(PatternIndex, this.dithering.patternIndex.value);
            this.ditheringMaterial.SetFloat(DitherThreshold, this.dithering.ditherThreshold.value);
            this.ditheringMaterial.SetFloat(DitherStrength, this.dithering.ditherStrength.value);
            this.ditheringMaterial.SetFloat(DitherScale, this.dithering.ditherScale.value);

            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempTargetDithering", false);

            // Render Graph не даёт читать и писать одну текстуру в одном пассе,
            // поэтому эффект пишем во временную текстуру и копируем её обратно.
            RenderGraphUtils.BlitMaterialParameters effectParams =
                new RenderGraphUtils.BlitMaterialParameters(source, temp, this.ditheringMaterial, 0);
            renderGraph.AddBlitPass(effectParams, k_RenderTag);

            // Копия обязана быть point-семплинговой: дефолт AddBlitPass — ClampBilinear,
            // из-за чего весь кадр размывался после каждого эффекта.
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest, passName: $"{k_RenderTag} Copy Back");
        }
    }
}