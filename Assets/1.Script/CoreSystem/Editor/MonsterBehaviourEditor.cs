using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonsterBehaviour), true)]
[CanEditMultipleObjects]
public class MonsterBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("현재 상태", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            var behaviour = (MonsterBehaviour)target;
            EditorGUI.showMixedValue = targets.Length > 1;
            EditorGUILayout.EnumPopup("State", behaviour.State);
            EditorGUI.showMixedValue = false;
        }
        if (targets.Length == 1 && target is BossMonsterBehaviour bossBehaviour)
            DrawBossDebugger(bossBehaviour);
    }

    private static void DrawBossDebugger(BossMonsterBehaviour behaviour)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("보스 패턴 디버그", EditorStyles.boldLabel);
        BossMonster boss = behaviour.GetComponent<BossMonster>();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("선택된 패턴", behaviour.CurrentPattern);
            EditorGUILayout.Toggle("공격 진행 중", behaviour.IsAttacking);
            EditorGUILayout.Toggle("돌진 중", behaviour.IsCharging);
            EditorGUILayout.ObjectField("플레이어", behaviour.Target, typeof(Transform), true);
            if (boss != null)
            {
                EditorGUILayout.FloatField("현재 HP", boss.CurrentHP);
                EditorGUILayout.FloatField("패턴 전환 HP 비율", boss.PatternTriggerHpRatio);
            }
        }
        EditorGUILayout.HelpBox("플레이 중 사용하세요. 수동 제어는 자동 이동·공격 판단을 멈춥니다. " +
            "공격 버튼은 거리·체력에 따른 선택만 생략하며, 실제 타격·지형 검사는 유지합니다. " +
            "근접 타격에는 클립의 OnAttackHit 이벤트가 필요합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !behaviour.isActiveAndEnabled ||
            boss == null || boss.IsDead))
        {
            bool manual = EditorGUILayout.Toggle("수동 제어", behaviour.DebugManualControl);
            if (manual != behaviour.DebugManualControl)
            {
                behaviour.DebugStopPattern();
                behaviour.DebugManualControl = manual;
            }
            using (new EditorGUI.DisabledScope(behaviour.IsAttacking || behaviour.Target == null ||
                !behaviour.Target.gameObject.activeInHierarchy))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("근접 실행")) behaviour.DebugRunPattern(BossMonsterBehaviour.BossAttackPattern.Melee);
                if (GUILayout.Button("원거리 실행")) behaviour.DebugRunPattern(BossMonsterBehaviour.BossAttackPattern.Ranged);
                if (GUILayout.Button("돌진 실행")) behaviour.DebugRunPattern(BossMonsterBehaviour.BossAttackPattern.Charge);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("공격 중단"))
            {
                behaviour.DebugManualControl = true;
                behaviour.DebugStopPattern();
            }
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
