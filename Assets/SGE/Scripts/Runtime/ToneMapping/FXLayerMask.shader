Shader "Hidden/MAZELINE/PostProcess/FXLayerMask"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        ZWrite Off
        ZTest LEqual
        ColorMask R
        Blend SrcAlpha One, Zero One

        Pass
        {
            Name "FXLayerMask"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // DOTS instancing support
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // Unlit 셰이더 지원을 위한 태그
            #pragma multi_compile_fragment _ _SURFACE_TYPE_TRANSPARENT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Include DOTS instancing support (최신 URP 방식)
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // FX 레이어 마스크는 R 채널에 Alpha 값을 출력
                // Additive 블렌딩을 사용하는 파티클/이펙트의 경우 Alpha 값이 중요
                // Vertex Color의 Alpha를 사용하여 원본 셰이더의 Alpha 정보를 반영
                // Alpha가 0인 경우 마스크에 포함되지 않도록 함
                half alpha = input.color.a;
                
                // R 채널에 Alpha 값을 출력 (Additive 블렌딩 고려)
                // Alpha가 0이면 마스크에 포함되지 않음
                return half4(alpha, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
    
    // Fallback for objects that don't support the above pass
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

