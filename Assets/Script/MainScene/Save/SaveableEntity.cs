using Newtonsoft.Json.Linq;
using UnityEngine;

// 저장 대상 프리팹에 붙여 종류 id를 지정하고, 같은 오브젝트(자식 포함)의 ISaveable들을 개체 기록으로 묶음
// 프리팹을 찾는 데는 SaveRegistry가 이 id를 사용
[DisallowMultipleComponent]
public class SaveableEntity : MonoBehaviour
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    [SerializeField] private string typeId;

    public string TypeId => typeId;

    private ISaveable[] saveables;
    private ISaveable[] Saveables => saveables ??= GetComponentsInChildren<ISaveable>(true);

    public EntityRecord Capture()
    {
        Vector3 position = transform.position;
        var record = new EntityRecord
        {
            type = typeId,
            x = position.x,
            y = position.y,
            z = position.z,
            rotY = transform.eulerAngles.y,
        };

        foreach (var saveable in Saveables)
        {
            JToken token = saveable.Save();
            if (token != null) record.data[saveable.SaveKey] = token;
        }
        return record;
    }

    // 위치·회전은 생성하는 쪽이 정함 (Rigidbody 유무 등 개체마다 옮기는 방법이 달라서)
    public void LoadData(EntityRecord record)
    {
        foreach (var saveable in Saveables)
        {
            if (record.data.TryGetValue(saveable.SaveKey, out JToken token)) saveable.Load(token);
        }
    }
}
