using UnityEngine;

public class ConsoleLogger : ScenarioLogger
{
    public override void Write(LogEntry entry)
    {
        base.Write(entry);
        Debug.Log($"[Scenario] {entry}");
    }
}
