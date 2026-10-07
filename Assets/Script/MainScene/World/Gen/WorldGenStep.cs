using UnityEngine;

// 월드 생성 단계 하나 (바이옴, 자원 배치, 시작 지점, 이후 구조물 등)
// 새 콘텐츠는 이 클래스를 상속한 단계 에셋을 만들어 WorldGenConfig.steps에 추가
public abstract class WorldGenStep : ScriptableObject
{
    [Tooltip("이 단계 난수를 구분하는 키 (비우면 에셋 이름). 바꾸면 같은 시드라도 결과가 달라짐")]
    [SerializeField] private string key;

    public string Key => string.IsNullOrEmpty(key) ? name : key;

    // 청크 하나를 채움
    // 경계 안은 월드 생성 시 전부 호출되고, 경계 밖은 청크를 불러올 때마다 호출됨 (저장 안 함)
    // 같은 시드·좌표면 항상 같은 결과가 나오도록 ctx.ChunkRandom만 사용
    public virtual void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk) { }

    // 모든 청크를 채운 뒤 맵 전체를 보고 처리 (경계 안 생성 시에만 호출)
    public virtual void GenerateMap(WorldGenContext ctx) { }
}
