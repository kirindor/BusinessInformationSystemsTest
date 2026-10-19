using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Training Analyzer")]
public class TrainingAnalyzer : ScenarioAnalyzer
{
    [Tooltip("Баллы за шаг, выполненный с 1-й, 2-й, ... попытки. Для последующих попыток берётся последнее значение")]
    [SerializeField] private List<int> _pointsByAttempt = new List<int> { 10, 5, 0 };

    public override AnalysisResult Analyze(IReadOnlyList<LogEntry> log)
    {
        var result = new AnalysisResult(Title);

        foreach (var step in LogReader.ReadSteps(log, result.warnings))
        {
            var completed = step.status == StepStatus.Completed;
            var attempts = completed ? step.violations + 1 : step.violations;
            var points = completed ? _pointsByAttempt[Mathf.Min(attempts, _pointsByAttempt.Count) - 1] : 0;

            result.score += points;
            result.maxScore += _pointsByAttempt[0];
            result.lines.Add($"{step.title}: {step.status}, попыток {attempts}, баллов {points}");
        }

        return result;
    }
}
