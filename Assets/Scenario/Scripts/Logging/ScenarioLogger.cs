using System.Collections.Generic;
using UnityEngine;

public class ScenarioLogger : MonoBehaviour
{
    [SerializeField] private List<LogEntry> _entries = new List<LogEntry>();

    public IReadOnlyList<LogEntry> Entries => _entries;

    public virtual void BeginRun()
    {
        _entries.Clear();
    }

    public virtual void Write(LogEntry entry)
    {
        _entries.Add(entry);
    }
}
