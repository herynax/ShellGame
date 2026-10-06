Shader "Hidden/ShellGame/OldScreen"
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
            Name "OldScreenPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // _Time, _ScreenParams, sampler_LinearClamp (через GlobalSamplers.hlsl),
            // макросы SAMPLE_TEXTURE2D_X и GetFullScreenTriangle*.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PSXNoise.hlsl"

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

            float _Curvature;
            float _CornerRounding;
            float _VignetteIntensity;
            float _ApertureIntensity;
            float _ApertureResolution;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _InterlaceIntensity;
            float _InterlaceSpeed;
            float _RollIntensity;
            float _RollSpeed;
            float _RollWidth;
            float _BleedIntensity;
            float _NoiseIntensity;
            float _NoiseScale;

            half3 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            // --- 1. Кривизна экрана + скругление углов ---
            // Положительный _Curvature выгибает картинку наружу (barrel),
            // отрицательный — вогнуто. За пределами [0,1] отдаём чёрный:
            // это рамка трубки.
            float2 ApplyCurvature(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;

                float2 offset = abs(centered.yx) / float2(6.0, 4.0);
                centered += centered * offset * offset * _Curvature;

                // Скругление: у краёв поджимаем углы внутрь.
                float2 edge = saturate(abs(centered) - (1.0 - _CornerRounding));
                centered -= sign(centered) * edge * edge * _CornerRounding * 2.0;

                return centered * 0.5 + 0.5;
            }

            // --- 2. Виньетка с усилением в углах ---
            half3 ApplyVignette(float2 uv, half3 color)
            {
                float2 centered = uv * 2.0 - 1.0;
                float dist = length(centered * float2(1.0, 0.92));
                float falloff = smoothstep(0.55, 1.5, dist);
                return color * (1.0 - saturate(falloff * _VignetteIntensity));
            }

            // --- 3. RGB-апертура (горизонтальные триады) ---
            // Непрерывная позиция внутри триады вместо floor по колонкам —
            // иначе на дробных _ApertureResolution начинается алиасинг.
            half3 ApplyAperture(float2 uv, half3 color)
            {
                if (_ApertureIntensity <= 0.0) return color;

                float triadPos = frac(uv.x * _ApertureResolution / 3.0) * 3.0;
                half3 mask = saturate(half3(1.0, 1.0, 1.0) - abs(half3(triadPos, triadPos - 1.0, triadPos - 2.0)));

                // 0.5 + mask: активный слот 1.5, остальные 0.5 — сетка темнит
                // картинку примерно на 17%, как настоящий гриль.
                return color * lerp(half3(1.0, 1.0, 1.0), 0.5 + mask, _ApertureIntensity);
            }

            // --- 4. Сканлайны ---
            // Сглаженная синусоида, а не step: на большом _ScanlineCount step
            // рассыпается в алиасинг.
            half3 ApplyScanlines(float2 uv, half3 color)
            {
                if (_ScanlineIntensity <= 0.0) return color;

                float lines = 0.5 + 0.5 * cos(uv.y * _ScanlineCount * 3.14159265);
                return color * lerp(1.0, lines, _ScanlineIntensity);
            }

            // --- 5. Interlace: каждый кадр гасит половину строк ---
            // Строка, чья чётность совпала с фазой кадра, показывается
            // полностью; противоположная приглушается. На _InterlaceSpeed = 0
            // все строки видны сразу (фаза не меняется — эффекта нет).
            half3 ApplyInterlace(float2 uv, half3 color)
            {
                if (_InterlaceIntensity <= 0.0) return color;

                float phase = fmod(floor(_Time.y * _InterlaceSpeed), 2.0);
                float row = fmod(floor(uv.y * _ScreenParams.y), 2.0);
                return color * (1.0 - abs(row - phase) * _InterlaceIntensity);
            }

            // --- 6. Бегущая яркая полоса (CRT roll) ---
            half3 ApplyRoll(float2 uv, half3 color)
            {
                if (_RollIntensity <= 0.0) return color;

                float width = max(_RollWidth, 0.001);
                float band = frac(uv.y + _Time.y * _RollSpeed);
                float rollLine = smoothstep(0.0, width, band) * (1.0 - smoothstep(width, width * 2.0, band));
                return color * (1.0 + rollLine * _RollIntensity);
            }

            // --- 7. Свечение фосфора: размазывание ярких пикселей ---
            // Порог отсекает тени, чтобы свечение не съедало картинку.
            half3 ApplyBleed(float2 uv, half3 color)
            {
                if (_BleedIntensity <= 0.0) return color;

                float2 texel = 1.0 / _ScreenParams.xy;
                half3 sum = half3(0.0, 0.0, 0.0);
                sum += max(SampleSource(uv + float2( texel.x,  texel.y)) - 0.65, 0.0);
                sum += max(SampleSource(uv + float2(-texel.x,  texel.y)) - 0.65, 0.0);
                sum += max(SampleSource(uv + float2( texel.x, -texel.y)) - 0.65, 0.0);
                sum += max(SampleSource(uv + float2(-texel.x, -texel.y)) - 0.65, 0.0);

                return color + sum * (0.25 * _BleedIntensity);
            }

            // --- 8. Статический шум поверх всего ---
            half3 ApplyNoise(float2 uv, half3 color)
            {
                if (_NoiseIntensity <= 0.0) return color;

                float2 p = uv * _NoiseScale + float2(_Time.y * 11.0, _Time.y * -7.0);
                return color + (PSXHash21(floor(p)) - 0.5) * _NoiseIntensity;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = ApplyCurvature(input.texcoord.xy);

                half3 color = half3(0.0, 0.0, 0.0);
                if (uv.x > 0.0 && uv.x < 1.0 && uv.y > 0.0 && uv.y < 1.0)
                {
                    color = SampleSource(uv);

                    color = ApplyBleed(uv, color);
                    color = ApplyVignette(uv, color);
                    color = ApplyAperture(uv, color);
                    color = ApplyScanlines(uv, color);
                    color = ApplyInterlace(uv, color);
                    color = ApplyRoll(uv, color);
                    color = ApplyNoise(uv, color);
                }

                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
}