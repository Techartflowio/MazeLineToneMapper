Shader "Hidden/HDRP/SGE/ToneMapping"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        
        HLSLINCLUDE
        #pragma target 4.5
        #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
        
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
        
        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        // Input texture
        TEXTURE2D_X(_InputTexture);
        SAMPLER(sampler_InputTexture);
        
        // Layer masks
        TEXTURE2D(_CharacterLayerMask);
        SAMPLER(sampler_CharacterLayerMask);
        TEXTURE2D(_FXLayerMask);
        SAMPLER(sampler_FXLayerMask);
        
        // Parameters
        float _Exposure;
        float _TonemapAGXGamma;
        float _TonemapAGXGammaPivot;
        float _LayerMaskApplyWeight;
        float _FXLayerMaskApplyWeight;

        // Tone mapping functions
        float3 FilmicToneMapping(float3 color)
        {
            const float A = 0.22f;
            const float B = 0.30f;
            const float C = 0.10f;
            const float D = 0.20f;
            const float E = 0.01f;
            const float F_constant = 0.30f;
            const float WHITE = 11.2f;
            
            float3 x = color * _Exposure * 1.6f;
            float3 curr = ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F_constant)) - E / F_constant;
            
            float3 whiteScale = 1.0f / (((WHITE * (A * WHITE + C * B) + D * E) / (WHITE * (A * WHITE + B) + D * F_constant)) - E / F_constant);
            return curr * whiteScale;
        }

        float3 NeutralToneMapping(float3 color)
        {
            const float startCompression = 0.8 - 0.04;
            const float desaturation = 0.15;
            
            color *= _Exposure;
            
            float x = min(color.r, min(color.g, color.b));
            float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
            color -= offset;

            float peak = max(color.r, max(color.g, color.b));
            if (peak < startCompression) return color;

            const float d = 1. - startCompression;
            float newPeak = 1. - d * d / (peak + d - startCompression);
            color *= newPeak / peak;

            float g = 1. - 1. / (desaturation * (peak - newPeak) + 1.);
            return lerp(color, newPeak * float3(1, 1, 1), g);
        }

        float GranTurismoTonemapper(float x)
        {
            const float P = 1;
            const float a = 1;
            const float m = 0.22;
            const float l = 0.4;
            const float c = 1.33;
            const float b = 0;
            
            float l0 = (P - m) * l / a;
            float L0 = m - m / a;
            float L1 = m + (1 - m) / a;
            float L_x = m + a * (x - m);
            float T_x = m * pow(x / m, c) + b;
            float S0 = m + l0;
            float S1 = m + a * l0;
            float C2 = a * P / (P - S1);
            float S_x = P - (P - S1) * exp(-(C2 * (x - S0) / P));
            
            float w0 = 1 - smoothstep(0, m, x);
            float w2 = step(m + l0, x);
            float w1 = 1 - w0 - w2;
            
            return T_x * w0 + L_x * w1 + S_x * w2;
        }

        float3 GTToneMapping(float3 color)
        {
            float3 c = color * _Exposure;
            c.r = GranTurismoTonemapper(c.r);
            c.g = GranTurismoTonemapper(c.g);
            c.b = GranTurismoTonemapper(c.b);
            return c;
        }

        // AGX Tone Mapping
        static const float3x3 agx_mat = float3x3(
            0.842479062253094, 0.0784335999999992, 0.0792237451477643,
            0.0423282422610123, 0.878468636469772, 0.0791661274605434,
            0.0423756549057051, 0.0784336, 0.879142973793104);

        static const float3x3 agx_mat_inv = float3x3(
            1.19687900512017, -0.0980208811401368, -0.0990297440797205,
            -0.0528968517574562, 1.15190312990417, -0.0989611768448433,
            -0.0529716355144438, -0.0980434501171241, 1.15107367264116);

        float3 agxDefaultContrastApprox(float3 x)
        {
            float3 x2 = x * x;
            float3 x4 = x2 * x2;
            
            return 15.5 * x4 * x2
                - 40.14 * x4 * x
                + 31.96 * x4
                - 6.868 * x2 * x
                + 0.4298 * x2
                + 0.1191 * x
                - 0.00232;
        }

        float3 AGXToneMapping(float3 color)
        {
            const float min_ev = -12.47393f;
            const float max_ev = 4.026069f;

            float3 val = mul(agx_mat, color);
            val = clamp(log2(val), min_ev, max_ev);
            val = (val - min_ev) / (max_ev - min_ev);
            val = agxDefaultContrastApprox(val);
            val = mul(agx_mat_inv, val);
            
            // Apply artistic gamma
            float pivot = clamp(_TonemapAGXGammaPivot, 1e-4, 1.0);
            float gamma = _TonemapAGXGamma;
            float s = gamma;
            float lin = saturate(s);
            float delta = lin - 1.0;
            float denom = max(1.0 + 0.8 * delta, 1e-3);
            float exp = 1.0 / denom;
            
            val = max(val, 1e-6);
            val = pow(val / pivot, exp) * pivot;
            
            // Gamut compress
            float luma = dot(val, float3(0.2126, 0.7152, 0.0722));
            float peak = max(max(val.r, val.g), val.b);
            float hi = saturate(smoothstep(0.8, 1.0, peak));
            float strength = 0.15 * hi;
            val = lerp(val, luma.xxx, strength);
            const float k = 0.2;
            val = val / (1.0 + k * val);
            
            return val;
        }

        float3 ApplyLayerMasks(float3 original, float3 tonemapped, float2 uv)
        {
            float characterMask = SAMPLE_TEXTURE2D(_CharacterLayerMask, sampler_CharacterLayerMask, uv).r;
            float fxMask = SAMPLE_TEXTURE2D(_FXLayerMask, sampler_FXLayerMask, uv).r;
            
            float characterWeight = lerp(1.0, _LayerMaskApplyWeight, characterMask);
            float fxWeight = lerp(1.0, _FXLayerMaskApplyWeight, fxMask);
            float combinedWeight = min(characterWeight, fxWeight);
            
            return lerp(original, tonemapped, combinedWeight);
        }
        
        ENDHLSL

        // Pass 0: None (passthrough)
        Pass
        {
            Name "None"
            
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNone

            float4 FragNone(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 color = SAMPLE_TEXTURE2D_X(_InputTexture, sampler_InputTexture, input.texcoord).rgb;
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        // Pass 1: Unified Tone Mapping
        Pass
        {
            Name "Tone Mapping"
            
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragToneMap
            
            #pragma multi_compile _ TM_FILMIC TM_NEUTRAL TM_GT TM_AGX

            float4 FragToneMap(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float3 color = SAMPLE_TEXTURE2D_X(_InputTexture, sampler_InputTexture, input.texcoord).rgb;
                float3 originalColor = color;
                float3 toneMappedColor = color;

                #ifdef TM_FILMIC
                    toneMappedColor = FilmicToneMapping(originalColor);
                #elif defined(TM_NEUTRAL)
                    toneMappedColor = NeutralToneMapping(originalColor);
                #elif defined(TM_GT)
                    toneMappedColor = GTToneMapping(originalColor);
                #elif defined(TM_AGX)
                    toneMappedColor = AGXToneMapping(originalColor);
                #endif

                float3 finalColor = ApplyLayerMasks(originalColor, toneMappedColor, input.texcoord);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
