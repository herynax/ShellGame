Shader "ShellGame/Map/MapUnderlay"
{
    Properties
    {
        [MainColor] _BaseColor("Board Color", Color) = (0.12, 0.10, 0.08, 0.95)
        _BorderColor("Border Color", Color) = (0.28, 0.22, 0.16, 1.0)
        _CornerColor("Corner Accent Color", Color) = (0.45, 0.35, 0.22, 1.0)
        _BorderWidth("Border Width", Range(0.005, 0.1)) = 0.03
        _CornerSize("Corner Size", Range(0.01, 0.2)) = 0.08
        _Roughness("Grid / Grain Intensity", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent-10" 
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BorderColor;
                float4 _CornerColor;
                float _BorderWidth;
                float _CornerSize;
                float _Roughness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;

                return output;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float2 edge = min(uv, 1.0 - uv);
                float minEdge = min(edge.x, edge.y);

                // Noise grain
                float grain = (hash(floor(uv * 256.0)) - 0.5) * _Roughness;

                // Base board color
                float4 col = _BaseColor;
                col.rgb += grain;

                // Border
                if (minEdge < _BorderWidth)
                {
                    float t = smoothstep(0.0, _BorderWidth, minEdge);
                    col = lerp(_BorderColor, col, t);
                }

                // Corners
                if (edge.x < _CornerSize && edge.y < _CornerSize)
                {
                    if (minEdge < _BorderWidth * 1.8)
                    {
                        col = _CornerColor;
                    }
                }

                // Simple tabletop lighting
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                float3 diffuse = lerp(float3(0.8, 0.8, 0.8), mainLight.color * NdotL + SampleSH(input.normalWS), 0.4);

                col.rgb *= diffuse;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
