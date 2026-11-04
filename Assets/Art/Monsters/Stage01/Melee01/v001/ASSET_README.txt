1스테이지 근접 해골 샘플

프리팹: Prefabs/Stage01_Melee01.prefab
모델: FBX/Stage01_Melee01.fbx
씬: Review/Stage01_Melee01_Review.unity. 카메라와 기준 바닥을 둔 정적 비교 씬입니다. 불투명 바닥은 기준면보다 5cm 낮고 주황색 표식이 Y=0입니다.

프리팹의 Animator는 활성화되어 있으며 Generic Avatar를 사용합니다. 별도 Controller는 연결되어 있지 않으므로 인스턴스의 Animator에 Clips/의 클립을 사용하는 Controller를 연결하세요. 캐릭터의 전방은 +Z입니다. 횡스크롤 +X로 배치할 때는 바깥 부모를 Y축으로 90도 회전하고 내부 리그 이름은 유지하세요. 루트 모션은 꺼져 있습니다.

클립: Idle(4.27초, 반복), Move(0.80초, 제자리 반복), Attack(1.07초), Hit(0.67초).
Move와 Attack에는 각각 약 3.23cm와 0.59cm의 일정한 수직 접지 오프셋이 있습니다. 무기는 손 소켓에서 X축 -90도 회전합니다. 적 AI, 타격·사망 판정, 이동 속도와 상태 전환은 호출자가 연결합니다.

원본 모델·스키닝·리그·애니메이션: Kay Lousberg, KayKit Character Pack: Skeletons 1.0 (CC0).
https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0
원본 라이선스: Licenses/KayKit_CC0.txt
