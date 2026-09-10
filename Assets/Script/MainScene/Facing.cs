using UnityEngine;
public class Facing : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform root;
    public void UpdateFacing(Vector3 moveDir, Vector3 cameraRight, Vector3 cameraForwardFlat)
    {
        // 양수면 오른쪽, 음수면 왼쪽
        float sideDot = Vector3.Dot(moveDir, cameraRight);
        // 양수면 앞, 음수면 뒤
        float frontDot = Vector3.Dot(moveDir, -cameraForwardFlat);

        bool isFront = frontDot > Mathf.Abs(sideDot);

        animator.SetBool("IsFront", isFront);

        if (!isFront)
        {
            Flip(sideDot > 0);
        }
    }
    private void Flip(bool isRight)
    {
        Vector3 scale = root.localScale;
        scale.x = isRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        root.localScale = scale;
    }
}
