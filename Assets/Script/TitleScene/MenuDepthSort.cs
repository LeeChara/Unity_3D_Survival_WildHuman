using System.Collections.Generic;
using UnityEngine;

// 자식들을 y 위치 순으로 정렬해 화면 아래쪽(앞쪽)에 있는 것이 위에 그려지게 함 (UI는 형제 순서가 그리기 순서)
public class MenuDepthSort : MonoBehaviour
{
    private readonly List<RectTransform> children = new();

    private void LateUpdate()
    {
        children.Clear();
        foreach (Transform child in transform)
        {
            children.Add((RectTransform)child);
        }

        // y가 높을수록(화면 위쪽, 멀리 있을수록) 먼저 그림
        children.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].GetSiblingIndex() != i) children[i].SetSiblingIndex(i);
        }
    }
}
