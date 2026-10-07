using Newtonsoft.Json.Linq;

// 컴포넌트 하나의 저장 상태 (개체 기록의 data 안에 SaveKey 이름으로 한 칸씩 들어감)
// 같은 오브젝트의 SaveableEntity가 모아서 저장·복원함
public interface ISaveable
{
    string SaveKey { get; }
    // 저장할 것이 없으면 null
    JToken Save();
    void Load(JToken data);
}
