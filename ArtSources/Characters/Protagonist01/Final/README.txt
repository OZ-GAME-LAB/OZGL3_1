주인공 최종 사용 자산

확정 선택: 최신 검 공격 3종 + 기존 v007 비공격 모션 15종.
2026-10-27 후속 요청: Jump_Rise_Loop(0.8초), Jump_Fall_Loop(1.0초)를 작은 균형 움직임이 있는 공중 루프로 교체했습니다.
기존 점프 시전·착지·정점·Jump_Full 및 다른 선택 클립 16개는 그대로 유지했습니다.
최신 공중 동작과 연결 미리보기: Review/Airborne/index.html의 외부 갤러리 링크. 기존 Jump_Full에는 새 두 루프가 삽입되지 않았습니다.
GIF의 기본 저장소는 프로젝트 밖의 ~/Desktop/OZGL3_ArtReview입니다. OZGL3_ART_REVIEW_DIR로 다른 외부 위치를 지정할 수 있습니다.
현재 GIF60개를 외부에 복사하고 해시를 검증했습니다. 프로젝트의 옛 GIF 원본 삭제는 자동 승인 검토가 차단하여 남아 있으며, 현재 갤러리에서는 외부본을 사용합니다.
2026-10-27 전체 검수에서 공격 모델의 팔꿈치 주변 스킨 가중치만 제한적으로 보정했습니다. 애니메이션 곡선, 관절, 검과 손의 움직임은 유지했습니다.

사용 파일
- 기존 모션: Assets/Art/Characters/Protagonist01/ChibiMotion_v007
- 검 공격: Assets/Art/Characters/Protagonist01/Combat
- 검 공격 프리팹: Combat/Prefabs/Protagonist_Combat.prefab
- 전체 선택 목록: selection.json
- 최종 GIF 갤러리: Review/index.html

원본
- Model: 텍스처와 24본 리그가 내장된 기존 chibi 모델 GLB, 콘셉트와 생성 출처.
- LegacyMotions: 승인된 15종의 선택 목록, 모션 길이와 Unity 파일 해시, 필요한 제작 기록. 현재 GIF는 외부 저장소의 같은 상대 경로에 있습니다.
- Combat: 승인된 공격 3종만 담은 편집 가능한 Blender 파일, 텍스처, CC0 라이선스와 검증 기록.
- Combat/SkinWeights: 팔꿈치 보정 전후 영향도와 Unity 메시 갱신에 사용하는 확정 데이터.

리그 호환성
기존 15종은 Chibi24, 공격 3종은 KayKit23 전용 Generic 클립입니다.
두 세트는 뼈 이름·계층·스킨과 손 표현이 달라 같은 Animator에 바로 혼용할 수 없습니다.
각 클립의 compatiblePrefab을 selection.json에 기록했습니다.
이번 정리는 최종 사용 원본을 보존하는 작업이며, 한 리그로의 재변환이나 게임 연결을 수행한 것은 아닙니다.

보존한 의존성
ChibiCap_v006의 리그 FBX/리그 프리팹/손 morph 메시/재질/텍스처는 기존 모션 제작에 필요한 원본입니다.
ChibiMotion_v007/SourceGait는 해당 모션 제작 코드의 입력입니다.
Runtime 및 루트의 기존 베이스 컬러는 현재 플레이어 프리팹과 씬에서 참조하므로 유지했습니다.
게임 코드·컨트롤러·씬·VFX·환경·몬스터 모션과 모델 자체는 변경하지 않았습니다.
몬스터 비교 검수 도구의 주인공 참조 경로 한 곳만 Final/Model/Protagonist_ComparisonReference.blend로 갱신했습니다. 이 파일은 기존 비교용 모습을 유지하는 정지 모델이며 애니메이션이 없습니다.

정리 기록
Validation/cleanup-result.json: 실제 삭제한 대상과 파일 수/용량.
Validation/preservation-result.json: 선택 자산과 기존 필수 파일 보존 검사.
Validation/guid-validation-current.json: 정리 후 Unity 참조 검사.
Validation/Review20261027: 이후 실시한 전체 프레임·다시점 검수와 팔꿈치 보정 기록. 앞의 정리 시점 해시 증거와 구분해서 사용해야 합니다.
Validation/Airborne20261027: 후속 상승·낙하 루프 수정과 검수 기록. 앞의 전체 검수는 이번 두 클립 수정 전의 역사적 기록입니다.
Validation/StorageCleanup20261027: 머지 후 사용 의존성, 외부GIF복사검증, 삭제 후보와 자동 승인 차단으로 남은 파일 목록.
현재 팀원 작업에서 Player_Chibi.controller는 기존 v007모션8개를 참조합니다. 이번 Rise/Fall2개는 게임 컨트롤러에 연결하지 않았습니다.
