무기별 치비 이동 동작

Sword/Player_Chibi_Sword_Motions.prefab과 Staff/Player_Chibi_Staff_Motions.prefab을 빈 씬에 놓고 Play하면 무기별 대기를 볼 수 있습니다. 플레이 중 해당 인스턴스를 선택한 뒤 Animator의 Parameters 탭에서 Run을 켜면 제자리에서 달립니다. Jump를 누르면 한 번 뛰고, 착지 후 Run 값에 따라 달리기나 대기로 돌아옵니다.

검은 오른손 소켓에 롱소드를, 스태프는 왼손에 수정을 단 지팡이를 듭니다. 스태프 대기는 AnimationStudies/StaffTipBolt20261102의 Study_StaffTipIdle_Chibi.anim을 공유합니다.

Jump_Full은 골반에 점프 높이가 들어 있는 완결형 클립입니다. Rigidbody로 높이를 제어하는 플레이어에는 같은 무기 폴더의 Jump_Takeoff, Jump_Rise_Loop, Jump_Apex_Loop, Jump_Fall_Loop, Jump_Land를 도약, 상승, 정점, 낙하, 착지 상태에 연결합니다. 각 폴더의 컨트롤러는 해당 무기의 모션을 확인하는 용도입니다.

대시 사용

- 씬에서 확인하려면 위 프리팹 중 하나를 놓고 Play한 뒤, 해당 인스턴스의 Animator에서 Dash 트리거를 누릅니다. 대기 중에도 달리는 중에도 실행할 수 있습니다. 동작이 끝나면 현재 Run 값에 따라 돌아옵니다.
- 이 프리팹을 코드에서 제어할 때는 루트의 Animator에 SetTrigger("Dash")를 호출합니다.
- 다른 치비 Animator Controller에서 사용하려면 Sword/Sword_Dash.anim 또는 Staff/Staff_Dash.anim을 상태의 Motion에 연결하고 Speed를 1로 설정합니다. 두 클립은 60fps로 작성한 0.40초짜리 제자리 동작입니다. 캐릭터의 이동 거리는 이동 코드에서 제어합니다.
- 손에 맞는 무기 소켓과 그립은 같은 폴더의 Player_Chibi_*_Motions.prefab에 설정되어 있습니다. 다른 프리팹에 클립만 적용할 때도 같은 치비 리그와 해당 무기의 손·소켓 구성을 사용합니다.

스태프 내려찍기 시전

- Staff/Player_Chibi_Staff_Motions.prefab에서 Skill 트리거를 누르면 왼손의 스태프를 들어 바닥에 내려찍고 원래 대기로 복귀합니다. Run 값이 켜져 있으면 달리기로 복귀합니다.
- Staff/Staff_Skill_GroundSlam.anim은 60fps로 작성한 0.60초짜리 제자리 동작입니다. Speed를 1로 재생할 때 0.25초에 스태프 아래 끝이 바닥에 닿고, 0.333초까지 접촉 자세를 유지합니다.
- 이동·스킬 로직에 연결할 때는 스킬 상태의 길이에 맞게 재생 속도를 정하고, 효과 발생 시점을 클립의 0.25초 접촉에 맞춥니다.
