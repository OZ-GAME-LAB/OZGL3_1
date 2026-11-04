1스테이지 해골 캐릭터 — Rogue / Warrior / Mage

Prefabs/의 Skeleton_Rogue.prefab, Skeleton_Warrior.prefab, Skeleton_Mage.prefab은 각각 독립적으로 배치할 수 있습니다. Review/ 아래 Skeleton_Rogue_Review.unity, Skeleton_Warrior_Review.unity, Skeleton_Mage_Review.unity, Skeleton_Minion_Review.unity를 열면 동봉 모델을 비교할 수 있습니다.

캐릭터의 Generic Animator는 균일 배율(0.585289224)을 적용하는 바깥 부모가 아니라 원본 모델 자식에 있습니다. 컨트롤러는 지정되어 있지 않습니다. 동작을 재생하려면 해당 자식 Animator에 Clips/ 아래 같은 캐릭터의 클립을 사용하는 컨트롤러를 연결하세요. 원본 FBX에는 95개 동작이 있습니다. 이 패키지는 그중 다음 클립을 개별 .anim으로 제공합니다.

Rogue (6)   Idle_Combat, Running_A, 2H_Ranged_Aiming, 2H_Ranged_Shoot, 2H_Ranged_Reload, Hit_A
Warrior (5) Idle_Combat, Walking_B, 1H_Melee_Attack_Chop, 1H_Melee_Attack_Slice_Diagonal, Hit_A
Mage (5)    Idle_Combat, Walking_D_Skeletons, Spellcast_Shoot, Spellcast_Raise, Hit_A

대기·걷기·달리기는 반복입니다. Rogue의 Aiming은 팔을 내린 자세에서 조준하는 단발 동작입니다. 무기는 Rogue 오른손 석궁(로컬 Y -90°), Warrior 오른손 도끼(기본 회전), Mage 왼손 지팡이(로컬 Z +90°)로 배치합니다. Mage의 시전 손은 오른손입니다.

게임에 연결할 때는 무기와 지면의 간격을 확인하세요. Rogue는 대기·피격 중 석궁이 낮게 내려가고, Warrior는 대각선 베기 회수 중 도끼가 지면을 통과할 수 있습니다. Mage의 지팡이는 일부 포즈에서 모자나 지면과 겹칩니다. 이동 중 발끝이나 뒤꿈치가 기준면 아래로 내려가는 구간도 있습니다.

출처: Kay Lousberg, KayKit Character Pack: Skeletons 1.0 (CC0). 원본 라이선스: LICENSE_KayKit.txt.
https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0
원본 커밋: 15b62b9bad122f72926c10fb14d622c73819fa54
