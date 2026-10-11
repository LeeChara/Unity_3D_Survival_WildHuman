// 구름 그림자 공용 함수 (LightingOverlay·GroundShadow에서 사용)
// 값은 ShadowSystem이 전역으로 넘겨줌. 같은 계산을 ShadowSystem.GetCloudShade에서도 사용
#ifndef WILDHUMAN_CLOUD_SHADOW_INCLUDED
#define WILDHUMAN_CLOUD_SHADOW_INCLUDED

// ShadowSystem.CloudSecondScale과 같아야 함 (두 번째 층은 더 잘게 반복해서 모양이 계속 바뀌게 함)
#define CLOUD_SECOND_SCALE 1.7
#define CLOUD_FIRST_WEIGHT 0.65

TEXTURE2D(_CloudTex);
SAMPLER(sampler_CloudTex);
float4 _CloudOffsets; // xy: 1층 UV 이동량, zw: 2층 UV 이동량
float4 _CloudParams;  // x: 1 / 무늬 한 장 크기(m), y: 덮는 비율, z: 가장자리 부드러움, w: 지면 어둡기

// 월드 XZ 위치의 구름 정도 (0 = 맑음, 1 = 구름 아래). 덮는 비율이 0이면 항상 0
float CloudCover(float2 xz)
{
    if (_CloudParams.y <= 0) return 0;

    float2 uv = xz * _CloudParams.x;
    // CPU 조회(GetPixelBilinear)와 같은 값이 나오도록 밉맵 없이 0단계에서 읽음
    float n = SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, uv + _CloudOffsets.xy, 0).r * CLOUD_FIRST_WEIGHT
            + SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, uv * CLOUD_SECOND_SCALE + _CloudOffsets.zw, 0).r * (1 - CLOUD_FIRST_WEIGHT);

    float edge = 1 - _CloudParams.y;
    return smoothstep(edge - _CloudParams.z, edge + _CloudParams.z, n);
}

#endif
