Shader "PostEffect/Dithering"
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
            Name "Dithering"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // Единственная зависимость. Даёт _ScreenParams, sampler_PointClamp
            // (через GlobalSamplers.hlsl), макросы SAMPLE_TEXTURE2D_X и
            // GetFullScreenTriangle* — то есть всё, что нужно ниже.
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

            uint _PatternIndex;
            float _DitherThreshold;
            float _DitherStrength;
            float _DitherScale;

            // Нормализованные матрицы Байера 4x4 (значения 0..0.9375, то есть /16).
            // PS1 использовал упорядоченный дизеринг по матрице 4x4 — именно он
            // давал характерные «полосатые» градиенты вместо плавных.
            // Раньше здесь была шахматка и матрица с отрицательными значениями,
            // которые не являются дизерингом и били по кадру грубыми блоками.
            float4x4 GetDitherPattern(uint index)
            {
                // 0 — классический Байер 4x4
                if (index == 0) return float4x4(
                     0.0000, 0.5000, 0.1250, 0.6250,
                     0.7500, 0.2500, 0.8750, 0.3750,
                     0.1875, 0.6875, 0.0625, 0.5625,
                     0.9375, 0.4375, 0.8125, 0.3125);
                // 1 — инвертированный Байер (светлит, а не затемняет)
                if (index == 1) return float4x4(
                     1.0000, 0.5000, 0.8750, 0.3750,
                     0.2500, 0.7500, 0.1250, 0.6250,
                     0.8125, 0.3125, 0.9375, 0.4375,
                     0.0625, 0.5625, 0.1875, 0.6875);
                // 2 — шахматка 2x2 (legacy-вид из старой версии пака)
                if (index == 2) return float4x4(
                     0.0, 1.0, 0.0, 1.0,
                     1.0, 0.0, 1.0, 0.0,
                     0.0, 1.0, 0.0, 1.0,
                     1.0, 0.0, 1.0, 0.0);
                // 3 — эффект выключен (единицы в матрице = пиксель всегда белый)
                return float4x4(
                     1.0, 1.0, 1.0, 1.0,
                     1.0, 1.0, 1.0, 1.0,
                     1.0, 1.0, 1.0, 1.0,
                     1.0, 1.0, 1.0, 1.0);
            }

            float Get4x4TexValue(float2 uv, float brightness, float4x4 pattern)
            {
                uint x = (uint)fmod(uv.x, 4);
                uint y = (uint)fmod(uv.y, 4);

                // _DitherThreshold — множитель яркости, 1.0 = нейтрально
                // (дизерится только чистый белый, картинка не темнеет).
                // Раньше здесь стоял bias = (threshold - 1.0) * 0.25, из-за чего
                // дефолт пака 512 давал bias 127 и эффект был полным no-op.
                float threshold = max(_DitherThreshold, 0.0);
                if ((brightness * threshold) < pattern[x][y]) return 0.0;
                return 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;

                // Point-семплинг обязателен: эффект пиксельный, билинейный сэмпл
                // размывает результат предыдущих эффектов.
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float2 screenPos = uv * _ScreenParams.xy;
                float scale = max(_DitherScale, 1.0);
                uint2 ditherCoordinate = (uint2)(screenPos / scale);

                float brightness = (color.r + color.g + color.b) / 3.0;

                float4x4 ditherPattern = GetDitherPattern(_PatternIndex);
                float ditherPixel = Get4x4TexValue((float2)ditherCoordinate, brightness, ditherPattern);

                // Клампим strength в [0,1], чтобы lerp никогда не давал отрицательный/экстраполированный множитель
                float strength = saturate(_DitherStrength);
                return color * lerp(1.0, ditherPixel, strength);
            }
            ENDHLSL
        }
    }
}
