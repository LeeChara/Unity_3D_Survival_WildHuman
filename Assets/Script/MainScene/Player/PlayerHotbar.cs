using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 핫바 선택 칸 관리 (숫자키 1~9, 0 / 마우스 휠)
public class PlayerHotbar : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    public int SelectedIndex { get; private set; }
    public ItemStack SelectedStack => inventory.Get(SelectedIndex);

    public event Action<int> SelectionChanged;

    public void Select(int index)
    {
        index = (int)Mathf.Repeat(index, Inventory.SlotsPerRow);
        if (index == SelectedIndex) return;

        SelectedIndex = index;
        SelectionChanged?.Invoke(SelectedIndex);
    }

    // 키 1~9는 1~9, 키 0은 10으로 들어옴 (바인딩의 Scale 프로세서)
    // 키를 뗄 때 0이 들어오므로 무시
    void OnHotbarSelect(InputValue value)
    {
        int number = Mathf.RoundToInt(value.Get<float>());
        if (number <= 0) return;

        Select(number - 1);
    }

    // 휠을 아래로 굴리면 다음 칸, 위로 굴리면 이전 칸 (끝에서 반대쪽으로 순환)
    void OnHotbarScroll(InputValue value)
    {
        float scroll = value.Get<float>();
        if (Mathf.Approximately(scroll, 0f)) return;

        Select(SelectedIndex + (scroll < 0f ? 1 : -1));
    }
}
