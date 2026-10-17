using System.Collections.Generic;

// 이펙트 이름별 한글 설명과 분류 이름 (이펙트 관리 창·정리 메뉴용)
// 분류는 Assets/Data/Effect 아래 폴더 이름. 새 이펙트를 만들면 여기에도 한 줄 추가 (없으면 설명 없이 표시됨)
public static class EffectDescriptions
{
    public static readonly Dictionary<string, string> Categories = new()
    {
        ["Gather"] = "채집",
        ["Monster"] = "몬스터",
        ["Fire"] = "불·폭발",
        ["Water"] = "물",
        ["Player"] = "플레이어",
        ["World"] = "월드·설치물",
    };

    // 이름 → (분류 폴더, 설명)
    public static readonly Dictionary<string, (string category, string text)> Names = new()
    {
        ["TreeHit"] = ("Gather", "나무를 때릴 때: 나무 조각 + 잎사귀"),
        ["TreeDestroy"] = ("Gather", "나무가 쓰러질 때: 조각·잎 + 먼지"),
        ["WoodHit"] = ("Gather", "그루터기를 때릴 때: 나무 조각"),
        ["WoodDestroy"] = ("Gather", "그루터기가 부서질 때: 조각 + 먼지"),
        ["BushHit"] = ("Gather", "덤불을 때릴 때: 잎사귀"),
        ["BushDestroy"] = ("Gather", "덤불이 부서질 때: 잎 + 잔가지"),
        ["StoneHit"] = ("Gather", "바위를 때릴 때: 돌 파편"),
        ["StoneDestroy"] = ("Gather", "바위가 부서질 때: 파편 + 먼지"),
        ["OreHit"] = ("Gather", "광석을 때릴 때: 광석색 파편 + 불티"),
        ["OreDestroy"] = ("Gather", "광석이 부서질 때: 파편 + 불티 + 먼지"),
        ["CactusHit"] = ("Gather", "선인장을 때릴 때: 파편 + 가시"),
        ["CactusDestroy"] = ("Gather", "선인장이 부서질 때: 파편 + 가시 + 먼지"),

        ["MonsterHit"] = ("Monster", "몬스터 피격: 타격 별"),
        ["MonsterDeath"] = ("Monster", "시체가 사라질 때: 흰 연기"),

        ["Embers"] = ("Fire", "타는 동안 떠오르는 불씨"),
        ["Extinguish"] = ("Fire", "불이 꺼질 때: 회색 연기"),
        ["Explosion"] = ("Fire", "화약통 폭발: 파편 + 연기 + 불티"),
        ["CampfireEmbers"] = ("Fire", "모닥불 불씨 (반복)"),

        ["WaterSplash"] = ("Water", "물을 끼얹었을 때 튀는 물방울"),
        ["Steam"] = ("Water", "젖은 것에 불이 닿아 마를 때: 김"),
        ["WaterEnter"] = ("Water", "물에 들어가는 순간: 첨벙"),
        ["WaterStep"] = ("Water", "물속 걸음: 작은 물방울"),

        ["SandStep"] = ("Player", "발걸음: 사막 모래 먼지"),
        ["SnowStep"] = ("Player", "발걸음: 설원 눈가루"),
        ["AshStep"] = ("Player", "발걸음: 화산 재"),
        ["IceStep"] = ("Player", "발걸음: 빙판 얼음 가루"),
        ["PickupSparkle"] = ("Player", "아이템 획득: 몸 주변 반짝 (따라감)"),
        ["EatSparkle"] = ("Player", "음식 먹기: 회복 반짝이 (따라감)"),

        ["QuakeDust"] = ("World", "지진으로 자원이 솟을 때: 흙먼지"),
        ["SmelterSmoke"] = ("World", "제련소 가동 중 굴뚝 연기 (반복)"),
    };

    public static string Describe(string name) => Names.TryGetValue(name, out var entry) ? entry.text : string.Empty;

    public static string CategoryOf(string name) => Names.TryGetValue(name, out var entry) ? entry.category : null;

    public static string CategoryLabel(string category) => Categories.TryGetValue(category, out string label) ? label : category;
}
