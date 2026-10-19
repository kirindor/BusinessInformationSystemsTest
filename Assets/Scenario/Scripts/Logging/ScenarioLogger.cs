using System.Collections.Generic;
using UnityEngine;

public class ScenarioLogger : MonoBehaviour
{
    [SerializeField] private List<LogEntry> entries = new List<LogEntry>();

    public IReadOnlyList<LogEntry> Entries => entries;

    public virtual void BeginRun()
    {
        entries.Clear();
    }

    public virtual void Write(LogEntry entry)
    {
        entries.Add(entry);
    }
}
