using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class LogFile
{
    public List<LogEntry> entries = new List<LogEntry>();

    public static void Save(string path, IEnumerable<LogEntry> entries)
    {
        var file = new LogFile { entries = new List<LogEntry>(entries) };
        File.WriteAllText(path, JsonUtility.ToJson(file, true));
    }

    public static List<LogEntry> Load(string path)
    {
        return JsonUtility.FromJson<LogFile>(File.ReadAllText(path)).entries;
    }
}
