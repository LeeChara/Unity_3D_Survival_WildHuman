using UnityEditor;
using UnityEngine;

// Scene 뷰를 게임 카메라와 같은 각도로 맞추는 에디터 툴
// 빌보드 스프라이트에 그려진 기둥 발밑 등에 콜라이더를 맞출 때 게임과 같은 시점에서 보기 위해 사용
// 프리팹 편집 화면에는 Main Camera가 없으므로 각도는 고정값 사용 (MainScene의 Main Camera 회전과 일치해야 함)
public static class SceneViewGameAngle
{
    // 등각 시점: 아래로 35.26도(= atan(1/√2)), 좌우 45도
    private static readonly Quaternion GameCameraRotation = Quaternion.Euler(35.26f, 45f, 0f);

    [MenuItem("Tools/Scene View/게임 카메라 각도로 보기")]
    private static void AlignToGameAngle()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        sceneView.orthographic = false;
        sceneView.rotation = GameCameraRotation;
        sceneView.Repaint();
    }
}
