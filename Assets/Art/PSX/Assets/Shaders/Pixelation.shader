Shader "PostEffect/Pixelation"
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
            Name "Pixelation"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // Единственная зависимость. Даёт sampler_PointClamp (через
            // GlobalSamplers.hlsl), макросы SAMPLE_TEXTURE2D_X и
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

            float _WidthPixelation;
            float _HeightPixelation;
            float _ColorPrecision;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;

                // Защита от деления на 0 и от значений < 1 (иначе весь экран схлопнется в один пиксель)
                float width = max(_WidthPixelation, 1.0);
                float height = max(_HeightPixelation, 1.0);

                uv.x = floor(uv.x * width) / width;
                uv.y = floor(uv.y * height) / height;

                // Point-семплинг обязателен: эффект пиксельный, билинейный сэмпл
                // размывает результат предыдущих эффектов.
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // Защита от деления на 0 для точности цвета
                float precision = max(_ColorPrecision, 1.0);
                color = floor(color * precision) / precision;

                return color;
            }
            ENDHLSL
        }
    }
}
