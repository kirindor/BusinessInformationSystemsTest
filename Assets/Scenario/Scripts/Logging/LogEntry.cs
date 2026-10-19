using System;

[Serializable]
public class LogEntry
{
    public string type;
    public float time;
    public string stepId;
    public string group;
    public string result;
    public int attempt;
    public string message;

    public override string ToString()
    {
        var line = $"{time:0.00}  {type}";
        if (!string.IsNullOrEmpty(stepId))
            line += $"  [{stepId}]";
        if (!string.IsNullOrEmpty(result))
            line += $"  {result}";
        if (attempt > 0)
            line += $"  попытка {attempt}";
        if (!string.IsNullOrEmpty(message))
            line += $"  — {message}";
        return line;
    }
}
