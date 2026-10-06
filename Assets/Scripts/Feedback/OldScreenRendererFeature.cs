using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using ShellGame.Rendering;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Полноэкранный эффект старого экрана (CRT): кривизна, апертура,
    /// сканлайны, interlace, бегущая полоса, свечение фосфора и шум.
    /// Управляется через OldScreenVolume в Volume-стеке.
    ///
    /// Render Graph: источник приходит в _BlitTexture, геометрия — процедурный
    /// полноэкранный треугольник (только SV_VertexID), поэтому Vert/Varyings
    /// шейдер объявляет сам.
    /// </summary>
    public sealed class OldScreenRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private Material _material;
        private OldScreenPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find("Hidden/ShellGame/OldScreen");

            if (_shader == null)
            {
                Debug.LogWarning("OldScreenRendererFeature: шейдер не найден/не назначен.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            if (_material == null)
                Debug.LogWarning("OldScreenRendererFeature: материал не создан из шейдера.");

            _pass = new OldScreenPass(_material) { renderPassEvent = _renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (!PSXCameraFilter.ShouldEnqueue(ref renderingData.cameraData)) return;

            var stack = VolumeManager.instance.stack;
            var component = stack.GetComponent<OldScreenVolume>();
            if (component == null) return;

            _pass.Curvature = component.curvature.value;
            _pass.CornerRounding = component.cornerRounding.value;
            _pass.VignetteIntensity = component.vignetteIntensity.value;
            _pass.ApertureIntensity = component.apertureIntensity.value;
            _pass.ApertureResolution = component.apertureResolution.value;
            _pass.ScanlineIntensity = component.scanlineIntensity.value;
            _pass.ScanlineCount = component.scanlineCount.value;
            _pass.InterlaceIntensity = component.interlaceIntensity.value;
            _pass.InterlaceSpeed = component.interlaceSpeed.value;
            _pass.RollIntensity = component.rollIntensity.value;
            _pass.RollSpeed = component.rollSpeed.value;
            _pass.RollWidth = component.rollWidth.value;
            _pass.BleedIntensity = component.bleedIntensity.value;
            _pass.NoiseIntensity = component.noiseIntensity.value;
            _pass.NoiseScale = component.noiseScale.value;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class OldScreenPass : ScriptableRenderPass
        {
            private const string k_RenderTag = "ShellGame Old Screen (CRT)";

            static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
            static readonly int CornerRoundingId = Shader.PropertyToID("_CornerRounding");
            static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
            static readonly int ApertureIntensityId = Shader.PropertyToID("_ApertureIntensity");
            static readonly int ApertureResolutionId = Shader.PropertyToID("_ApertureResolution");
            static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
            static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
            static readonly int InterlaceIntensityId = Shader.PropertyToID("_InterlaceIntensity");
            static readonly int InterlaceSpeedId = Shader.PropertyToID("_InterlaceSpeed");
            static readonly int RollIntensityId = Shader.PropertyToID("_RollIntensity");
            static readonly int RollSpeedId = Shader.PropertyToID("_RollSpeed");
            static readonly int RollWidthId = Shader.PropertyToID("_RollWidth");
            static readonly int BleedIntensityId = Shader.PropertyToID("_BleedIntensity");
            static readonly int NoiseIntensityId = Shader.PropertyToID("_NoiseIntensity");
            static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");

            private readonly Material _material;

            public float Curvature;
            public float CornerRounding;
            public float VignetteIntensity;
            public float ApertureIntensity;
            public float ApertureResolution;
            public float ScanlineIntensity;
            public float ScanlineCount;
            public float InterlaceIntensity;
            public float InterlaceSpeed;
            public float RollIntensity;
            public float RollSpeed;
            public float RollWidth;
            public float BleedIntensity;
            public float NoiseIntensity;
            public float NoiseScale;

            public OldScreenPass(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
            if (_material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!PSXCameraFilter.ShouldRender(cameraData)) return;

            _material.SetFloat(CurvatureId, Curvature);
                _material.SetFloat(CornerRoundingId, CornerRounding);
                _material.SetFloat(VignetteIntensityId, VignetteIntensity);
                _material.SetFloat(ApertureIntensityId, ApertureIntensity);
                _material.SetFloat(ApertureResolutionId, ApertureResolution);
                _material.SetFloat(ScanlineIntensityId, ScanlineIntensity);
                _material.SetFloat(ScanlineCountId, ScanlineCount);
                _material.SetFloat(InterlaceIntensityId, InterlaceIntensity);
                _material.SetFloat(InterlaceSpeedId, InterlaceSpeed);
                _material.SetFloat(RollIntensityId, RollIntensity);
                _material.SetFloat(RollSpeedId, RollSpeed);
                _material.SetFloat(RollWidthId, RollWidth);
                _material.SetFloat(BleedIntensityId, BleedIntensity);
                _material.SetFloat(NoiseIntensityId, NoiseIntensity);
                _material.SetFloat(NoiseScaleId, NoiseScale);

                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;

                // cameraColor is always a valid RenderGraph texture (unlike activeColorTexture
                // which can be the system back buffer without a valid descriptor)
                TextureHandle source = resourceData.cameraColor;
                TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, descriptor, "_OldScreenTempPSX", false);

                // Render Graph не даёт читать и писать одну текстуру в одном пассе,
                // поэтому эффект пишем во временную текстуру и копируем её обратно.
                RenderGraphUtils.BlitMaterialParameters effectParams =
                    new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(effectParams, k_RenderTag);

                // Шейдер сэмплит билинейно, поэтому копия тоже билинейная.
                // Копируем обратно в activeColorTexture (cameraColor или backBuffer).
                renderGraph.AddBlitPass(temp, resourceData.activeColorTexture, Vector2.one, Vector2.zero,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampBilinear, passName: $"{k_RenderTag} Copy Back");
            }
        }
    }
}