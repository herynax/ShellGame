Shader "ShellGame/Map/MapNodeClipped"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Color("Color (Legacy)", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength("Emission Strength", Range(0, 5)) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _LightingStrength("Lighting Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _Color;
                float4 _EmissionColor;
                float _EmissionStrength;
                float _Cutoff;
                float _LightingStrength;
            CBUFFER_END

            // Global Map Clipping Box Parameters
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
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
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

            void ApplyBoxClip(float3 worldPos)
            {
                if (_MapClipEnabled > 0.5)
                {
                    float3 localPos = mul(_MapClipWorldToLocal, float4(worldPos, 1.0)).xyz - _MapClipBoxCenter;
                    float3 d = abs(localPos) - _MapClipBoxExtents;
                    float maxDist = max(d.x, max(d.y, d.z));
                    if (maxDist > 0.0)
                    {
                        discard;
                    }
                }
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float fade = CalculateBoxFade(input.positionWS);
                if (fade <= 0.001)
                    discard;

                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 tint = _BaseColor * _Color * input.color;
                float4 albedo = texColor * tint;
                albedo.a *= fade;

                // Lighting
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                float3 ambient = SampleSH(input.normalWS);
                float3 diffuse = lerp(float3(1, 1, 1), (mainLight.color * NdotL + ambient), _LightingStrength);

                float3 emission = _EmissionColor.rgb * _EmissionStrength;
                float3 finalRgb = albedo.rgb * diffuse + emission;

                return float4(finalRgb, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4x4 _MapClipWorldToLocal;
            float3 _MapClipBoxCenter;
            float3 _MapClipBoxExtents;
            float _MapClipBoxFade;
            float _MapClipEnabled;

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                output.positionWS = positionWS;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float fade = CalculateBoxFade(input.positionWS);
                if (fade <= 0.001)
                    discard;
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
