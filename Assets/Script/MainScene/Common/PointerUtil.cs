using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 마우스 포인터 관련 공용 계산 (UI 위 여부, 커서 아래 땅 지점)
public static class PointerUtil
{
    private static PointerEventData uiPointerData;
    private static EventSystem uiPointerDataOwner;
    private static readonly List<RaycastResult> uiRaycastResults = new();

    // 입력 콜백 안에서는 EventSystem.IsPointerOverGameObject()가 이전 프레임 기준이라 경고가 뜨므로
    // 현재 포인터 위치로 UI 레이캐스트를 직접 수행 (Raycast Target이 켜진 UI만 해당)
    public static bool IsOverUI(Vector2 screenPos)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        // 씬 재시작으로 EventSystem이 바뀌면 새로 생성
        if (uiPointerData == null || uiPointerDataOwner != eventSystem)
        {
            uiPointerData = new PointerEventData(eventSystem);
            uiPointerDataOwner = eventSystem;
        }
        uiPointerData.position = screenPos;

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(uiPointerData, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }

    // 커서 아래 땅(height 높이의 수평면) 지점
    public static bool TryGetGroundPoint(Vector2 screenPos, float height, out Vector3 point)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, height, 0f));

        if (groundPlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = default;
        return false;
    }
}
