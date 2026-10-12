using UnityEngine;
using UnityEngine.UI;

// 게임 HUD 캔버스의 배율을 설정의 GUI 크기에 맞춤 (Constant Pixel Size 캔버스에 붙임)
[RequireComponent(typeof(CanvasScaler))]
public class HudScaler : MonoBehaviour
{
    private CanvasScaler scaler;

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
    }

    private void OnEnable()
    {
        GameOptions.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameOptions.Changed -= Apply;
    }

    private void Apply()
    {
        scaler.scaleFactor = GameOptions.HudScale;
    }
}
