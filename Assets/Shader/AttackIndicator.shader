// 보스 공격 범위 표시 (AttackIndicator가 바닥에 눕힌 사각형 스프라이트에 사용)
// UV로 원·부채꼴·도넛(극좌표형)과 직선(사각형)을 그리고, 안쪽이 차오르는 진행도를 함께 표시
// 도트 그림에 맞춰 월드 기준 _PixelSize 격자로 끊어서 그림
// 색(바탕·차오름·테두리)과 전체 진하기(_Alpha, 사라질 때 사용)는 렌더러마다 MaterialPropertyBlock으로 넘김
Shader "WildHuman/AttackIndicator"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // 그림자와 같은 이유로 깊이 비교 없이 지면 위에 그림 (가장 낮은 정렬 순서라 세워진 스프라이트보다 먼저 그려짐)
        ZTest Always
        Cull Off

        Pass
        {
            Name "AttackIndicator"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 렌더러마다 MaterialPropertyBlock으로 넘김
            float _Shape;       // 0: 극좌표형(원·부채꼴·도넛), 1: 직선(사각형)
            float2 _Size;       // 표시 크기 (m, 가로·세로)
            float _Inner;       // 극좌표형: 안쪽 반지름 비율 (0이면 꽉 찬 원)
            float _HalfAngle;   // 극좌표형: 부채꼴 반각 (도, 180이면 전방위)
            float _Progress;    // 차오른 정도 (0~1)
            float _PixelSize;   // 도트 한 칸 크기 (m)
            float4 _AreaColor;  // 범위 바탕
            float4 _FillColor;  // 차오른 부분
            float4 _EdgeColor;  // 테두리
            float _Alpha;       // 전체 진하기 (0~1)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 표시 안의 위치 (m). 도트 격자의 칸 중심으로 끊음
                float2 local = (input.uv - 0.5) * _Size;
                float pixel = max(_PixelSize, 0.001);
                local = (floor(local / pixel) + 0.5) * pixel;

                float inside;
                float filled;
                float edge;

                if (_Shape < 0.5)
                {
                    // 극좌표형: 중심이 원점, 위쪽(+Y)이 정면
                    float outer = _Size.x * 0.5;
                    float inner = outer * _Inner;
                    float r = length(local);
                    float angle = degrees(atan2(abs(local.x), local.y));

                    inside = step(r, outer) * step(inner, r) * step(angle, _HalfAngle);
                    // 안쪽 반지름에서 바깥으로 차오름
                    filled = step(r, lerp(inner, outer, _Progress));
                    // 테두리: 바깥·안쪽 원, 부채꼴 옆변에서 한 칸
                    float sideDistance = _HalfAngle < 179.0 ? r * sin(radians(max(0.0, _HalfAngle - angle))) : 1e5;
                    edge = step(outer - r, pixel) + step(r - inner, pixel) * step(0.001, inner) + step(sideDistance, pixel);
                }
                else
                {
                    // 직선: 아래쪽 끝(-Y)이 시작점, 위쪽으로 차오름
                    float2 halfSize = _Size * 0.5;
                    inside = step(abs(local.x), halfSize.x) * step(abs(local.y), halfSize.y);
                    filled = step(local.y + halfSize.y, _Size.y * _Progress);
                    edge = step(halfSize.x - abs(local.x), pixel) + step(halfSize.y - abs(local.y), pixel);
                }

                clip(inside - 0.5);

                half4 color = lerp(_AreaColor, _FillColor, filled);
                color = lerp(color, _EdgeColor, saturate(edge));
                color.a *= _Alpha;
                return color;
            }
            ENDHLSL
        }
    }
}
