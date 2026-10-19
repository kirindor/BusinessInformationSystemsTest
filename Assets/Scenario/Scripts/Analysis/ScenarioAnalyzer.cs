using System.Collections.Generic;
using UnityEngine;

public abstract class ScenarioAnalyzer : ScriptableObject
{
    [SerializeField] private string title;

    public string Title => title;

    public abstract AnalysisResult Analyze(IReadOnlyList<LogEntry> log);
}
