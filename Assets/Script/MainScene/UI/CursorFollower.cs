using UnityEngine;
using UnityEngine.InputSystem;

// 커서 옆에 붙어 다니는 UI(툴팁, 호버 글자)의 위치 계산
public static class CursorFollower
{
    // 기본은 커서 오른쪽 아래, 화면 밖으로 나가는 방향은 커서 반대쪽으로 뒤집음
    // Screen Space - Overlay 캔버스이므로 화면 좌표 = 월드 좌표
    // offset은 Canvas 단위 (오른쪽 아래 방향이 양수 x, 음수 y)
    public static void Place(RectTransform panel, Canvas canvas, Vector2 offset)
    {
        if (Pointer.current == null) return;

        Vector2 cursor = Pointer.current.position.ReadValue();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 size = panel.rect.size * scale;
        offset *= scale;

        bool flipX = cursor.x + offset.x + size.x > Screen.width;
        bool flipY = cursor.y + offset.y - size.y < 0f;

        panel.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        panel.position = cursor + new Vector2(flipX ? -offset.x : offset.x, flipY ? -offset.y : offset.y);
    }
}
