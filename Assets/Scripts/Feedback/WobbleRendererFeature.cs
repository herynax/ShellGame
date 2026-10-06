using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using ShellGame.Feedback;
using ShellGame.Rendering;

/// <summary>
/// Полноэкранный пасс «плавания» экрана: синусоидальный screen warp плюс
/// шумовое дрожание, управляемые через WobbleVolume в Volume-стеке.
/// Источник приходит в _BlitTexture, геометрия — процедурный треугольник.
/// </summary>
public sealed class WobbleRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader _shader;
    [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    private Material _material;
    private WobblePass _pass;

    public override void Create()
    {
        if (_shader == null)
            _shader = Shader.Find("Hidden/ShellGame/Wobble");

        if (_shader == null)
        {
            Debug.LogWarning("WobbleRendererFeature: шейдер не найден/не назначен.");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(_shader);
        if (_material == null)
            Debug.LogWarning("WobbleRendererFeature: материал не создан из шейдера.");

        _pass = new WobblePass(_material) { renderPassEvent = _renderPassEvent };
    }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null) return;
            if (!PSXCameraFilter.ShouldEnqueue(ref renderingData.cameraData)) return;


        var stack = VolumeManager.instance.stack;
        var component = stack.GetComponent<WobbleVolume>();
        if (component == null)
            return;

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

    private sealed class WobblePass : ScriptableRenderPass
    {
        private const string k_RenderTag = "ShellGame Wobble (PSX)";

        static readonly int WarpAmplitudeId = Shader.PropertyToID("_WarpAmplitude");
        static readonly int WarpFrequencyId = Shader.PropertyToID("_WarpFrequency");
        static readonly int WarpSpeedId = Shader.PropertyToID("_WarpSpeed");
        static readonly int NoiseAmplitudeId = Shader.PropertyToID("_NoiseAmplitude");
        static readonly int NoiseFrequencyId = Shader.PropertyToID("_NoiseFrequency");
        static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");

        private readonly Material _material;

        public float WarpAmplitude;
        public float WarpFrequency;
        public float WarpSpeed;
        public float NoiseAmplitude;
        public float NoiseFrequency;
        public float NoiseSpeed;

        public WobblePass(Material material)
        {
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (!PSXCameraFilter.ShouldRender(cameraData)) return;

            _material.SetFloat(WarpAmplitudeId, WarpAmplitude);
            _material.SetFloat(WarpFrequencyId, WarpFrequency);
            _material.SetFloat(WarpSpeedId, WarpSpeed);
            _material.SetFloat(NoiseAmplitudeId, NoiseAmplitude);
            _material.SetFloat(NoiseFrequencyId, NoiseFrequency);
            _material.SetFloat(NoiseSpeedId, NoiseSpeed);

            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle source = resourceData.cameraColor;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_WobbleTempPSX", false);

            // 1) source -> temp через материал Wobble (шейдер читает _BlitTexture)
            var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
            renderGraph.AddBlitPass(blitParams, passName: $"{k_RenderTag} Apply");

            // 2) temp -> source (копия назад)
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero,
                filterMode: RenderGraphUtils.BlitFilterMode.ClampBilinear,
                passName: $"{k_RenderTag} Copy Back");
        }
    }
}