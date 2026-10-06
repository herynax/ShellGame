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

            // Единственная зависимость. Даёт sampler_LinearClamp (через
            // GlobalSamplers.hlsl), макросы SAMPLE_TEXTURE2D_X и
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;

                float2 centerOffset = uv - 0.5;
                float distanceFromCenter = length(centerOffset);
                float2 direction = centerOffset / max(distanceFromCenter, 1e-5);

                float chromaStrength = _Intensity * (0.035 + distanceFromCenter * 0.08) * (1.0 + _Intensity * 1.25);

                float2 uvR = uv - direction * chromaStrength;
                float2 uvG = uv;
                float2 uvB = uv + direction * chromaStrength;

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
