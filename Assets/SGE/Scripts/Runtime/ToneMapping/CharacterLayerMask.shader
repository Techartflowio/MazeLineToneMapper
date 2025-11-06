Shader "Hidden/MAZELINE/PostProcess/CharacterLayerMask"
{
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        ZWrite On
        ZTest LEqual
        ColorMask R

        Pass
        {
            Name "CharacterLayerMask"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // DOTS instancing support
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Include DOTS instancing support (최신 URP 방식)
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 레이어 마스크는 R 채널에 1.0 (흰색) 값을 출력
                // 다른 채널은 사용하지 않으므로 최소한의 메모리 사용
                return half4(1.0, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}

