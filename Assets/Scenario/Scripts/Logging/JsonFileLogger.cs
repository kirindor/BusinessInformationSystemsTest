using System;
using System.IO;
using UnityEngine;

public class JsonFileLogger : ScenarioLogger
{
    [SerializeField] private string folder = "ScenarioLogs";

    public string FilePath { get; private set; }

    public override void BeginRun()
    {
        base.BeginRun();
        var directory = Path.Combine(Application.persistentDataPath, folder);
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, $"run_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        Debug.Log($"Журнал прохождения: {FilePath}");
    }

    public override void Write(LogEntry entry)
    {
        base.Write(entry);
        LogFile.Save(FilePath, Entries);
    }
}
