주인공 양손검 — KayKit 기반 Generic 23본

캐릭터: Prefabs/Protagonist_Combat.prefab
무기: Prefabs/Longsword_KayKitSocket.prefab
편집 원본: ArtSources/Characters/Protagonist01/Final/Combat/Protagonist_Combat.blend

Clips/Longsword_Attack_Chop.anim (0.7초), Longsword_Attack_Slice.anim (0.6초), Longsword_Attack_Stab.anim (0.7초)을 위 캐릭터에 사용합니다. FBX는 기본 리그이고 실제 모션은 .anim 파일에 있습니다. 클립은 Protagonist_KayKit_v018과 Hero_KayKit_Rig 등의 계층 이름을 사용하므로 리그의 이름이나 부모를 변경하면 바인딩도 변경해야 합니다. ../ChibiMotion_v007에는 Visual/Armature/Hips 기준 24본용 동명 클립이 따로 있습니다.

기본 프리팹에서 WeaponRoot는 비활성화되어 있고 각 공격 클립이 해당 자식을 활성화하고 움직입니다. 무기를 교체하려면 Weapon_Attachment.json에 기록된 부모, 이름 및 초기 로컬 변환을 유지하세요. 다른 이름의 렌더 무기를 추가하면 중복해서 보일 수 있습니다.

애니메이션·원본 무기 출처: Kay Lousberg / KayKit, Character Animations 및 Character Pack: Skeletons (CC0).
https://kaylousberg.itch.io/kaykit-character-animations
https://kaylousberg.itch.io/kaykit-skeletons
Licenses/에는 원본 CC0 문서 2개, 애니메이션 원본과 클립 대응표 및 패키지 출처가 있습니다. KayKit 표기는 해당 소스에 적용됩니다.
