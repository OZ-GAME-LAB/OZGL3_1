지팡이 끝 조준·발사 / Player_Chibi

Unity에서 보기
  Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102/
  이 폴더의 Player_Chibi_StaffTipBolt.prefab을 빈 씬에 놓고 Play를 누르면
  실제 치비 모델이 지팡이를 들고 대기한다. 플레이 중 이 인스턴스를 선택해
  Animator 창의 Parameters에서 Cast 트리거를 누르면 한 번 발사한 뒤
  대기로 복귀한다. 스크립트에서는 Animator.SetTrigger("Cast")를 호출한다.

동작
  베이가 Q의 짧은 준비와 직선 발사를 참고해 치비 리그에 새로 키를 잡았다.
  Study_StaffTipBolt_Chibi.anim: 60fps, 0–54 프레임(0.90초), 제자리 동작.
  23–26 프레임에 지팡이 축이 수평을 유지한다. 26 프레임에 왼손 지팡이의
  EffectTip에서 탄을 발사한 뒤, 팔꿈치를 접으며 준비 자세로 돌아온다.
  Study_StaffTipIdle_Chibi.anim은 2.4초마다 반복되는 호흡과 체중 이동을 담는다.
  오른쪽 주먹은 옆구리 아래에 늘어뜨린 채 몸통의 움직임을 따라 앞뒤로
  흔들린다. 시전 중에는 조준과 반동을 뒤따라 흔들리고 대기로 이어진다.
  Study_StaffTipBolt_FX.anim은 같은 시각에 탄이 분리되는 모습을 보여주는
  시각 확인용 클립이다. 본편의 스킬 입력·피해 판정과 독립적으로 재생한다.

GIF 다시 만들기
  1. Unity 메뉴 Project M > Studies > Build and capture staff tip bolt를 실행한다.
     애니메이션, 재생 프리팹, 좌우 PNG 프레임과 verification.json을 갱신한다.
  2. Pillow가 설치된 Python으로 이 폴더의 compose_staff_aim.py를 실행한다.
     결과 위치: ~/Desktop/OZGL3_ArtReview/Characters/Protagonist01/Studies/ChibiStaffAimCast20261102/idle-sway/
     idle-both.gif는 좌우 측면의 대기 한 주기다. staff-aim-both.gif에서
     대기와 발사, 다시 대기로 돌아가는 흐름을 함께 본다. staff-aim-left.gif와
     staff-aim-right.gif는 개별 측면, right-arm-sway-detail.gif는 오른팔 확대다.

주먹 형태 수정
  이 프리팹은 왼손 지팡이 그립을 보존하고, 빈 오른손에만 닫힌 형태의
  메시와 피부 텍스처를 사용한다. 엄지와 손가락 모양을 바꿀 때는 별도
  Blender 프로세스에서 다음과 같이 조형과 텍스처를 다시 만든 뒤 Unity
  메뉴를 실행한다(스크립트와 같은 폴더에서 실행).
    blender --background --factory-startup --python build_right_fist.py
