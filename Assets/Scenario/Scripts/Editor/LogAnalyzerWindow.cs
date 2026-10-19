using System.IO;
using UnityEditor;
using UnityEngine;

public class LogAnalyzerWindow : EditorWindow
{
    private ScenarioAnalyzer _analyzer;
    private string _path;
    private string _report;
    private Vector2 _scroll;

    [MenuItem("Tools/Scenario Log Analyzer")]
    private static void Open()
    {
        GetWindow<LogAnalyzerWindow>("Log Analyzer");
    }

    private void OnGUI()
    {
        _analyzer = (ScenarioAnalyzer)EditorGUILayout.ObjectField("Analyzer", _analyzer, typeof(ScenarioAnalyzer), false);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Log file", string.IsNullOrEmpty(_path) ? "—" : Path.GetFileName(_path));
        if (GUILayout.Button("Open...", GUILayout.Width(70)))
            _path = EditorUtility.OpenFilePanel("Scenario log", Path.Combine(Application.persistentDataPath, "ScenarioLogs"), "json");
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(_analyzer == null || string.IsNullOrEmpty(_path)))
        {
            if (GUILayout.Button("Analyze file"))
                _report = _analyzer.Analyze(LogFile.Load(_path)).ToString();
        }

        using (new EditorGUI.DisabledScope(_analyzer == null || !Application.isPlaying))
        {
            if (GUILayout.Button("Analyze current run"))
                _report = _analyzer.Analyze(FindAnyObjectByType<ScenarioRunner>().Logger.Entries).ToString();
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_report, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }
}
