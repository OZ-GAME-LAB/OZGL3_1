using System;
using UnityEngine.Scripting;

[Serializable, Preserve]
public abstract class BaseData
{
    public string Id;
}

// JSON 키는 아래 필드 이름에 맞춥니다. 원본 데이터는 조회용으로 사용합니다.
public enum MonsterType
{
    Melee = 0,   // 근접
    Ranged = 1,  // 원거리
    Charge = 2,  // 돌진
    Smash = 3,   // 강타
    Boss = 4 
}

[Serializable, Preserve]
public class BaseMonsterData : BaseData
{
    public string Name;
    public string PrefabKey;
    public MonsterType MonsterType;
    public float HP;
    public float MeleeDamage;
    public float MoveSpeed;
}
[Serializable, Preserve]
public class MonsterData : BaseMonsterData
{
    public float SkillDamage;
}
[Serializable, Preserve]
public class BossMonsterData : BaseMonsterData
{  
    public float PatternTriggerHpRatio;
}

public enum PreloadAssetKind { GameObject, TextAsset, AudioClip, Sprite, Material, Texture2D }

[Serializable, Preserve]
public class PreloadData : BaseData
{
    public string Key;
    public PreloadAssetKind AssetType;
}

public enum PoolType { Monster = 0, BossMonster = 1 }

[Serializable, Preserve]
public class PoolData : BaseData
{
    public PoolType PoolType;
    public string DataId;
    public int InitialCount;
}

[Serializable, Preserve]
public class StageMonsterData : BaseData
{
    public string[] MonsterIds;
    public string BossMonsterId;
}
