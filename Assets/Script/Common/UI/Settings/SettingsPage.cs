using UnityEngine;
using UnityEngine.UI;

// 설정 화면의 탭 하나. SettingsUI가 탭 전환·위아래 내비게이션·닫기를 맡고, 각 페이지는 자기 항목의 값만 다룸
public abstract class SettingsPage : MonoBehaviour
{
    [Tooltip("위에서 아래 순서의 항목들 (키보드 위아래 이동 순서)")]
    [SerializeField] private Selectable[] rows;

    public Selectable[] Rows => rows;

    // 페이지가 보일 때마다 저장된 값으로 표시를 맞춤
    public abstract void Refresh();

    // "기본값" 버튼
    public abstract void ResetToDefault();

    // Esc를 이 페이지가 처리했으면 true (키 입력 대기 취소, 확인 창 닫기 등). false면 설정 창이 닫힘
    public virtual bool HandleBack() => false;

    // 탭을 떠나거나 설정 창이 닫힐 때 진행 중인 작업 정리
    public virtual void OnHide() { }
}
