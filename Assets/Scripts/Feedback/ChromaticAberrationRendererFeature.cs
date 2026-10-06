using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using ShellGame.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Полноэкранный PSX-постэффект: хроматическая аберрация, управляемая через
    /// ChromaticAberrationVolume в Volume-стеке.
    ///
    /// Screen warp и шум раньше были здесь же, но вынесены в отдельную фичу
    /// (WobbleRendererFeature + WobbleVolume), потому что «плавание» экрана —
    /// самостоятельный эффект, не зависимый от хроматики.
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

            _pass = new ChromaticAberrationPass(_material) { renderPassEvent = _renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (!PSXCameraFilter.ShouldEnqueue(ref renderingData.cameraData)) return;

            var stack = VolumeManager.instance.stack;
            var component = stack.GetComponent<ChromaticAberrationVolume>();
            if (component == null)
            {
                Debug.Log("ChromaticAberrationRendererFeature: ChromaticAberrationVolume component not found in Volume stack.");
                return;
            }

            _pass.Intensity = component.intensity.value;
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

            private readonly Material _material;

            public float Intensity;

            public ChromaticAberrationPass(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
            if (_material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!PSXCameraFilter.ShouldRender(cameraData)) return;

            _material.SetFloat(IntensityId, Intensity);

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;

                // cameraColor is always a valid RenderGraph texture (unlike activeColorTexture
                // which can be the system back buffer without a valid descriptor)
                TextureHandle source = resourceData.cameraColor;
                TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, descriptor, "_ChromaticAberrationTempPSX", false);

                // Render Graph не даёт читать и писать одну текстуру в одном пассе,
                // поэтому эффект пишем во временную текстуру и копируем её обратно.
                RenderGraphUtils.BlitMaterialParameters effectParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(effectParams, k_RenderTag);

                // Шейдер этого эффекта сэмплит билинейно, поэтому копия тоже билинейная.
                // Копируем обратно в activeColorTexture (cameraColor или backBuffer).
                renderGraph.AddBlitPass(temp, resourceData.activeColorTexture, Vector2.one, Vector2.zero,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampBilinear, passName: $"{k_RenderTag} Copy Back");
            }
        }
    }
}