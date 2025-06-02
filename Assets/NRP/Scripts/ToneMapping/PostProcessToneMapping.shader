Shader "Hidden/NTRANCE/PostProcess/ToneMapping"
{
	SubShader
    {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always

        

        // Pass 0: None (빈 패스)
        Pass
        {
            Name "None"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            half4 frag (Varyings input) : SV_Target
            {
                // None 타입일 때는 원본 이미지를 그대로 반환
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            }
            ENDHLSL
        }

        // Pass 1: Filmic ToneMapping
        // Uncharted 2 기반의 영화적 톤매핑
        // 높은 대비와 풍부한 색상을 제공
        Pass
        {
            Name "Filmic ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "NRPCommon.hlsl"
            
            static const float e = 2.71828;

			float3 reinhard_jodie(float3 v)
			{
			    float l = Luminance(v);
			    float3 tv = v / (1.0f + v);
			    return lerp(v / (1.0f + l), tv, tv);
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

           float3 F(float3 x) // Uncharted 2 톤매핑 함수
			{
				const float A = 0.22f;
				const float B = 0.30f;
				const float C = 0.10f;
				const float D = 0.20f;
				const float E = 0.01f;
				const float F_constant = 0.30f; // F 상수 이름 변경
			 
				return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F_constant)) - E / F_constant;
			}

			float3 Uncharted2ToneMapping(float3 color, float adapted_lum)
			{
				const float WHITE = 11.2f;
				return F(1.6f * adapted_lum * color) / F(WHITE);
			}

            float _IgnoreCharacterPixels;
			float _Exposure;
            float _ToneMapLowerBound;
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(LinearEyeDepth(characterDepth, _ZBufferParams), ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
            	
				half4 toneMappedCol = half4(Uncharted2ToneMapping(col.rgb, _Exposure),col.a);
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return lerp(col, toneMappedCol, pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}
            	
                return toneMappedCol;
            }
            ENDHLSL
        }

		// Pass 2: Neutral PBR ToneMapping
		// PBR 중립적 톤매핑
		// 물리 기반 렌더링에 최적화된 중립적인 톤매핑
		Pass
        {
            Name "Neutral PBR ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "NRPCommon.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 PBRNeutralToneMapping( float3 color ) {
			  const float startCompression = 0.8 - 0.04;
			  const float desaturation = 0.15;

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

            float _IgnoreCharacterPixels;
			float _Exposure;
            float _ToneMapLowerBound;
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(LinearEyeDepth(characterDepth, _ZBufferParams), ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
            	
				half4 toneMappedCol = half4(PBRNeutralToneMapping(col.rgb * _Exposure), col.a);
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return lerp(col, toneMappedCol, pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}
            	
                return toneMappedCol;
            }
            ENDHLSL
        }

		// Pass 3: GranTurismo ToneMapping
        // GranTurismo 스타일 톤매핑
        // GT 스포츠 시리즈에서 사용되는 톤매핑 커브
        Pass
        {
            Name "GT ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "NRPCommon.hlsl"
            
            static const float e = 2.71828;

			float W_f(float x,float e0,float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				float a = (x - e0) / (e1 - e0);
				return a * a*(3 - 2 * a);
			}
			float H_f(float x, float e0, float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				return (x - e0) / (e1 - e0);
			}

			float GranTurismoTonemapper(float x) {
				float P = 1;
				float a = 1;
				float m = 0.22;
				float l = 0.4;
				float c = 1.33;
				float b = 0;
				float l0 = (P - m)*l / a;
				float L0 = m - m / a;
				float L1 = m + (1 - m) / a;
				float L_x = m + a * (x - m);
				float T_x = m * pow(x / m, c) + b;
				float S0 = m + l0;
				float S1 = m + a * l0;
				float C2 = a * P / (P - S1);
				float S_x = P - (P - S1)*pow(e,-(C2*(x-S0)/P));
				float w0_x = 1 - W_f(x, 0, m);
				float w2_x = H_f(x, m + l0, m + l0);
				float w1_x = 1 - w0_x - w2_x;
				float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
				return f_x;
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _IgnoreCharacterPixels;
            float _Exposure;
            float _ToneMapLowerBound;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
                col.rgb *= _Exposure; // 노출 적용
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(sceneDepth, characterDepth);
            	
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return lerp(col, half4(GranTurismoTonemapper(col.r),GranTurismoTonemapper(col.g),GranTurismoTonemapper(col.b), col.a), pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}

                float r = GranTurismoTonemapper(col.r);
				float g = GranTurismoTonemapper(col.g);
				float b = GranTurismoTonemapper(col.b);
				half4 toneMappedCol = half4(r,g,b,col.a);
                return toneMappedCol;
            }
            ENDHLSL
        }

        // Pass 4: AGX ToneMapping
        // AGX 톤매핑
        // 소니의 AGX 톤매핑 커브를 구현
        Pass
        {
            Name "AGX ToneMapping"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "NRPCommon.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            float _Exposure;
            float _IgnoreCharacterPixels;
            float _ToneMapLowerBound;
            float _TonemapAGXGamma;

			static const float3x3 agx_mat = float3x3(
				0.842479062253094, 0.0784335999999992, 0.0792237451477643,
				0.0423282422610123, 0.878468636469772, 0.0791661274605434,
				0.0423756549057051, 0.0784336, 0.879142973793104);

			static const float3x3 agx_mat_inv = float3x3(
				1.19687900512017, -0.0980208811401368, -0.0990297440797205,
				-0.0528968517574562, 1.15190312990417, -0.0989611768448433,
				-0.0529716355144438, -0.0980434501171241, 1.15107367264116);

			// Mean error^2: 3.6705141e-06
			float3 agxDefaultContrastApprox(float3 x) {
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

			float3 agx(float3 val) {
				
				const float min_ev = -12.47393f;
				const float max_ev = 4.026069f;

				// Input transform (inset)
				val = mul(agx_mat, val);
			
				// Log2 space encoding
				val = clamp(log2(val), min_ev, max_ev);
				val = (val - min_ev) / (max_ev - min_ev);
			
				// Apply sigmoid function approximation
				val = agxDefaultContrastApprox(val);

				return val;
			}

			float3 agxEotf(float3 val) 
			{
			
				// Inverse input transform (outset)
				val = mul(agx_mat_inv, val);
			
				// sRGB IEC 61966-2-1 2.2 Exponent Reference EOTF Display
				// NOTE: We're linearizing the output here. Comment/adjust when
				// *not* using a sRGB render target
				val = pow(val, _TonemapAGXGamma);

				return val;
			}

			float3 AGXFitted(float3 value) 
			{
				value = agx(value);
				value = agxEotf(value);
				return value;
			}

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
                
                float characterDepth = SampleCharacterSceneDepth(input.texcoord);
                float sceneDepth = SampleSceneDepth(input.texcoord);
                float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * 
                    step(LinearEyeDepth(characterDepth, _ZBufferParams), 
                         ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
                
                // 노출 적용
                float3 exposedColor = col.rgb * _Exposure;
                
                // AGX 톤매핑 적용
                float3 toneMappedColor = AGXFitted( exposedColor);
                
                // 캐릭터 픽셀 처리
                if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
                {
                    float lerpFactor = pow(saturate(_ToneMapLowerBound * 1.2), 0.5);
                    toneMappedColor = lerp(col.rgb, toneMappedColor, lerpFactor);
                }
                
                return half4(toneMappedColor, col.a);
            }
            ENDHLSL
        }
    }
}