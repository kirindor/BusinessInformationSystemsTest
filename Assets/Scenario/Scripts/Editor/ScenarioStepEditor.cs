using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScenarioStep))]
public class ScenarioStepEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Открыть в редакторе сценариев", GUILayout.Height(26)))
            ScenarioEditorWindow.Open((ScenarioStep)target);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
