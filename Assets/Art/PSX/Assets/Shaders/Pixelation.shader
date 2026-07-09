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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _WidthPixelation;
            float _HeightPixelation;
            float _ColorPrecision;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Защита от деления на 0 и от значений < 1 (иначе весь экран схлопнется в один пиксель)
                float width = max(_WidthPixelation, 1.0);
                float height = max(_HeightPixelation, 1.0);

                uv.x = floor(uv.x * width) / width;
                uv.y = floor(uv.y * height) / height;

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Защита от деления на 0 для точности цвета
                float precision = max(_ColorPrecision, 1.0);
                color = floor(color * precision) / precision;

                return color;
            }
            ENDHLSL
        }
    }
}