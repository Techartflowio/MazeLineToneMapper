Shader "Hidden/MAZELINE/HDRP/ToneMapping"
{
	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "HDRenderPipeline" }
		LOD 100

		ZWrite Off
		Cull Off
		ZTest Always

		// Pass 0: None (빈 패스)
		Pass
		{
			Name "None"

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			#pragma vertex Vert
			#pragma fragment frag

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			float4 frag(Varyings input) : SV_Target
			{
				// None 타입일 때는 원본 이미지를 그대로 반환
				return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
			}
			ENDHLSL
		}

		// Pass 1: 통합된 톤매핑 패스 (키워드 기반 분기)
		Pass
		{
			Name "Unified ToneMapping"

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesGlobal.hlsl"

			#pragma vertex Vert
			#pragma fragment frag

			#pragma multi_compile __ TM_FILMIC TM_NEUTRAL TM_GT TM_AGX

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			// 공통 파라미터
			float _Exposure;
			float _TonemapAGXGamma;
			float _TonemapAGXGammaPivot;

			static const float e = 2.71828;

			// Character Layer Mask 관련
			TEXTURE2D(_CharacterLayerMask);
			SAMPLER(sampler_CharacterLayerMask);
			float _LayerMaskApplyWeight;

			// FX Layer Mask 관련
			TEXTURE2D(_FXLayerMask);
			SAMPLER(sampler_FXLayerMask);
			float _FXLayerMaskApplyWeight;

			// Filmic ToneMapping 함수들
			float3 F(float3 x) // Uncharted 2 톤매핑 함수
			{
				const float A = 0.22f;
				const float B = 0.30f;
				const float C = 0.10f;
				const float D = 0.20f;
				const float E = 0.01f;
				const float F_constant = 0.30f;

				return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F_constant)) - E / F_constant;
			}

			float3 Uncharted2ToneMapping(float3 color, float adapted_lum)
			{
				const float WHITE = 11.2f;
				return F(1.6f * adapted_lum * color) / F(WHITE);
			}

			// Neutral PBR ToneMapping 함수
			float3 PBRNeutralToneMapping(float3 color)
			{
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

			// GranTurismo ToneMapping 함수들
			float W_f(float x, float e0, float e1)
			{
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				float a = (x - e0) / (e1 - e0);
				return a * a * (3 - 2 * a);
			}

			float H_f(float x, float e0, float e1)
			{
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				return (x - e0) / (e1 - e0);
			}

			float GranTurismoTonemapper(float x)
			{
				float P = 1;
				float a = 1;
				float m = 0.22;
				float l = 0.4;
				float c = 1.33;
				float b = 0;
				float l0 = (P - m) * l / a;
				float L0 = m - m / a;
				float L1 = m + (1 - m) / a;
				float L_x = m + a * (x - m);
				float T_x = m * pow(x / m, c) + b;
				float S0 = m + l0;
				float S1 = m + a * l0;
				float C2 = a * P / (P - S1);
				float S_x = P - (P - S1) * pow(e, -(C2 * (x - S0) / P));
				float w0_x = 1 - W_f(x, 0, m);
				float w2_x = H_f(x, m + l0, m + l0);
				float w1_x = 1 - w0_x - w2_x;
				float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
				return f_x;
			}

			// AGX ToneMapping 함수들
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

			float3 agx(float3 val)
			{
				const float min_ev = -12.47393f;
				const float max_ev = 4.026069f;

				val = mul(agx_mat, val);
				val = clamp(log2(val), min_ev, max_ev);
				val = (val - min_ev) / (max_ev - min_ev);
				val = agxDefaultContrastApprox(val);

				return val;
			}

			float3 agxEotf(float3 val)
			{
				val = mul(agx_mat_inv, val);
				return val;
			}

			float3 agxGamutCompress(float3 c)
			{
				float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
				float peak = max(max(c.r, c.g), c.b);
				float hi = saturate(smoothstep(0.8, 1.0, peak));
				float3 gray = luma.xxx;
				float strength = 0.15 * hi;
				c = lerp(c, gray, strength);
				const float k = 0.2;
				c = c / (1.0 + k * c);
				return c;
			}

			float agxPrepareLGGScalarExponent(float gammaUI)
			{
				float s = gammaUI;
				float lin = SRGBToLinear(s.xxx).x;
				float delta = lin - 1.0;
				float denom = max(1.0 + 0.8 * delta, 1e-3);
				return rcp(denom);
			}

			float3 agxApplyArtisticGammaPivoted(float3 c, float gammaUI, float pivot)
			{
				pivot = clamp(pivot, 1e-4, 1.0);
				float exp = agxPrepareLGGScalarExponent(gammaUI);
				c = max(c, 1e-6);
				return pow(c / pivot, exp) * pivot;
			}

			float3 AGXFitted(float3 value)
			{
				value = agx(value);
				value = agxEotf(value);
				value = agxApplyArtisticGammaPivoted(value, _TonemapAGXGamma, _TonemapAGXGammaPivot);
				value = agxGamutCompress(value);
				return value;
			}

			// 레이어 마스크 기반 톤매핑 가중치 적용 함수
			float3 ApplyCombinedLayerMaskWeight(float3 originalColor, float3 toneMappedColor, float2 uv)
			{
				// Character 레이어 마스크 값 샘플링
				float characterMaskValue = SAMPLE_TEXTURE2D(_CharacterLayerMask, sampler_CharacterLayerMask, uv).r;

				// FX 레이어 마스크 값 샘플링
				float fxMaskValue = SAMPLE_TEXTURE2D(_FXLayerMask, sampler_FXLayerMask, uv).r;

				// 각 마스크에 대한 가중치 계산
				float characterWeight = lerp(1.0, _LayerMaskApplyWeight, characterMaskValue);
				float fxWeight = lerp(1.0, _FXLayerMaskApplyWeight, fxMaskValue);

				// 두 가중치 중 더 작은 값을 사용 (둘 다 적용된 경우 더 강한 제한 적용)
				float combinedWeight = min(characterWeight, fxWeight);

				// 최종 색상: 가중치에 따라 톤매핑 적용
				return lerp(originalColor, toneMappedColor, combinedWeight);
			}

			float4 frag(Varyings input) : SV_Target
			{
				float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
				float3 originalColor = col.rgb;
				float3 toneMappedColor = float3(0, 0, 0);

				// 키워드에 따라 톤매핑 함수 선택
				#ifdef TM_FILMIC
					toneMappedColor = Uncharted2ToneMapping(originalColor, _Exposure);
				#elif defined(TM_NEUTRAL)
					toneMappedColor = PBRNeutralToneMapping(originalColor * _Exposure);
				#elif defined(TM_GT)
					float3 exposedColor = originalColor * _Exposure;
					float r = GranTurismoTonemapper(exposedColor.r);
					float g = GranTurismoTonemapper(exposedColor.g);
					float b = GranTurismoTonemapper(exposedColor.b);
					toneMappedColor = float3(r, g, b);
				#elif defined(TM_AGX)
					toneMappedColor = AGXFitted(originalColor);
				#else
					// 키워드가 없으면 원본 반환
					toneMappedColor = originalColor;
				#endif

				// 레이어 마스크 기반 가중치 적용
				float3 finalColor = ApplyCombinedLayerMaskWeight(originalColor, toneMappedColor, input.texcoord);

				return float4(finalColor, col.a);
			}
			ENDHLSL
		}
	}

	Fallback Off
}
