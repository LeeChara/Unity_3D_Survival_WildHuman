# 지형 청크 시스템 설계

초록 Plain(단색 Plane)을 실제 지형지물이 있는 큰 맵으로 교체하기 위한 설계.
청크 기반 지형 렌더링 + 스트리밍 + 바이옴별 Props/Monster 스폰을 다룬다.

## 1. 바닥 텍스처링

- 현재: `Assets/Material/Plane.mat`은 URP Lit, 텍스처 없이 단색(`_BaseColor` 초록)만 적용된 상태
- 개선안:
  - 시임리스(seamless) 텍스처를 `_BaseMap`에 연결
  - Texture Import: `Filter Mode = Point`, `Wrap Mode = Repeat` (픽셀아트 스타일 유지)
  - `_BaseColor`는 흰색(1,1,1,1)으로 리셋 (텍스처에 초록 틴트가 덧씌워지는 것 방지)
- 여러 바이옴이 만나는 경계는 부드러운 그라데이션 대신 **디더 패턴/임계값(threshold) 블렌드**로 처리해 픽셀아트 스타일을 유지한다.

## 2. 맵 구조 — 청크 기반

- Quad 1개 = 청크 1개
- 맵 크기는 **고정**, 청크마다 바이옴(숲/사막 등)이 하나씩 지정됨
- 같은 바이옴끼리 인접한 경계는 블렌드 불필요, 다른 바이옴끼리 인접한 경계만 블렌드 대상

## 3. 바이옴 배치 생성 (고정 크기 + 절차적 생성)

- 게임 시작 시(또는 빌드 타임) 전체 바이옴 그리드를 **1회** 계산해서 캐싱
  - `Mathf.PerlinNoise(x * scale + seedOffset, z * scale + seedOffset)` 로 청크 좌표별 노이즈 값(0~1) 계산
  - 노이즈 값의 임계값 구간으로 바이옴 결정 (예: 0.0~0.4 = 숲, 0.4~0.7 = 평원, 0.7~1.0 = 사막)
  - 노이즈가 공간적으로 부드럽게 변하기 때문에 비슷한 값의 청크가 자연스럽게 뭉쳐서 하나의 지역을 이룸
- `Seed`, `NoiseScale` 등은 Inspector에서 조절 가능한 별도 설정 에셋으로 분리 — 여러 시드를 테스트해보고 마음에 드는 배치 하나를 골라 고정하는 워크플로우 지원
- 확장 여지: 지금은 노이즈 값 하나로 구간을 나누는 단순한 방식. 나중에 더 자연스러운 분포가 필요하면 온도/습도 두 채널을 조합하는 방식(Whittaker 다이어그램류)으로 확장 가능
- 맵 가장자리 청크는 한쪽 이웃이 없는데, 이 경우 그냥 블렌드하지 않는 것으로 처리 (로드 범위 밖이라 어차피 안 보임)

### `BiomeData` (ScriptableObject)

`Assets/Script/MainScene/Monster/MonsterData.cs` → `RoseviperData.cs` 같은 기존 데이터 에셋 패턴을 그대로 따른다. `Assets/Data/BiomeData/Forest.asset`, `Desert.asset` 식으로 관리.

필드:
- 바닥 텍스처, Tiling 크기
- `propSpawnTable`: 이 바이옴에서 스폰 가능한 Props 목록 + 확률/밀도
- `monsterSpawnTable`: 이 바이옴에서 스폰 가능한 몬스터 목록 + 확률 + `maxCountPerChunk`
- 블렌드 폭 (경계에서 몇 유닛까지 섞을지)
- 노이즈 임계값 범위

## 4. 렌더링 — 공용 바이옴 머티리얼

모든 청크가 **동일한 머티리얼/셰이더 하나**를 공유하는 구조 (SRP 배칭 유지, 바이옴이 늘어나도 새 머티리얼/셰이더 불필요).

- 바이옴별 바닥 텍스처는 `Texture2DArray` 하나에 순서대로 저장, 인덱스로 슬라이스 선택
- 청크별로 달라지는 값(내 바이옴 인덱스, 4방향 이웃 바이옴 인덱스)은 `MaterialPropertyBlock`으로 렌더러에 개별 주입

```csharp
var mpb = new MaterialPropertyBlock();
mpb.SetFloat("_MyBiome", myBiomeIndex);
mpb.SetVector("_NeighborBiomes", new Vector4(north, east, south, west));
meshRenderer.SetPropertyBlock(mpb);
```

셰이더 로직:
1. 프래그먼트가 청크 가장자리에 얼마나 가까운지 계산 (로컬 UV 또는 월드 좌표 기준 거리)
2. 그 방향 이웃 바이옴이 다르면, 거리를 블렌드 폭으로 나눈 값을 디더 패턴에 넣어 픽셀 단위로 두 텍스처 중 하나를 선택
3. 이웃이 같은 바이옴이면 블렌드 계산 스킵

바닥 텍스처 Tiling UV와 블렌드 마스크용 좌표는 **청크 로컬 UV가 아니라 월드 좌표 기반**으로 계산해야 청크 경계에서 반복 패턴과 블렌드가 모두 자연스럽게 이어진다.

미해결 이슈: 청크 모서리(코너)에서 3~4개 바이옴이 만나는 경우는 1차로 "가장 가까운 축 하나만" 블렌드하는 단순한 방식으로 시작. 실제로 어색해 보이면 코너 전용 처리 추가.

## 5. 청크 스트리밍

- 플레이어 월드 좌표 → 청크 좌표 변환: `Vector2Int(floor(x / chunkSize), floor(z / chunkSize))`
- 로드 반경 R(청크 단위) 내를 사각형(체비쇼프 거리) 판정으로 활성 범위 결정
- 매 프레임 무거운 연산 없이, **플레이어 청크 좌표가 바뀔 때만** 로드/언로드 트리거
- 오브젝트 풀링: 맵이 고정 크기라 필요한 청크 개수 상한이 정해져 있음 → `Instantiate`/`Destroy` 대신 풀에서 꺼내 재배치
- 바이옴 그리드가 사전 계산돼 있어 청크 로드는 "조회 + 재배치" 수준으로 가벼움
- Props/Monster 배치 연산이 무거워지면, 코루틴으로 프레임당 1~2개 청크씩 분산 로드하는 것을 고려

## 6. Props / Monster 스폰

| | Props | Monster |
|---|---|---|
| 스폰 테이블 | `BiomeData.propSpawnTable` | `BiomeData.monsterSpawnTable` |
| 위치 결정 | 청크 좌표 기반 **결정론적 시드** (재입장해도 동일 위치) | **비결정론적** 랜덤 (파밍 방지 목적) |
| 개체 수 제한 | 밀도 기반 | `maxCountPerChunk` |
| 청크 언로드 시 | 그대로 유지 (재로드 시 시드로 동일하게 재현) | despawn + 재스폰 쿨다운 |
| AI 연산 | 해당 없음 | 청크 로드 범위보다 **좁은 별도 거리**로 on/off 토글 |

- Props는 `System.Random(chunkCoord.GetHashCode() ^ globalSeed)` 같은 결정론적 랜덤으로 위치를 계산해, 청크를 언로드했다 다시 로드해도 항상 같은 자리에 나오게 한다.
- Monster는 청크 로드 시마다 스폰 테이블 가중치로 새로 굴리되, 청크 언로드 시 despawn하고 일정 시간(예: 5분) 이내 재로드되면 재스폰을 스킵해 왔다갔다 파밍을 방지한다.
- 몬스터 생명주기(스폰/despawn)는 청크 이벤트에 종속시키되, "지금 이 몬스터가 AI 연산을 할지"는 청크 로드 범위와 무관한 좁은 거리 체크로 별도 토글한다. 지형은 시야 확보를 위해 멀리까지 로드해도, 화면에 안 보이는 몬스터까지 AI를 켜둘 필요는 없기 때문.

### 참고 — 기존 코드 이슈

`Assets/Script/MainScene/Monster/MonsterAI.cs`의 `Update()`(51~93행)가 거리와 무관하게 매 프레임 실행되고, 그 안에서 `Physics.OverlapSphere`(122, 127행)도 매 프레임 호출됨. 현재 런타임 스포너는 존재하지 않고 몬스터는 씬에 수동 배치돼있음. 위의 거리 기반 AI on/off 토글을 도입하면 이 문제는 자연히 해결됨.

## 구현 순서 제안

1. 바이옴 그리드 생성기 (`BiomeData` + 노이즈 임계값 기반)
2. 청크 프리팹 + 공용 블렌드 셰이더
3. `MaterialPropertyBlock` 연결 (이웃 인덱스 전달)
4. 청크 스트리밍 매니저 (풀링 + 로드 범위)
5. Props/Monster 스폰 테이블 연동
