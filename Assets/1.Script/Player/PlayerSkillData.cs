using UnityEngine;
public enum SkillDamageType
{
    Physical, // 힘 기반
    Magic     // 마력 기반
}

[CreateAssetMenu(menuName = "ProjectM/Player Skill Data", fileName = "PlayerSkill_")]
public class PlayerSkillData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string _id;          // 예: sword_wave (저장·장착 식별용, 중복 금지)
    [SerializeField] private string _displayName; // 예: 검기
    [SerializeField, TextArea(2, 4)] private string _description;
    [SerializeField] private Sprite _icon;

    [Header("Balance")]
    [SerializeField] private SkillDamageType _damageType;
    [SerializeField, Min(0)] private int _price = 500;
    [SerializeField, Min(0f)] private float _damageCoefficient = 1.8f;
    [SerializeField, Min(0f)] private float _cooldown = 4f;
    [SerializeField, Min(0f)] private float _castDuration = 0.4f; // 스킬 상태 유지 시간

    // 게임 중에 코드로 SO 값을 바꾸면 에디터에서 원본 에셋이 바뀌어 버리므로 막아둠
    public string Id { get { return _id; } }
    public string DisplayName { get { return _displayName; } }
    public string Description { get { return _description; } }
    public Sprite Icon { get { return _icon; } }
    public SkillDamageType DamageType { get { return _damageType; } }
    public int Price { get { return _price; } }
    public float DamageCoefficient { get { return _damageCoefficient; } }
    public float Cooldown { get { return _cooldown; } }
    public float CastDuration { get { return _castDuration; } }
}
