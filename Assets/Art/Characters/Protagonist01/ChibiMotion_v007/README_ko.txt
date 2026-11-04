주인공 Chibi 모션 (Generic 24본)

이 폴더의 Clips/에는 Visual/Armature/Hips 계층에 맞춘 클립 20개가 있습니다. 맨손은 Prefabs/Protagonist_Chibi_MotionReview.prefab, 검과 지팡이는 각각 Prefabs/Protagonist_Chibi_SwordGrip.prefab과 Prefabs/Protagonist_Chibi_StaffGrip.prefab을 사용합니다. 세 프리팹의 뼈는 같고 손 메시만 다릅니다.

모션 확인
프리팹을 빈 씬에 배치하고 인스턴스의 루트 Animator를 활성화합니다. Animator Controller에 이 폴더의 클립을 기본 상태로 연결해 Play하면 됩니다. 기본 프리팹에는 Controller나 Avatar가 지정되어 있지 않습니다. 런타임 입력, 판정과 이동은 호출자가 연결합니다.

지팡이 조준·발사 샘플은 별도 컨트롤러까지 연결된 ../AnimationStudies/StaffTipBolt20261102/Player_Chibi_StaffTipBolt.prefab입니다. 빈 씬에 배치하고 Play하면 무장 대기가 반복됩니다. 인스턴스의 Animator 창 > Parameters에서 Cast 트리거를 누르면 왼손의 지팡이 끝에서 발사한 뒤 대기로 돌아옵니다. 이 프리팹의 발사체는 시각 샘플입니다.

클립 목록 (Clips/)
• 이동: Idle_Fists, Walk_Fists, Run_Fists, Dash_Forward, Ladder_Climb_Loop
• 점프: Jump_Full, Jump_Takeoff, Jump_Rise_Loop, Jump_Apex_Loop, Jump_Fall_Loop, Jump_Land
• 전투·피격: Longsword_Attack_Chop, Longsword_Attack_Slice, Longsword_Attack_Stab, Hit_Reaction, Death_Fall
• 소품·상호작용: Potion_Drink, Item_Throw_Overhand, Interact_Reach, Pickup_Low

Walk, Run, Dash는 제자리 클립입니다. Jump_Full은 골반에 수직 궤적을 포함합니다. 물리 컨트롤러가 높이를 정하는 경우에는 Takeoff → Rise → Apex → Fall → Land를 조합해 높이를 두 번 적용하지 마세요. Rise와 Fall은 공중 대기 루프입니다.

소품 장착
각 손 아래에 WeaponSocket과 WeaponSocket_Left가 있습니다. 오른손 WeaponSocket에는 소품용 위치·회전·배율이 적용되어 있으므로 소켓 아래에 렌더 소품을 놓을 때는 로컬 위치와 회전을 초기화하고 배율을 1로 둡니다. 포션·투척·줍기처럼 손을 펼치는 동작은 맨손 프리팹을 사용하세요. 장착/해제, 투척 물리와 판정 이벤트는 클립 밖에서 처리합니다.

SourceGait/에는 자세 비교용 Skeleton.prefab과 대기·보행·달리기 원본 클립이 있습니다. ../Combat/Clips의 동명 검 클립은 23본 KayKit 계층에 맞춘 별도 버전이므로 이 프리팹에는 현재 폴더의 Clips/ 버전을 사용하세요.
