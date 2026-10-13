using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 세이브 파일에 그대로 들어가는 데이터 (Newtonsoft JSON으로 직렬화)
// Vector3 같은 Unity 타입은 불필요한 프로퍼티까지 직렬화되므로 기본 타입만 사용
// 공개 프로퍼티도 직렬화 대상이 되므로 계산 값은 메서드로 제공

// 월드 목록 화면에서 읽는 요약 정보 (meta.json)
[Serializable]
public class WorldMeta
{
    // 세이브 폴더 이름과 같음
    public string id;
    public string name;
    // UTC Ticks
    public long createdAt;
    public long lastPlayedAt;
    // 누적 플레이 시간(초)
    public float playTime;
    public int dayCount = 1;
    public int saveVersion;
    // 이 월드를 만든 생성 로직 버전 (WorldGenConfig.genVersion)
    public int genVersion;
    public WorldGenSettings settings = new();
}

// 맵 설정 화면에서 고른 값
[Serializable]
public class WorldGenSettings
{
    public int seed;
    // WorldGenConfig.mapSizes의 id
    public string mapSize;
    // WorldGenConfig.difficulties의 id (아직 게임에 반영되지 않음)
    public string difficulty;
}

// 실제 월드 상태 (world.json)
[Serializable]
public class WorldSaveData
{
    public int saveVersion;
    public MapData map;
    // 새 월드면 null (씬에 배치된 기본 상태로 시작)
    public EntityRecord player;
    // 관리 시스템별 개체 목록 (몬스터, 설치물, 바닥 아이템 등)
    public Dictionary<string, List<EntityRecord>> entities = new();
    // 개체가 아닌 월드 상태 (시간 등)
    public Dictionary<string, JToken> systems = new();

    public List<EntityRecord> GetEntities(string key)
    {
        return entities.TryGetValue(key, out var list) ? list : new List<EntityRecord>();
    }

    public JToken GetSystem(string key)
    {
        return systems.TryGetValue(key, out JToken token) ? token : null;
    }
}

// 개체 하나 (DST의 {prefab, x, z, data}와 같은 구조)
// data는 컴포넌트별 칸 (health, inventory 등) — 각 컴포넌트가 ISaveable로 자기 칸을 채우고 읽음
[Serializable]
public class EntityRecord
{
    public string type;
    public float x;
    public float y;
    public float z;
    public float rotY;
    public Dictionary<string, JToken> data = new();

    public Vector3 Position() => new(x, y, z);
}

// 경계 안 맵 전체 (월드 생성 시 한 번 만들고, 이후에는 이 데이터를 수정·저장)
// 종류는 이름표(palette)의 번호로 저장해서 에셋 순서가 바뀌어도 세이브가 깨지지 않음
[Serializable]
public class MapData
{
    // 경계 안 청크 수 (청크 좌표는 원점 중심: -width/2 ~ width-width/2-1)
    public int width;
    public int height;

    public float spawnX;
    public float spawnZ;

    // BiomeData.id 목록
    public List<string> biomePalette = new();
    // PropData.id 목록
    public List<string> propPalette = new();
    // WorldStructureData.id 목록
    public List<string> structurePalette = new();

    // 인덱스 = (x + width/2) + (z + height/2) * width
    public ChunkData[] chunks;

    // 월드 생성 때 정해진 구조물 (경계 안만, 구조물이 생기기 전에 만든 월드는 비어 있음)
    public List<StructureSite> structures = new();

    public static MapData Create(int width, int height)
    {
        var map = new MapData { width = width, height = height, chunks = new ChunkData[width * height] };
        for (int i = 0; i < map.chunks.Length; i++)
        {
            map.chunks[i] = new ChunkData();
        }
        return map;
    }

    public Vector2Int MinCoord() => new(-width / 2, -height / 2);

    public bool Contains(Vector2Int coord)
    {
        int x = coord.x + width / 2;
        int z = coord.y + height / 2;
        return x >= 0 && x < width && z >= 0 && z < height;
    }

    public bool TryGetChunk(Vector2Int coord, out ChunkData chunk)
    {
        if (!Contains(coord))
        {
            chunk = null;
            return false;
        }

        chunk = chunks[(coord.x + width / 2) + (coord.y + height / 2) * width];
        return true;
    }

    public Vector2Int IndexToCoord(int index)
    {
        Vector2Int min = MinCoord();
        return new Vector2Int(min.x + index % width, min.y + index / width);
    }

    // 백그라운드 저장용 복사본 (쓰는 동안 자원이 파괴되어도 저장 데이터가 바뀌지 않도록)
    public MapData Clone()
    {
        var copy = new MapData
        {
            width = width,
            height = height,
            spawnX = spawnX,
            spawnZ = spawnZ,
            biomePalette = new List<string>(biomePalette),
            propPalette = new List<string>(propPalette),
            structurePalette = new List<string>(structurePalette),
            chunks = new ChunkData[chunks.Length],
            structures = new List<StructureSite>(structures.Count),
        };

        for (int i = 0; i < chunks.Length; i++)
        {
            copy.chunks[i] = chunks[i].Clone();
        }
        foreach (var site in structures)
        {
            copy.structures.Add(site.Clone());
        }
        return copy;
    }
}

// 월드 구조물 하나 (종류·위치·회전과, 부품 상태)
// 배치는 항상 프리팹에서 다시 만들고, state에는 바뀐 상태(상자 내용물, 사라진 부품, 퍼즐 진행 등)만 둠
[Serializable]
public class StructureSite
{
    // structurePalette 번호
    public int type;
    public float x;
    public float z;
    public float rotY;
    // WorldStructure.Capture 결과 (한 번도 로드되지 않았으면 null)
    public JObject state;

    public Vector3 Position() => new(x, 0f, z);

    public StructureSite Clone()
    {
        return new StructureSite { type = type, x = x, z = z, rotY = rotY, state = (JObject)state?.DeepClone() };
    }
}

// 청크 하나의 내용
// 자원은 상태가 "존재 여부와 위치"뿐이라 개체 기록 대신 같은 길이의 배열 3개로 압축 저장
// 시간이 지나면 다른 종류로 바뀌는 자원(빈 덤불, 밑동)과 다른 위치에 다시 생길 자원은 따로 짧은 목록으로 저장
[Serializable]
public class ChunkData
{
    // biomePalette 번호 (-1 = 없음)
    public int biome = -1;
    // propPalette 번호
    public List<int> types = new();
    public List<float> px = new();
    public List<float> pz = new();

    // 바뀔 자원의 번호(types 기준)와 바뀌는 월드 시간. 대부분의 자원은 바뀌지 않으므로 해당하는 것만 저장
    public List<int> growIndex = new();
    public List<float> growAt = new();

    // 다른 위치에 다시 생길 자원의 종류(propPalette 번호)와 생길 월드 시간
    public List<int> regenTypes = new();
    public List<float> regenAt = new();

    // 불타던 자원의 번호(types 기준)·체력·남은 연소 시간
    // 청크가 언로드되거나 저장될 때만 기록 (로드된 동안은 자원 오브젝트가 상태를 가짐). 언로드된 동안은 타지 않고 멈춤
    public List<int> burnIndex = new();
    public List<float> burnHp = new();
    public List<float> burnLeft = new();

    public int PropCount() => types.Count;

    public int AddProp(int type, float x, float z)
    {
        types.Add(type);
        px.Add(x);
        pz.Add(z);
        return types.Count - 1;
    }

    // 뒤 번호들이 한 칸씩 당겨지므로 바뀔 자원 번호도 맞춰 당김
    public void RemovePropAt(int index)
    {
        types.RemoveAt(index);
        px.RemoveAt(index);
        pz.RemoveAt(index);

        for (int i = growIndex.Count - 1; i >= 0; i--)
        {
            if (growIndex[i] == index) RemoveGrowAt(i);
            else if (growIndex[i] > index) growIndex[i]--;
        }
        for (int i = burnIndex.Count - 1; i >= 0; i--)
        {
            if (burnIndex[i] == index) RemoveBurnAt(i);
            else if (burnIndex[i] > index) burnIndex[i]--;
        }
    }

    public Vector3 PropPosition(int index) => new(px[index], 0f, pz[index]);

    #region 바뀌는 자원

    // time이 0 이하면 바뀌지 않음으로 지움
    public void SetGrowTime(int index, float time)
    {
        int slot = growIndex.IndexOf(index);
        if (time <= 0f)
        {
            if (slot >= 0) RemoveGrowAt(slot);
            return;
        }

        if (slot >= 0) growAt[slot] = time;
        else
        {
            growIndex.Add(index);
            growAt.Add(time);
        }
    }

    // 지금(now)까지 바뀔 시간이 된 자원 번호
    public List<int> DueGrowths(float now)
    {
        var due = new List<int>();
        for (int i = 0; i < growIndex.Count; i++)
        {
            if (growAt[i] <= now) due.Add(growIndex[i]);
        }
        return due;
    }

    private void RemoveGrowAt(int slot)
    {
        growIndex.RemoveAt(slot);
        growAt.RemoveAt(slot);
    }

    #endregion

    #region 불타는 자원

    public void SetBurn(int index, float hp, float left)
    {
        int slot = burnIndex.IndexOf(index);
        if (slot >= 0)
        {
            burnHp[slot] = hp;
            burnLeft[slot] = left;
            return;
        }

        burnIndex.Add(index);
        burnHp.Add(hp);
        burnLeft.Add(left);
    }

    // 기록이 있으면 꺼내고 지움
    public bool TakeBurn(int index, out float hp, out float left)
    {
        int slot = burnIndex.IndexOf(index);
        if (slot < 0)
        {
            hp = left = 0f;
            return false;
        }

        hp = burnHp[slot];
        left = burnLeft[slot];
        RemoveBurnAt(slot);
        return true;
    }

    private void RemoveBurnAt(int slot)
    {
        burnIndex.RemoveAt(slot);
        burnHp.RemoveAt(slot);
        burnLeft.RemoveAt(slot);
    }

    #endregion

    #region 다른 위치에 다시 생길 자원

    public void AddRegen(int type, float time)
    {
        regenTypes.Add(type);
        regenAt.Add(time);
    }

    public int RegenCount() => regenTypes.Count;

    public void RemoveRegenAt(int index)
    {
        regenTypes.RemoveAt(index);
        regenAt.RemoveAt(index);
    }

    #endregion

    public ChunkData Clone()
    {
        return new ChunkData
        {
            biome = biome,
            types = new List<int>(types),
            px = new List<float>(px),
            pz = new List<float>(pz),
            growIndex = new List<int>(growIndex),
            growAt = new List<float>(growAt),
            regenTypes = new List<int>(regenTypes),
            regenAt = new List<float>(regenAt),
            burnIndex = new List<int>(burnIndex),
            burnHp = new List<float>(burnHp),
            burnLeft = new List<float>(burnLeft),
        };
    }
}
