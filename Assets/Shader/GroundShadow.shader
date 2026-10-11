// 바닥 그림자 (ShadowCaster가 바닥에 눕힌 사각형 스프라이트에 사용)
// UV로 타원을 그리고, 색·알파는 SpriteRenderer.color로 받음
// 해 그림자 머티리얼(_UseSunAlpha = 1)은 시간대 진하기 전역값 _ShadowSunAlpha를 함께 곱함
// 스텐실로 그림자끼리 겹친 곳은 한 번만 어두워지게 함 (먼저 그린 그림자가 차지)
Shader "WildHuman/GroundShadow"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _EdgeSoftness ("Edge Softness", Range(0, 0.5)) = 0.05
        [ToggleUI] _UseSunAlpha ("Use Sun Alpha (해 그림자만 켬)", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // 자원 스프라이트는 아랫부분이 지면 아래로 묻혀 있어 발밑 높이가 지면보다 낮을 수 있음
        // Shadow 정렬 레이어로 항상 세워진 스프라이트보다 먼저 그려지므로 깊이 비교 없이 지면 위에 그림
        ZTest Always
        Cull Off

        // 다른 기능과 겹치지 않도록 스텐실의 한 비트(64)만 사용
        Stencil
        {
            Ref 64
            ReadMask 64
            WriteMask 64
            Comp NotEqual
            Pass Replace
        }

        Pass
        {
            Name "GroundShadow"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _EdgeSoftness;
                float _UseSunAlpha;
            CBUFFER_END

            // 해 그림자 진하기 (ShadowSystem이 시간대에 맞춰 넘겨줌, 밤이면 0)
            float _ShadowSunAlpha;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // URP는 SpriteRenderer.color를 정점 색이 아닌 unity_SpriteColor로 넘김
                output.color = input.color * unity_SpriteColor;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 중심 0 → 테두리 1
                float d = length(input.uv * 2.0 - 1.0);
                float mask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                // 해 그림자는 시간대 진하기를 곱하고, 조명 그림자는 정점 색 알파만 사용
                // 구름 그림자가 생기면 해 그림자 쪽에 구름 값을 함께 곱함
                float alpha = input.color.a * mask * lerp(1.0, _ShadowSunAlpha, _UseSunAlpha);
                // 타원 밖이나 완전히 투명한 그림자(밤의 해 그림자 등)는 스텐실을 차지하지 않도록 버림
                clip(alpha - 0.001);

                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
