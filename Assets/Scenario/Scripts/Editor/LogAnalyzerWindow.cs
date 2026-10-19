using System.IO;
using UnityEditor;
using UnityEngine;

public class LogAnalyzerWindow : EditorWindow
{
    private ScenarioAnalyzer analyzer;
    private string path;
    private string report;
    private Vector2 scroll;

    [MenuItem("Tools/Scenario Log Analyzer")]
    private static void Open()
    {
        GetWindow<LogAnalyzerWindow>("Log Analyzer");
    }

    private void OnGUI()
    {
        analyzer = (ScenarioAnalyzer)EditorGUILayout.ObjectField("Analyzer", analyzer, typeof(ScenarioAnalyzer), false);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Log file", string.IsNullOrEmpty(path) ? "—" : Path.GetFileName(path));
        if (GUILayout.Button("Open...", GUILayout.Width(70)))
            path = EditorUtility.OpenFilePanel("Scenario log", Path.Combine(Application.persistentDataPath, "ScenarioLogs"), "json");
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(analyzer == null || string.IsNullOrEmpty(path)))
        {
            if (GUILayout.Button("Analyze file"))
                report = analyzer.Analyze(LogFile.Load(path)).ToString();
        }

        using (new EditorGUI.DisabledScope(analyzer == null || !Application.isPlaying))
        {
            if (GUILayout.Button("Analyze current run"))
                report = analyzer.Analyze(FindAnyObjectByType<ScenarioRunner>().Logger.Entries).ToString();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }
}
