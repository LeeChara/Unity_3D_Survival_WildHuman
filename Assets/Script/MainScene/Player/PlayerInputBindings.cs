using UnityEngine;
using UnityEngine.InputSystem;

// PlayerInput이 쓰는 입력 에셋에 저장된 키 설정을 적용하고, 설정 화면에서 바뀌면 함께 갱신되게 등록
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputBindings : MonoBehaviour
{
    private InputActionAsset asset;

    // PlayerInput이 Awake에서 에셋을 준비하므로 Start에서 등록
    private void Start()
    {
        asset = GetComponent<PlayerInput>().actions;
        InputBindings.Register(asset);
    }

    private void OnDestroy()
    {
        InputBindings.Unregister(asset);
    }
}
