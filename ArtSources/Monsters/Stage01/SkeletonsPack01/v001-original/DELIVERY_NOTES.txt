Project M / KayKit Skeletons 1.0 / Rogue, Warrior, Mage
2026-10-29

출처 및 보존
- KayKit Character Pack Skeletons 1.0, CC0.
- https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0
- 고정 커밋 15b62b9bad122f72926c10fb14d622c73819fa54.
- SourceAssets의 FBX/GLB/glTF/atlas/license는 원본 바이트를 보존한다.
- Editable의 .blend 3개는 원본 GLB를 가져와 저장한 편집 원본이며, 41개 본과 95개 액션을 각각 보존한다.
- Unity에는 원본 FBX를 직접 임포트한다. 원본 GLB에만 있는 독립 Icosphere helper는 FBX에 없다.
- 캐릭터 메시·스키닝·본·키·타이밍을 재제작하거나 리타깃하지 않았다.

Unity 구성
- Assets/Art/Monsters/Stage01/SkeletonsPack01/v001
- FBX: 각 캐릭터의 원본 95개 동작 보존. Generic, hierarchy 유지, curve/mesh compression 없음.
- Prefabs/Skeleton_Rogue.prefab, Skeleton_Warrior.prefab, Skeleton_Mage.prefab: 독립 프리팹.
- 공통 외부 부모 배율 0.585289224. 원본 mesh/bone 데이터에 스케일을 굽지 않았다.
- Animator는 프리팹 안의 원본 모델 자식에 있다. 외부 부모는 게임 단위 배율용이다.
- AnimatorController는 연결하지 않았다. 클립은 해당 내부 Animator에서 사용한다.
- Materials: 원래 atlas 및 노란 눈 팔레트를 공유한다. 원본의 PBR 조명 전체를 재현하는 셰이더는 아니며, 프로젝트의 단순 면 표현을 위한 경량 셰이더다.
- 원본 노멀·본 가중치·Rigid Hood/Cape/Helmet/Hat를 보존한다. 메시 병합·감량·본 삭제는 하지 않았다.
- 별도 무기를 손 소켓에 조립했다. Rogue: 오른손 석궁 local Y=-90도; Warrior: 오른손 도끼 identity; Mage: 왼손 지팡이 local Z=+90도. 원본 무기 기하·비율은 유지한다.

선택 클립 / 실제 원본 이름
- Rogue(6): Idle_Combat, Running_A, 2H_Ranged_Aiming, 2H_Ranged_Shoot, 2H_Ranged_Reload, Hit_A.
- Warrior(5): Idle_Combat, Walking_B, 1H_Melee_Attack_Chop, 1H_Melee_Attack_Slice_Diagonal, Hit_A.
- Mage(5): Idle_Combat, Walking_D_Skeletons, Spellcast_Shoot, Spellcast_Raise, Hit_A.
- Idle/Walking/Running에만 loop metadata를 켰다. 원본 곡선과 복제 .anim의 시간·값·탄젠트·가중치 해시를 비교한다.
- Aiming은 팔을 내린 자세에서 조준 자세로 바뀌는 단발 동작이다. 반복 대기로 사용할 수 없다.
- 새 16개 외에 기존 Minion Idle/Move/Attack/Hit 4개도 GIF 검수에 포함한다.
- 죽음·등장·점프·회전을 새 게임용 클립으로 선택하지 않았다. 필요하면 보존된 95개 원본에서 검토한다.

알려진 제한 / 배포 승인과 구분
- 원본 곡선 보존과 임포트 검증은 시각 품질 승인과 다르다.
- Rogue: 범용 대기·피격에서 큰 원본 석궁의 앞부분이 바닥 기준선 아래로 내려가는 구간이 있다. 조준·발사와 범용 대기는 무기 파지가 완전히 일관된 세트가 아니다.
- Mage: 지팡이를 왼손에 두어 오른손 시전을 확보했지만 모자/장식 겹침과 일부 낮은 자세의 지팡이 바닥 접촉이 남는다.
- 이 문제를 가리려고 팔/손목/몸통 키를 수정하거나 무기 크기를 임의로 줄이지 않았다. 원본 맨손 모델/95동작과 무기는 별도로 보존되어 있다.
- Warrior는 짧은 원본 도끼에 양손 손잡이 접촉이 맞지 않아 같은 팩의 한손 클립을 선택했다. 무기와 리그를 늘리지 않았다.
- Warrior 대각선 베기 회수 중 도끼 끝의 지면 통과도 남는다. 새 3종 이동 중 일부 발끝/뒤꿈치가 Y=0을 소폭 통과한다(Rogue Running_A f5, Warrior Walking_B f8, Mage Walking_D_Skeletons f7). 무기 최저점과 구분해 실제 이미지로 확인했다.
- 원본 범용 클립 간 끝/시작 자세가 모두 이어지는 것은 아니다. 연결 GIF는 검수용 0.12초 혼합이며 실제 게임 Animator 전환을 바꾼 결과가 아니다.
- QA의 전체 최저Y 수치는 무기를 포함하므로 발 접지값으로 해석하면 안 된다.
- 최종 전체 동작 시각 검수 기록은 QA/final-visual-review.*를 확인한다.
- 메인 프로젝트 임포트 검증은 통과했다(QA/main-project-import-validation.json). 전체 시각 검수는 완료했지만 위 간섭을 해결한 최종 게임 완성품으로 승인하지 않았다.

기존 Minion에 관한 정정
- 기존 v001은 완전 무수정 원본이 아니다. 이전 제작에서 공통 크기 맞춤, 청록 망토/주황 눈, 무기 부착축, 이동·공격의 일정한 바닥 높이 보정이 있었다.
- 이번 추가 작업에서는 기존 Minion의 FBX/프리팹/클립/재질을 수정하지 않았다. 주인공 참조를 포함한 22개 보호 파일의 SHA256을 보존했다.

검수 자료
- 외부 폴더: C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original/index.html
- 기존 Minion 포함 20개 클립의 1배속·0.5배속 측면 GIF. 실제 Unity 스키닝 모델을 같은 월드 단위와 고정 카메라로 캡처한다.
- 왼쪽은 현재 주인공의 고정 참조. 원본 주인공은 수정하지 않으며, 격리된 Built-in 검수 복제본의 재질에 원래 albedo 텍스처·색만 연결했다.
- 전체 PNG, 주요 자세, 정면·반대쪽·후면, 연결 미리보기와 수치 보고서를 함께 보존한다.
- 원본 속도는 유지한다. 단발 GIF 끝의 0.5초 정지는 반복 사이의 안내용이며 원본 키에는 추가하지 않았다.
- 미리보기 장면은 Review 아래 별도로 저장한다. 실제 플레이 장면/Animator 전환/인게임 코드/VFX는 변경하지 않는다.

재현
1. Tools/prepare_review.py로 .local-validation/SkeletonsPack01 격리 프로젝트와 참조 복제본 준비.
2. Tools/run_review.ps1 -Mode BuildAndRender. 실제 그래픽 장치를 사용한 Unity 캡처.
3. Tools/compose_review.py로 GIF/전체 프레임 시트/HTML 생성.
4. Tools/publish_assets.py로 이 새 family만 게임 프로젝트에 복사.
5. 실제 게임 프로젝트 Unity에서 SkeletonPackValidate.Inspect 실행.

라이선스/활용 에셋 통합 목록
planning/Project_M_활용에셋목록_2026-10-29.txt
