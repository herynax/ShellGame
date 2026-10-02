Shader "Hidden/ShellGame/ChromaticAberration"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "ChromaticAberrationPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // Единственная зависимость. Даёт _Time, sampler_LinearClamp
            // (через GlobalSamplers.hlsl), макросы SAMPLE_TEXTURE2D_X и
            // GetFullScreenTriangle*.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // BlitMaterialParameters биндит источник в _BlitTexture и рисует
            // процедурный полноэкранный треугольник (вершинного буфера нет).
            TEXTURE2D(_BlitTexture);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float _Intensity;

            // Screen warp — плавное "плавание" картинки (не трогали).
            float _WarpAmplitude;
            float _WarpFrequency;
            float _WarpSpeed;

            // Шум поверх варпа — включается ближе к передозу.
            // Амплитуда = 0, пока доза < половины порога.
            float _NoiseAmplitude;
            float _NoiseFrequency;
            float _NoiseSpeed;

            // --- простой value-noise на хэше, без текстур ---
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // Смещение для одного канала — свой "seed", чтобы R/G/B плыли не синхронно.
            float2 noiseOffset(float2 uv, float seed)
            {
                float2 t = float2(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.77);
                float n1 = valueNoise(uv * _NoiseFrequency + t + seed);
                float n2 = valueNoise(uv * _NoiseFrequency + t.yx + seed + 91.7);
                return (float2(n1, n2) - 0.5) * _NoiseAmplitude;
            }

            float2 screenWarp(float2 uv)
            {
                float2 warped = uv;

                float waveX = sin(uv.y * _WarpFrequency + _Time.y * _WarpSpeed);
                float waveY = cos(uv.x * _WarpFrequency + _Time.y * _WarpSpeed * 0.8);
                warped.x += waveX * _WarpAmplitude;
                warped.y += waveY * _WarpAmplitude * 0.6;

                float waveX2 = sin((uv.x + uv.y) * _WarpFrequency * 0.65 + _Time.y * _WarpSpeed * 1.35);
                float waveY2 = cos((uv.y - uv.x) * _WarpFrequency * 0.85 + _Time.y * _WarpSpeed * 1.15);
                warped.x += waveX2 * _WarpAmplitude * 0.35;
                warped.y += waveY2 * _WarpAmplitude * 0.3;

                return warped;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;
                float2 warped = screenWarp(uv);

                float2 centerOffset = warped - 0.5;
                float distanceFromCenter = length(centerOffset);
                float2 direction = centerOffset / max(distanceFromCenter, 1e-5);

                float chromaStrength = _Intensity * (0.035 + distanceFromCenter * 0.08) * (1.0 + _Intensity * 1.25);
                float2 noiseDrift = noiseOffset(warped, 0.0) + noiseOffset(warped * 1.35 + 7.1, 18.4) * 0.5;

                float2 uvR = warped - direction * chromaStrength + noiseDrift * 0.7;
                float2 uvG = warped + noiseDrift;
                float2 uvB = warped + direction * chromaStrength + noiseDrift * 0.9;

                // Bilinear-семплинг: каналы берутся со смещением, сглаживание убирает ступеньки.
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvR).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvG).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).b;

                return half4(r, g, b, 1);
            }
            ENDHLSL
        }
    }
}
