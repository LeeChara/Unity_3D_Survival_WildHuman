using UnityEngine;

// 히트박스(BoxCollider) 위치 조정용 디버그 표시
// Hitbox 오브젝트는 공격 중에만 켜지므로 항상 켜져 있는 부모(AttackPivot 등)에 붙여서 사용
[ExecuteAlways]
public class HitboxVisualizer : MonoBehaviour
{
    private enum ShowMode { Always, ActiveOnly }

    [SerializeField] private BoxCollider hitbox;
    [SerializeField] private MeshRenderer visual; // 콜라이더가 없는 Cube 메시 오브젝트

    [SerializeField] private bool show = true;
    [SerializeField] private ShowMode showMode = ShowMode.Always;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 0f, 0.15f);   // 히트박스가 꺼져 있을 때
    [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.45f); // 판정이 켜져 있을 때

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock propertyBlock;

    private bool IsHitboxActive => hitbox.enabled && hitbox.gameObject.activeInHierarchy;

    // 인스펙터에서 수정한 콜라이더 값이 바로 반영되도록 매 프레임 동기화
    private void LateUpdate()
    {
        if (hitbox == null || visual == null) return;

        bool isActive = IsHitboxActive;
        bool visible = show && (showMode == ShowMode.Always || isActive);
        visual.enabled = visible;
        if (!visible) return;

        Transform hitboxTransform = hitbox.transform;
        Transform visualTransform = visual.transform;

        visualTransform.SetPositionAndRotation(hitboxTransform.TransformPoint(hitbox.center), hitboxTransform.rotation);

        // 월드 기준 크기를 부모 스케일로 나눠 로컬 스케일로 변환 (플레이어 스케일 7 반영)
        Vector3 worldSize = Vector3.Scale(hitbox.size, hitboxTransform.lossyScale);
        Vector3 parentScale = visualTransform.parent != null ? visualTransform.parent.lossyScale : Vector3.one;
        visualTransform.localScale = new Vector3(worldSize.x / parentScale.x, worldSize.y / parentScale.y, worldSize.z / parentScale.z);

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.SetColor(BaseColorId, isActive ? activeColor : idleColor);
        visual.SetPropertyBlock(propertyBlock);
    }

    // 씬 뷰에는 테두리를 함께 표시
    private void OnDrawGizmos()
    {
        if (!show || hitbox == null) return;

        Gizmos.color = IsHitboxActive ? activeColor : idleColor;
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
        Gizmos.matrix = hitbox.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(hitbox.center, hitbox.size);
    }
}
