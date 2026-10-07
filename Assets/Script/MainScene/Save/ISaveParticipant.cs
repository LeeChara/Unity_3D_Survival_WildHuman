// 개체 목록이나 월드 전체 상태를 관리하는 시스템의 저장 참여 (몬스터 스포너, 설치 관리자, 시간 등)
// 씬에 있으면 WorldSaveManager가 찾아서 호출하므로 따로 등록할 필요 없음
public interface ISaveParticipant
{
    void Capture(WorldSaveData data);
    void Restore(WorldSaveData data);
}
