using System;
using System.Collections.Generic;

public class StepRecord
{
    public string id;
    public string title;
    public string group;
    public int violations;
    public StepStatus status = StepStatus.Pending;
}

public static class LogReader
{
    public static List<StepRecord> ReadSteps(IReadOnlyList<LogEntry> log, List<string> warnings)
    {
        var steps = new List<StepRecord>();
        var hasPlan = false;
        var finished = false;

        foreach (var entry in log)
        {
            if (entry.type == LogEvents.RunFinished)
                finished = true;

            if (string.IsNullOrEmpty(entry.stepId))
                continue;

            var step = steps.Find(s => s.id == entry.stepId);
            if (step == null)
            {
                step = new StepRecord { id = entry.stepId, title = entry.stepId };
                steps.Add(step);
            }

            if (!string.IsNullOrEmpty(entry.group))
                step.group = entry.group;

            switch (entry.type)
            {
                case LogEvents.StepPlanned:
                    hasPlan = true;
                    if (!string.IsNullOrEmpty(entry.message))
                        step.title = entry.message;
                    break;
                case LogEvents.StepAction:
                    if (entry.result == LogEvents.Violation)
                        step.violations++;
                    break;
                case LogEvents.StepFinished:
                    if (Enum.TryParse(entry.result, out StepStatus status))
                        step.status = status;
                    break;
            }
        }

        if (log.Count == 0)
        {
            warnings.Add("Журнал пуст, оценивать нечего");
            return steps;
        }

        if (!hasPlan)
            warnings.Add("В журнале нет списка шагов (StepPlanned): учтены только шаги, по которым были события, максимум может быть занижен");

        if (!finished)
            warnings.Add("Прохождение не завершено: незавершённые шаги считаются невыполненными");

        return steps;
    }
}
