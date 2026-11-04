using UnityEngine;


// 스킬 판정의 공통 부모. 스킬마다 상속해서 플레이어(또는 자식 오브젝트)에 붙인다.
public abstract class PlayerSkillBase : MonoBehaviour
{
    [SerializeField] private PlayerSkillData _data;

    public PlayerSkillData Data { get { return _data; } }

    // damage: 한 대상에 전부 맞았을 때의 총피해 (공격력 x 계수, 반올림)
    // 연타·장판 스킬은 이 값을 타수/틱 수로 나눠서 사용
    public abstract void Execute(PlayerController player, Vector3 aimPoint, float damage);
}
