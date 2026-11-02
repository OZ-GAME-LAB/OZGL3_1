무기별 치비 이동 동작

Sword/Player_Chibi_Sword_Motions.prefab과 Staff/Player_Chibi_Staff_Motions.prefab을 빈 씬에 놓고 Play하면 무기별 대기를 볼 수 있습니다. 플레이 중 해당 인스턴스를 선택한 뒤 Animator의 Parameters 탭에서 Run을 켜면 제자리에서 달립니다. Jump를 누르면 한 번 뛰고, 착지 후 Run 값에 따라 달리기나 대기로 돌아옵니다.

검은 오른손 소켓에 롱소드를, 스태프는 왼손에 수정을 단 지팡이를 듭니다. 스태프 대기는 AnimationStudies/StaffTipBolt20261102의 Study_StaffTipIdle_Chibi.anim을 공유합니다.

Jump_Full은 골반에 점프 높이가 들어 있는 완결형 클립입니다. Rigidbody로 높이를 제어하는 플레이어에는 같은 무기 폴더의 Jump_Takeoff, Jump_Rise_Loop, Jump_Apex_Loop, Jump_Fall_Loop, Jump_Land를 도약, 상승, 정점, 낙하, 착지 상태에 연결합니다. 각 폴더의 컨트롤러는 해당 무기의 모션을 확인하는 용도입니다.
