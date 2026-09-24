using UnityEditor;
using UnityEngine;

// BiomeDatabase.biomes 순서 그대로 Texture2DArray를 만드는 에디터 툴.
// 슬라이스 인덱스 = BiomeDatabase 배열 순서 = 셰이더의 _MyBiome 값이 되도록
// 항상 이 순서를 그대로 따라간다.
public static class BiomeTextureArrayBuilder
{
    private const string OutputPath = "Assets/Data/TerrainData/BiomeTextureArray.asset";

    [MenuItem("Assets/Biome/Build Texture Array", true)]
    private static bool Validate()
    {
        return Selection.activeObject is BiomeDatabase;
    }

    [MenuItem("Assets/Biome/Build Texture Array")]
    private static void Build()
    {
        var database = Selection.activeObject as BiomeDatabase;
        if (database == null || database.biomes == null || database.biomes.Length == 0)
        {
            Debug.LogError("BiomeDatabase에 바이옴이 하나도 없습니다.");
            return;
        }

        var textures = new Texture2D[database.biomes.Length];
        for (int i = 0; i < database.biomes.Length; i++)
        {
            var biome = database.biomes[i];
            if (biome == null || biome.planeTexture == null)
            {
                Debug.LogError($"BiomeDatabase[{i}]에 바이옴 또는 텍스처가 비어있습니다.");
                return;
            }
            textures[i] = biome.planeTexture;
        }

        int width = textures[0].width;
        int height = textures[0].height;
        for (int i = 1; i < textures.Length; i++)
        {
            if (textures[i].width != width || textures[i].height != height)
            {
                Debug.LogError(
                    $"모든 바이옴 텍스처는 크기가 같아야 합니다. " +
                    $"'{textures[0].name}'({width}x{height}) vs '{textures[i].name}'({textures[i].width}x{textures[i].height})");
                return;
            }
        }

        var textureArray = new Texture2DArray(width, height, textures.Length, TextureFormat.RGBA32, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (int i = 0; i < textures.Length; i++)
        {
            textureArray.SetPixels32(textures[i].GetPixels32(), i);
        }
        textureArray.Apply();

        if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputPath) != null)
        {
            AssetDatabase.DeleteAsset(OutputPath);
        }
        AssetDatabase.CreateAsset(textureArray, OutputPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"BiomeTextureArray 생성 완료: {OutputPath} ({textures.Length}장)");
    }
}
