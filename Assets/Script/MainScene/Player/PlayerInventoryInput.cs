using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventoryInput : MonoBehaviour
{
    [SerializeField] private InventoryUI inventoryUI;

    void OnInventory(InputValue value)
    {
        // 열려 있어도 이동·공격 등 다른 동작은 그대로 동작
        inventoryUI.Toggle();
    }
}
