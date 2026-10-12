using UnityEngine;

public enum BufferedAction { None, Attack, Dodge }

// 선입력: 행동 중에 누른 공격·회피를 잠깐 기억해 뒀다가, 실행할 수 있게 되면 각 행동 스크립트가 꺼내서 실행
// 한 칸만 저장하므로 마지막으로 누른 입력만 남음
public class PlayerInputBuffer : MonoBehaviour
{
    [SerializeField] private PlayerCombatSetting setting;

    private BufferedAction action;
    private float pushedTime;

    public void Push(BufferedAction newAction)
    {
        action = newAction;
        pushedTime = Time.time;
    }

    // 유효 시간 안에 저장된 입력이 이 행동인지
    public bool Has(BufferedAction expected)
    {
        return action == expected && Time.time <= pushedTime + setting.inputBufferDuration;
    }

    // 실행했거나 더 이상 유효하지 않은 입력을 비움
    public void Clear()
    {
        action = BufferedAction.None;
    }
}
