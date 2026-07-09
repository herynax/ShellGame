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

            uint _PatternIndex;
            float _DitherThreshold;
            float _DitherStrength;
            float _DitherScale;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4x4 GetDitherPattern(uint index)
            {
                if(index == 0) return float4x4(0,1,0,1, 1,0,1,0, 0,1,0,1, 1,0,1,0);
                if(index == 1) return float4x4(0.23,0.2,0.6,0.2, 0.2,0.43,0.2,0.77, 0.88,0.2,0.87,0.2, 0.2,0.46,0.2,0);
                if(index == 2) return float4x4(-4,0,-3,1, 2,-2,3,-1, -3,1,-4,0, 3,-1,2,-2);
                if(index == 3) return float4x4(1,0,0,1, 0,1,1,0, 0,1,1,0, 1,0,0,1);
                return float4x4(1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1);
            }

            float Get4x4TexValue(float2 uv, float brightness, float4x4 pattern)
            {
                uint x = (uint)fmod(uv.x, 4);
                uint y = (uint)fmod(uv.y, 4);

                // ВАЖНО: threshold теперь работает как смещение (bias), а не множитель,
                // чтобы дефолтные значения вроде 4 не "убивали" эффект целиком.
                float bias = (_DitherThreshold - 1.0) * 0.25; // при threshold=1 bias=0 (нейтрально)
                if((brightness + bias) < pattern[x][y]) return 0.0;
                return 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 screenPos = input.uv * _ScreenParams.xy;
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