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
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
