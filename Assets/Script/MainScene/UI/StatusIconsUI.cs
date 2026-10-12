using System.Collections.Generic;
using UnityEngine;

// 체력 바 위에 플레이어가 걸린 상태 이상(화상 등)을 아이콘으로 나열
// 아이콘 아래 막대가 남은 시간만큼 줄어들고, 마우스를 올리면 이름·남은 시간·설명을 표시
public class StatusIconsUI : MonoBehaviour
{
    [SerializeField] private StatusEffectHost host;
    [Tooltip("복제해서 쓸 아이콘 칸 (처음에는 꺼 둠)")]
    [SerializeField] private StatusIconSlotUI slotTemplate;

    private readonly List<StatusIconSlotUI> slots = new();

    private void Awake()
    {
        slotTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        host.Changed += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        host.Changed -= Rebuild;
    }

    private void Update()
    {
        int count = Mathf.Min(host.ActiveCount, slots.Count);
        for (int i = 0; i < count; i++)
        {
            slots[i].SetRemaining(host.ActiveRemaining(i));
        }
    }

    private void Rebuild()
    {
        int count = host.ActiveCount;
        while (slots.Count < count)
        {
            slots.Add(Instantiate(slotTemplate, slotTemplate.transform.parent));
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < count;
            slots[i].gameObject.SetActive(used);
            if (used) slots[i].Set(host.ActiveData(i), host.ActiveRemaining(i));
        }
    }
}
