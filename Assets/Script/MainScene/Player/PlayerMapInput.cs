using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMapInput : MonoBehaviour
{
    [SerializeField] private WorldMapUI worldMapUI;

    void OnMap(InputValue value)
    {
        // 열려 있어도 이동·공격 등 다른 동작은 그대로 동작
        worldMapUI.Toggle();
    }
}
