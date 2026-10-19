using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Exam Analyzer")]
public class ExamAnalyzer : ScenarioAnalyzer
{
    [SerializeField] private int _maxScore = 100;
    [SerializeField] private int _failedPenalty = 10;
    [SerializeField] private int _skippedPenalty = 5;

    public override AnalysisResult Analyze(IReadOnlyList<LogEntry> log)
    {
        var result = new AnalysisResult(Title) { maxScore = _maxScore };
        var steps = LogReader.ReadSteps(log, result.warnings);

        var groups = new List<string>();
        foreach (var step in steps)
        {
            if (!groups.Contains(step.group))
                groups.Add(step.group);
        }

        if (groups.Contains(null))
            result.warnings.Add("Не у всех шагов в журнале указана группа, они собраны в «Без группы»");

        var penalty = 0;
        foreach (var group in groups)
        {
            result.lines.Add(group ?? "Без группы");
            var groupPenalty = 0;

            foreach (var step in steps)
            {
                if (step.group != group)
                    continue;

                var outcome = GetOutcome(step);
                var stepPenalty = outcome == StepStatus.Failed ? _failedPenalty
                    : outcome == StepStatus.Skipped ? _skippedPenalty
                    : 0;

                groupPenalty += stepPenalty;
                result.lines.Add($"    {step.title}: {outcome}, штраф {stepPenalty}");
            }

            result.lines.Add($"    Штраф группы: {groupPenalty}");
            penalty += groupPenalty;
        }

        result.score = Mathf.Max(0, _maxScore - penalty);
        return result;
    }

    private static StepStatus GetOutcome(StepRecord step)
    {
        if (step.violations > 0 || step.status == StepStatus.Failed)
            return StepStatus.Failed;

        return step.status == StepStatus.Completed ? StepStatus.Completed : StepStatus.Skipped;
    }
}
