Shader "PostEffect/Fog"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Fog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // Единственная зависимость. Даёт sampler_PointClamp (через
            // GlobalSamplers.hlsl), _ZBufferParams, _ScreenParams, макросы
            // SAMPLE_TEXTURE2D_X и GetFullScreenTriangle*.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Глубина в URP: _CameraDepthTexture + SampleSceneDepth/LinearEyeDepth.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            float _FogDensity;
            float _FogDistance;
            float4 _FogColor;
            float _NoiseScale;
            float _NoiseStrength;

            // Встроенная функция генерации шума, заменяющая потерянный voronoi.cginc
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123); }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), f.x),
                    lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), f.x),
                    f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;

                // Point-семплинг обязателен: эффект пиксельный, билинейный сэмпл
                // размывает результат предыдущих эффектов.
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Правильное чтение глубины в URP
                // Защита от невалидной глубины в Scene View (может возвращать 0/NaN)
                float rawDepth = SampleSceneDepth(uv);
                if (rawDepth <= 0.0) return color;
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // Дистанция с отступом (FogDistance)
                float dist = max(linearDepth - _FogDistance, 0.0);

                // Вычисление густоты тумана
                float fogFactor = exp2(-_FogDensity * dist);
                fogFactor = saturate(fogFactor);
                float fogMix = 1.0 - fogFactor;

                // Генерация шума
                float scale = max(_NoiseScale, 0.001);
                float screenNoise = noise(uv * _ScreenParams.xy / scale);

                fogMix = saturate(fogMix + (screenNoise * _NoiseStrength));

                // Цвет тумана берётся из профиля напрямую. Раньше здесь стоял
                // хардкод lerp(color, _FogColor * 0.1, fogMix), из-за чего любой
                // _FogColor схлопывался в почти чёрный и настройка была невозможна.
                return lerp(color, _FogColor, fogMix);
            }
            ENDHLSL
        }
    }
}
