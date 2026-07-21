using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Полноэкранный PSX-постэффект: хроматическая аберрация + screen warp,
    /// управляемые через ChromaticAberrationVolume в Volume-стеке.
    ///
    /// Execute использует классический cmd.Blit(source, dest, material) —
    /// он сам подставляет _MainTex, никаких доп. свойств/макросов не нужно.
    /// </summary>
    public sealed class ChromaticAberrationRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private Material _material;
        private ChromaticAberrationPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find("Hidden/ShellGame/ChromaticAberration");

            if (_shader == null)
            {
                Debug.LogWarning("ChromaticAberrationRendererFeature: шейдер не найден/не назначен.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new ChromaticAberrationPass(_material) { renderPassEvent = _renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var stack = VolumeManager.instance.stack;
            var component = stack.GetComponent<ChromaticAberrationVolume>();
            if (component == null || !component.IsActive()) return;

            _pass.Intensity = component.intensity.value;
            _pass.WarpAmplitude = component.warpAmplitude.value;
            _pass.WarpFrequency = component.warpFrequency.value;
            _pass.WarpSpeed = component.warpSpeed.value;
            _pass.NoiseAmplitude = component.noiseAmplitude.value;
            _pass.NoiseFrequency = component.noiseFrequency.value;
            _pass.NoiseSpeed = component.noiseSpeed.value;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _pass?.Dispose();
        }

        private sealed class ChromaticAberrationPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private RTHandle _tempHandle;

            public float Intensity;
            public float WarpAmplitude;
            public float WarpFrequency;
            public float WarpSpeed;
            public float NoiseAmplitude;
            public float NoiseFrequency;
            public float NoiseSpeed;

            public ChromaticAberrationPass(Material material)
            {
                _material = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempHandle, descriptor, name: "_ChromaticAberrationTempPSX");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null) return;

                var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cmd = CommandBufferPool.Get("ShellGame Chromatic Aberration (PSX)");

                _material.SetFloat("_Intensity", Intensity);
                _material.SetFloat("_WarpAmplitude", WarpAmplitude);
                _material.SetFloat("_WarpFrequency", WarpFrequency);
                _material.SetFloat("_WarpSpeed", WarpSpeed);
                _material.SetFloat("_NoiseAmplitude", NoiseAmplitude);
                _material.SetFloat("_NoiseFrequency", NoiseFrequency);
                _material.SetFloat("_NoiseSpeed", NoiseSpeed);

                cmd.Blit(cameraTarget, _tempHandle, _material);
                cmd.Blit(_tempHandle, cameraTarget);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _tempHandle?.Release();
            }
        }
    }
}
