using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Полноэкранный PSX-постэффект: хроматическая аберрация + screen warp,
    /// управляемые через ChromaticAberrationVolume в Volume-стеке.
    ///
    /// Render Graph: источник приходит в _BlitTexture, геометрия — процедурный
    /// полноэкранный треугольник (только SV_VertexID), поэтому Vert/Varyings
    /// шейдер объявляет сам.
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
            if (_material == null)
            {
                Debug.LogWarning("ChromaticAberrationRendererFeature: материал не создан из шейдера.");
            }
            else
            {
                Debug.Log("ChromaticAberrationRendererFeature: шейдер найден и материал создан.");
            }

            _pass = new ChromaticAberrationPass(_material) { renderPassEvent = _renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var stack = VolumeManager.instance.stack;
            var component = stack.GetComponent<ChromaticAberrationVolume>();
            if (component == null)
            {
                Debug.Log("ChromaticAberrationRendererFeature: ChromaticAberrationVolume component not found in Volume stack.");
                return;
            }

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
            _material = null;
            _pass = null;
        }

        private sealed class ChromaticAberrationPass : ScriptableRenderPass
        {
            private const string k_RenderTag = "ShellGame Chromatic Aberration (PSX)";

            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int WarpAmplitudeId = Shader.PropertyToID("_WarpAmplitude");
            static readonly int WarpFrequencyId = Shader.PropertyToID("_WarpFrequency");
            static readonly int WarpSpeedId = Shader.PropertyToID("_WarpSpeed");
            static readonly int NoiseAmplitudeId = Shader.PropertyToID("_NoiseAmplitude");
            static readonly int NoiseFrequencyId = Shader.PropertyToID("_NoiseFrequency");
            static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");

            private readonly Material _material;

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

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();


                _material.SetFloat(IntensityId, Intensity);
                _material.SetFloat(WarpAmplitudeId, WarpAmplitude);
                _material.SetFloat(WarpFrequencyId, WarpFrequency);
                _material.SetFloat(WarpSpeedId, WarpSpeed);
                _material.SetFloat(NoiseAmplitudeId, NoiseAmplitude);
                _material.SetFloat(NoiseFrequencyId, NoiseFrequency);
                _material.SetFloat(NoiseSpeedId, NoiseSpeed);

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;

                TextureHandle source = resourceData.activeColorTexture;
                TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, descriptor, "_ChromaticAberrationTempPSX", false);

                // Render Graph не даёт читать и писать одну текстуру в одном пассе,
                // поэтому эффект пишем во временную текстуру и копируем её обратно.
                RenderGraphUtils.BlitMaterialParameters effectParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(effectParams, k_RenderTag);

                // Шейдер этого эффекта сэмплит билинейно, поэтому копия тоже билинейная.
                renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampBilinear, passName: $"{k_RenderTag} Copy Back");
            }
        }
    }
}