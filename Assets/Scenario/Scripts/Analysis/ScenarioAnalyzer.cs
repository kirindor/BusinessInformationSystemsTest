using System.Collections.Generic;
using UnityEngine;

public abstract class ScenarioAnalyzer : ScriptableObject
{
    [SerializeField] private string _title;

    public string Title => _title;

    public abstract AnalysisResult Analyze(IReadOnlyList<LogEntry> log);
}
