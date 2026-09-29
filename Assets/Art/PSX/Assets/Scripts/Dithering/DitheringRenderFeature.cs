using System;
using UnityEngine;
using UnityEngine.Rendering;
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

        [Obsolete]
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            ditheringPass.Setup(renderer.cameraColorTargetHandle);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(ditheringPass);
        }

        protected override void Dispose(bool disposing)
        {
            ditheringPass?.Dispose();
        }
    }
    
    public class DitheringPass : ScriptableRenderPass
    {
        private static readonly string shaderPath = "PostEffect/Dithering";
        static readonly string k_RenderTag = "Render Dithering Effects";
        
        static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        static readonly int DitherThreshold = Shader.PropertyToID("_DitherThreshold");
        static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
        static readonly int DitherScale = Shader.PropertyToID("_DitherScale");
        
        Dithering dithering;
        Material ditheringMaterial;
        RTHandle currentTarget;
        RTHandle tempTarget; 
    
        public DitheringPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(shaderPath);
            if (shader == null) return;
            this.ditheringMaterial = CoreUtils.CreateEngineMaterial(shader);
        }
    
        public void Setup(RTHandle currentTarget)
        {
            this.currentTarget = currentTarget;
        }
 
        [Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RTHandles.Release(tempTarget);
            tempTarget = RTHandles.Alloc(desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_TempTargetDithering");
        }
 
        public void Dispose()
        {
            tempTarget?.Release();
        }
    
        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (this.ditheringMaterial == null) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
    
            var stack = VolumeManager.instance.stack;
            this.dithering = stack.GetComponent<Dithering>();
            if (this.dithering == null || !this.dithering.IsActive()) return;
    
            var cmd = CommandBufferPool.Get(k_RenderTag);
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    
        void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            
            this.ditheringMaterial.SetInt(PatternIndex, this.dithering.patternIndex.value);
            this.ditheringMaterial.SetFloat(DitherThreshold, this.dithering.ditherThreshold.value);
            this.ditheringMaterial.SetFloat(DitherStrength, this.dithering.ditherStrength.value);
            this.ditheringMaterial.SetFloat(DitherScale, this.dithering.ditherScale.value);
 
            cmd.Blit(currentTarget, tempTarget);
            cmd.Blit(tempTarget, currentTarget, this.ditheringMaterial, 0);
        }
    }
}