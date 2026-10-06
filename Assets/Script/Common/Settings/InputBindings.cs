using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 바꾼 키 설정을 저장하고, 사용 중인 모든 입력 에셋에 적용
// PlayerAction을 new로 만들거나 PlayerInput을 쓰는 곳마다 에셋이 따로 생기므로, 각자 Register 해 두면 바뀔 때 함께 갱신됨
public static class InputBindings
{
    private const string Key = "Options.InputBindings";

    private static readonly List<InputActionAsset> assets = new();

    private static string SavedJson => PlayerPrefs.GetString(Key, string.Empty);

    // 등록 시 저장된 키 설정을 바로 적용
    public static void Register(InputActionAsset asset)
    {
        if (asset == null || assets.Contains(asset)) return;

        assets.Add(asset);
        Apply(asset, SavedJson);
    }

    public static void Unregister(InputActionAsset asset)
    {
        assets.Remove(asset);
    }

    // 설정 화면에서 바꾼 에셋의 오버라이드를 저장하고 나머지 에셋에도 반영
    public static void Save(InputActionAsset source)
    {
        string json = source.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        ApplyToAll(json);
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        ApplyToAll(string.Empty);
    }

    private static void ApplyToAll(string json)
    {
        // 씬 전환 등으로 파괴된 에셋은 정리
        assets.RemoveAll(asset => asset == null);
        foreach (var asset in assets)
        {
            Apply(asset, json);
        }
    }

    private static void Apply(InputActionAsset asset, string json)
    {
        asset.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
    }
}
