// 영웅 외곽선 (새 영웅 화면 점검 1번: "영웅부터 찾기 쉽게 — 얇은 밝은 외곽선").
// 뒤집은 껍질: 뒷면만, 법선 방향으로 _Width(월드 유닛)만큼 부풀려 단색으로 그린다 → 몸 가장자리에 테가 보인다.
// 영웅 모델의 각 조각에 재질 하나를 더 붙여서 쓴다 (Visual3D.SetOutline). 머리카락 카드(구멍 뚫린 재질)는 뺀다.
Shader "HistoryHeroes/HeroOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.93, 0.7, 1)
        _Width ("Width (world)", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                // 월드에서 부풀린다: 모델 배율(FBX 단위·Visual3D 배율)과 상관없이 같은 두께
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                positionWS += normalWS * _Width;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
