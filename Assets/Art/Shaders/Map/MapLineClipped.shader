Shader "ShellGame/Map/MapLineClipped"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _EmissionStrength("Emission Strength", Range(0, 5)) = 1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _EmissionStrength;
            CBUFFER_END

            float4x4 _MapClipWorldToLocal;
            float3 _MapClipBoxCenter;
            float3 _MapClipBoxExtents;
            float _MapClipBoxFade;
            float _MapClipEnabled;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;

                return output;
            }

            float CalculateBoxFade(float3 worldPos)
            {
                if (_MapClipEnabled < 0.5)
                    return 1.0;

                float3 localPos = mul(_MapClipWorldToLocal, float4(worldPos, 1.0)).xyz - _MapClipBoxCenter;
                float3 d = abs(localPos) - _MapClipBoxExtents;
                float maxDist = max(d.x, max(d.y, d.z));

                if (maxDist > 0.0)
                    return 0.0;

                float fadeMargin = max(_MapClipBoxFade, 0.001);
                float edgeDist = -maxDist;
                return saturate(edgeDist / fadeMargin);
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float fade = CalculateBoxFade(input.positionWS);
                if (fade <= 0.001)
                    discard;

                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 finalColor = texColor * _BaseColor * input.color;
                finalColor.rgb *= _EmissionStrength;
                finalColor.a *= fade;

                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
