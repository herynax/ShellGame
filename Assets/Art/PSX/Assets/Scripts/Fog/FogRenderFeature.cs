using UnityEngine;
using UnityEngine.Rendering;
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

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            fogPass.Setup(renderer.cameraColorTargetHandle);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(fogPass);
        }

        protected override void Dispose(bool disposing)
        {
            fogPass?.Dispose();
        }
    }

    public class FogPass : ScriptableRenderPass
    {
        private static readonly string shaderPath = "PostEffect/Fog";
        static readonly string k_RenderTag = "Render Fog Effects";

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
        RTHandle currentTarget;
        RTHandle tempTarget;

        public FogPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Fog] Shader not found at path: {shaderPath}");
                return;
            }
            this.fogMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public void Setup(RTHandle currentTarget)
        {
            this.currentTarget = currentTarget;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // ВАЖНО: правильный URP-способ запросить текстуру глубины для этого пасса.
            // Замена старого camera.depthTextureMode, который в Execute срабатывает слишком поздно
            // и приводит к рассинхрону между Scene View и Game View / билдом.
            ConfigureInput(ScriptableRenderPassInput.Depth);

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempTarget, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_TempTargetFog");
        }

        public void Dispose()
        {
            tempTarget?.Release();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (this.fogMaterial == null) return;
            if (!renderingData.cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            this.fog = stack.GetComponent<Fog>();
            if (this.fog == null || !this.fog.IsActive()) return;

            var cmd = CommandBufferPool.Get(k_RenderTag);
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            this.fogMaterial.SetFloat(FogDensity, this.fog.fogDensity.value);
            this.fogMaterial.SetFloat(FogDistance, this.fog.fogDistance.value);
            this.fogMaterial.SetColor(FogColor, this.fog.fogColor.value);
            this.fogMaterial.SetFloat(FogNear, this.fog.fogNear.value);
            this.fogMaterial.SetFloat(FogFar, this.fog.fogFar.value);
            this.fogMaterial.SetFloat(FogAltScale, this.fog.fogAltScale.value);
            this.fogMaterial.SetFloat(FogThinning, this.fog.fogThinning.value);
            this.fogMaterial.SetFloat(NoiseScale, this.fog.noiseScale.value);
            this.fogMaterial.SetFloat(NoiseStrength, this.fog.noiseStrength.value);

            cmd.Blit(currentTarget, tempTarget);
            cmd.Blit(tempTarget, currentTarget, this.fogMaterial, 0);
        }
    }
}