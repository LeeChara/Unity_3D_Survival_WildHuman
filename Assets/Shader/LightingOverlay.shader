// 조명 오버레이 (Full Screen Pass Renderer Feature에서 사용)
// 픽셀마다 카메라 광선이 지면과 만나는 점을 구해, 광원과의 거리로 밝기를 계산하고 화면 색에 곱함
// 값은 LightingSystem이 전역으로 넘겨줌 (같은 계산을 LightingSystem.GetLightLevel에서도 사용)
Shader "Hidden/WildHuman/LightingOverlay"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "LightingOverlay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // LightingSetting.MaxLights와 같아야 함
            #define MAX_LIGHTS 32

            float4 _OverlayAmbient;
            float4 _OverlayLightPosRadius[MAX_LIGHTS]; // x, z, 반경, 밝기
            float4 _OverlayLightColor[MAX_LIGHTS];
            int _OverlayLightCount;
            float _OverlaySteps;

            float _OverlayGroundHeight;
            // 게임 카메라 화면 네 모서리의 near·far 평면 위 점 (LightingSystem이 넘겨줌)
            // 시작점과 끝점을 따로 받아서 원근·직교 카메라 모두에서 맞게 계산
            float3 _OverlayNearBL, _OverlayNearBR, _OverlayNearTL, _OverlayNearTR;
            float3 _OverlayFarBL, _OverlayFarBR, _OverlayFarTL, _OverlayFarTR;
            // LightingSystem이 있는 씬의 게임 카메라에서만 1 (다른 씬·씬 뷰는 그대로 통과)
            float _OverlayEnabled;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_OverlayEnabled < 0.5) return scene;

                // 모서리 점을 보간해 이 픽셀의 광선을 구하고, 지면과 만나는 점을 계산
                float3 origin = lerp(lerp(_OverlayNearBL, _OverlayNearBR, uv.x),
                                     lerp(_OverlayNearTL, _OverlayNearTR, uv.x), uv.y);
                float3 ray = lerp(lerp(_OverlayFarBL, _OverlayFarBR, uv.x),
                                  lerp(_OverlayFarTL, _OverlayFarTR, uv.x), uv.y) - origin;
                float t = (_OverlayGroundHeight - origin.y) / min(ray.y, -1e-4);
                float2 ground = origin.xz + ray.xz * max(t, 0);

                float3 lights = 0;
                for (int i = 0; i < _OverlayLightCount; i++)
                {
                    float4 l = _OverlayLightPosRadius[i];
                    float f = 1 - saturate(distance(ground, l.xy) / max(l.z, 1e-4));
                    lights += _OverlayLightColor[i].rgb * (f * f * (3 - 2 * f)) * l.w;
                }

                float3 light = saturate(max(_OverlayAmbient.rgb, lights));
                if (_OverlaySteps > 0)
                    light = ceil(light * _OverlaySteps) / _OverlaySteps;

                return half4(scene.rgb * light, scene.a);
            }
            ENDHLSL
        }
    }
}
