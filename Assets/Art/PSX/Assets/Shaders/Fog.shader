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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // В URP нужно явно подключать библиотеку для работы с картой глубины
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Правильное чтение глубины в URP
                float rawDepth = SampleSceneDepth(uv);
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

                // Оригинальная логика смешивания из вашего кода
                float4 ambientColor = float4(0.1, 0.1, 0.1, 0.1);
                return lerp(color, _FogColor * ambientColor, fogMix);
            }
            ENDHLSL
        }
    }
}