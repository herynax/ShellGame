using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using ShellGame.Rendering;

namespace PSX
{
    public class PixelationRenderFeature : ScriptableRendererFeature
    {
        PixelationPass pixelationPass;

        public override void Create()
        {
            pixelationPass = new PixelationPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!PSXCameraFilter.ShouldEnqueue(ref renderingData.cameraData)) return;
            renderer.EnqueuePass(pixelationPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(pixelationPass?.Material);
            pixelationPass = null;
        }
    }

    public class PixelationPass : ScriptableRenderPass
    {
        private static readonly string shaderPath = "PostEffect/Pixelation";
        private const string k_RenderTag = "Render Pixelation Effects";
        static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
        static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
        static readonly int ColorPrecison = Shader.PropertyToID("_ColorPrecision");

        Pixelation pixelation;
        Material pixelationMaterial;

        public Material Material => pixelationMaterial;

        public PixelationPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Pixelation] Shader not found at path: {shaderPath}. Проверьте компиляцию шейдера в консоли и путь.");
                return;
            }
            this.pixelationMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (this.pixelationMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!PSXCameraFilter.ShouldRender(cameraData)) return;

            var stack = VolumeManager.instance.stack;
            this.pixelation = stack.GetComponent<Pixelation>();
            if (this.pixelation == null) return;
            if (!this.pixelation.IsActive()) return;

            this.pixelationMaterial.SetFloat(WidthPixelation, this.pixelation.widthPixelation.value);
            this.pixelationMaterial.SetFloat(HeightPixelation, this.pixelation.heightPixelation.value);
            this.pixelationMaterial.SetFloat(ColorPrecison, this.pixelation.colorPrecision.value);

            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempTargetPixelation", false);

            // Render Graph не даёт читать и писать одну текстуру в одном пассе,
            // поэтому эффект пишем во временную текстуру и копируем её обратно.
            RenderGraphUtils.BlitMaterialParameters effectParams =
                new RenderGraphUtils.BlitMaterialParameters(source, temp, this.pixelationMaterial, 0);
            renderGraph.AddBlitPass(effectParams, k_RenderTag);

            // Копия обязана быть point-семплинговой: дефолт AddBlitPass — ClampBilinear,
            // из-за чего весь кадр размывался после каждого эффекта.
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest, passName: $"{k_RenderTag} Copy Back");
        }
    }
}