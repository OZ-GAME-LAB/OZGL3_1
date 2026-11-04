전투 VFX 샘플 4종

프리팹은 상위 Prefabs/ 폴더에 있습니다. DashBurst는 대시 잔상, SlashWave는 전진하는 검기, MagicMissile은 꼬리를 남기는 탄환, Explosion은 폭발 섬광과 충격파입니다. 프리팹을 씬에 배치해도 자동으로 재생되지는 않습니다.

재생: ProjectM.CombatVfxPreview 네임스페이스의 CombatVfxSample 컴포넌트에서 Play()를 호출합니다. ResetPreview()는 입자와 시간을 초기화하고, Completed 이벤트는 재생 완료를 알립니다. SetPreviewTime(seconds)으로 특정 시간을 볼 수도 있습니다. 게임의 오브젝트 풀 및 타격 판정은 호출자가 연결합니다.

전방은 효과의 로컬 +Z입니다. 횡스크롤 +X에는 Quaternion.LookRotation(Vector3.right), -X에는 Quaternion.LookRotation(Vector3.left)를 사용합니다. 전진은 자식 Visual_Travel에서 처리하므로 프리팹 루트를 같은 속도로 또 이동시키지 마세요.

효과           재생 시간   전진 속도 / 시간     입자 상한 합계
DashBurst       0.60초      14.0m/s / 0.22초     14
SlashWave       0.95초       7.5m/s / 0.58초     12
MagicMissile    1.05초       8.5m/s / 0.68초     16
Explosion       1.55초       제자리               61

입자 상한은 ParticleSystem.maxParticles 값의 합이며 실제 발사량과 다를 수 있습니다.
