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
            output.texcoord = GetNormalizedFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        // Input texture
        TEXTURE2D_X(_InputTexture);
        SAMPLER(sampler_InputTexture);
        
        // Layer masks
        TEXTURE2D_X(_CharacterLayerMask);
        SAMPLER(sampler_CharacterLayerMask);
        TEXTURE2D_X(_FXLayerMask);
        SAMPLER(sampler_FXLayerMask);
        
        // Parameters
        float _Exposure;
        float _LayerMaskApplyWeight;
        float _FXLayerMaskApplyWeight;

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

        float3 ApplyLayerMasks(float3 original, float3 tonemapped, float2 uv)
        {
            float characterMask = SAMPLE_TEXTURE2D_X(_CharacterLayerMask, sampler_CharacterLayerMask, uv).r;
            float fxMask = SAMPLE_TEXTURE2D_X(_FXLayerMask, sampler_FXLayerMask, uv).r;
            
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
            
            float4 FragToneMap(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.texcoord;
                float3 originalColor = SAMPLE_TEXTURE2D_X(_InputTexture, sampler_InputTexture, uv).rgb;
                float3 toneMappedColor = GTToneMapping(originalColor);
                float3 finalColor = ApplyLayerMasks(originalColor, toneMappedColor, uv);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
