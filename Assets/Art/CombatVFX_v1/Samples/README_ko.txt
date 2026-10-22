Project M Combat VFX v1 — isolated visual samples

이 폴더는 샘플 프리팹과 GIF 캡처용입니다. 기존 게임 씬·플레이어·전투·환경 자산에 자동 연결하지 않습니다.
프리팹: Prefabs/DashBurst, SlashWave, MagicMissile, Explosion
구성 기준값(초 / m/s / 입자 상한 합계 / Renderer 오브젝트 수):
DashBurst: 0.60 / 14.0(0.22초 이동) / 14 / 6
SlashWave: 0.95 / 7.5(0.58초 이동) / 12 / 7
MagicMissile: 1.05 / 8.5(0.68초 이동) / 16 / 6
Explosion: 1.55 / 이동 없음 / 61 / 7
위 입자 수는 ParticleSystem.maxParticles 합계이며 항상 그 수만큼 방출하지 않습니다. Renderer 수 역시 구성 수이며 실제 드로콜 실측값이 아닙니다. 최신 생성 결과는 BuildReport.json을 확인하세요.
방향: 효과의 로컬 +Z가 전방입니다. 횡스크롤 +X는 Quaternion.LookRotation(Vector3.right), 반대는 Vector3.left로 배치하세요.
API namespace: ProjectM.CombatVfxPreview
GetComponent<CombatVfxSample>().Play()로 샘플을 명시적으로 시작합니다. SetPreviewTime(seconds)는 60Hz 입자/메시 시뮬레이션을 고정 난수로 재현합니다. ResetPreview()로 처음으로 되돌립니다.
배치/활성화만으로 재생하지 않습니다. 샘플 재사용 시 ResetPreview()로 입자와 재생 시간을 지우며 끝나면 Completed 이벤트로 알립니다. 오브젝트를 자동 파괴하거나 기존 PoolManager에 등록하지 않습니다.
GIF 캡처는 원점에 프리팹을 두고 60Hz 순서로 SetPreviewTime(t)를 호출하세요. 진행 위치는 내부 Visual_Travel이 계산하므로 루트를 중복 이동시키지 않습니다. 루프 끝에 약간의 빈 프레임을 남기면 효과 시작점이 구별됩니다.
SlashWave는 검기 메시가 전진하고 MagicMissile은 코어·나선·리본 꼬리와 입자를 남깁니다. DashBurst는 짧은 바람 잔상, Explosion은 섬광·각진 불꽃·불티·연기·공중/지면 충격파입니다.
투명 효과는 깊이 검사를 유지합니다. 꼬리는 일정 면적의 메시 리본이며 화면 전체 왜곡·충돌·동적 광원·VFX Graph·외부 텍스처는 사용하지 않습니다.
입자 수 상한과 메시 수는 BuildReport.json의 구성값이며 CPU/GPU 성능 실측이나 목표 기기 프레임 보장 수치가 아닙니다.
생성 메뉴: Project M > Samples > Build Four Combat VFX Samples / CombatVfxBuilder.Build(). 추가 Meshy 크레딧은 사용하지 않습니다.
